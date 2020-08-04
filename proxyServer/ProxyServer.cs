//#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE1006 // Naming Styles

using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using Microsoft.Win32.SafeHandles;
using proxyServer.Interfaces;

namespace proxyServer
{
	public class ProxyServer :VBase,  ISettings, IDisposable
	{
		// IDisposable implementation
		protected override void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				handle.Dispose();
				if (started)
				{
					StopServer();
					server.Dispose();
				}
				if (_timer != null)
				{
					_timer.Stop();
					_timer.Dispose();
					_timer = null;
				}
				ipv4Addr = null;
				console = null;
				clientList = null;
			}

			disposed = true;
		}

		// ISettings Implementation

		public void LoadSettings(KeyValuePair<string, string> kvp)
		{
			string key = kvp.Key.ToLower();
			string value = kvp.Value.ToLower();

			if (key == "auto_allow") autoAllow = value == "true";
			if (key == "http_mode") SetMode(StringToMode(value), "http");
			if (key == "https_mode") SetMode(StringToMode(value), "https");
		}

		public void WriteSettings(System.Xml.XmlWriter xml)
		{
			xml.WriteStartElement("settings_start");
			xml.WriteElementString("auto_allow", autoAllow ? "true" : "false");
			xml.WriteElementString("http_mode", ModeToString(httpMode));
			xml.WriteElementString("https_mode", ModeToString(httpsMode));
			xml.WriteEndElement();
		}

		//Proxy Server

		Socket server;
		string ipv4Addr;
		int port;
		int pclimit;
		VConsole console;
		List<Socket> clientList = new List<Socket>();
		bool stopping = false;
		bool started = false;
		Mode httpMode;
		Mode httpsMode;
		private readonly Form1 ctx;
		private readonly VDependencyWatcher dw;
		System.Windows.Forms.Timer _timer;

		public bool autoAllow = true;
		public bool autoClean = false;

		public enum Mode : int
		{
			forward = 0,
			MITM = 1,
			Undefined = 2
		}

		struct ReadObj
		{
			public Socket s;
			public byte[] buffer;
			public Request request;
		}

		public ProxyServer(string ipAddress, int portNumber, int pendingLimit, VConsole consoleMod, Form1 context)
		{
			ipv4Addr = ipAddress;
			port = portNumber;
			pclimit = pendingLimit;
			console = consoleMod;
			ctx = context;
			dw = context.VdwMod;
			dw.AddCondition(() => httpMode == Mode.MITM && !ctx.mitmHttp.started, ctx.CreateLog("MITM mode is set for http, but mitm service is not enabled!", VLogger.LogLevel.warning));
			dw.AddCondition(() => httpsMode == Mode.MITM && !ctx.mitmHttp.started, ctx.CreateLog("MITM mode is set for https, but mitm service is not enabled", VLogger.LogLevel.warning));
			dw.AddCondition(() => httpsMode == Mode.MITM && !ctx.CertMod.Started, ctx.CreateLog("MITM mode is set for https, but SSL Certification service is not started!", VLogger.LogLevel.warning));
			dw.AddCondition(() => ctx.mitmHttp.started && httpMode != Mode.MITM && httpsMode != Mode.MITM, ctx.CreateLog("MITM Service is running but no protocol modes set to MITM mode", VLogger.LogLevel.warning));
			if (autoClean)
			{
				_timer = new System.Windows.Forms.Timer();
				_timer.Tick += new EventHandler(AutoClean);
				_timer.Interval = 10 * 60 * 1000;
				_timer.Start();
			}
		}

		//Public methods

		public void Setup(string ipAddress, int portNumber, int pendingLimit)
		{
			ipv4Addr = ipAddress;
			port = portNumber;
			pclimit = pendingLimit;
		}

		public void StartServer()
		{
			server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			IPEndPoint ep = null;
			byte[] buffer = new byte[1024];
			if (ipv4Addr != "") ep = CreateEndPoint(ipv4Addr);
			if (ep != null)
			{
				started = true;
				server.Bind(ep);
				server.Listen(pclimit);
				server.BeginAccept(new AsyncCallback(AcceptClient), null);
			}
		}

		public void StopServer()
		{
			stopping = true;

			foreach (Socket s in clientList)
			{
				KillSocket(s, false);
			}

			ctx.LogMod.Log("Client shutdown ok", VLogger.LogLevel.information);

			clientList.Clear();

			if (started)
			{
				if (server.Connected) server.Shutdown(SocketShutdown.Both);
				server.Close();
				server.Dispose();
			}

			ctx.LogMod.Log("Server Stopped!", VLogger.LogLevel.information);

			stopping = false;
			started = false;
		}

		public void KillSocket(Socket client, bool autoRemove = true)
		{
			if (autoRemove && clientList != null) clientList.Remove(client);

			try
			{
				client.Shutdown(SocketShutdown.Both);
				client.Disconnect(false);
			}
			catch (Exception)
			{
				Console.WriteLine("graceful killsocket failed!");
			}
			client.Close();
			client.Dispose();
		}

		public void CleanSockets()
		{
			List<Socket> copy = ctx.ListCopy(clientList);
			bool result = true;
			foreach (Socket socket in copy)
			{
				try
				{
					KillSocket(socket);
				}
				catch (Exception)
				{
					console.Debug("Clean Sockets failed!");
					result = false;
				}
			}

			if (result)
			{
				ctx.LogMod.Log("All clients disconnected from server", VLogger.LogLevel.information);
			}
			else
			{
				ctx.LogMod.Log("Some clients failed to disconnect from server!", VLogger.LogLevel.warning);
			}

			Array.Clear(copy.ToArray(), 0, copy.Count);
		}

		public void SetMode(Mode mode, string protocol)
		{
			if (protocol == "http") httpMode = mode;
			if (protocol == "https") httpsMode = mode;
		}

		public Mode GetMode(string protocolName)
		{
			protocolName = protocolName.ToLower();
			return protocolName == "http" ? httpMode : protocolName == "https" ? httpsMode : Mode.Undefined;
		}

		public void PrintModes()
		{
			console.WriteLine("==Proxy Server Protocol Modes==");
			console.WriteLine("HTTP: " + ModeToString(httpMode));
			console.WriteLine("HTTPs: " + ModeToString(httpsMode));
			console.WriteLine("");
		}

		//Private methods

		private void AutoClean(object sender, EventArgs e) => CleanSockets();

		private void AcceptClient(IAsyncResult ar)
		{
			Socket client = null;
			try
			{
				client = server.EndAccept(ar);
			}
			catch (Exception)
			{
				return;
			}

			IPEndPoint client_ep = (IPEndPoint)client.RemoteEndPoint;
			string remoteAddress = client_ep.Address.ToString();
			string remotePort = client_ep.Port.ToString();

			//TODO: Implement block command -> keep the server and existing connections alive, but drop new connections

			var allow = autoAllow || console.ChoicePrompt($"\n[IN] Connection {remoteAddress}:{remotePort}\nDo you want to allow connection");
			if (allow)
			{
				clientList.Add(client);
				ReadObj obj = new ReadObj
				{
					buffer = new byte[1024],
					s = client
				};
				client.BeginReceive(obj.buffer, 0, obj.buffer.Length, SocketFlags.None, new AsyncCallback(ReadPackets), obj);
			}
			else
			{
				KillSocket(client, !stopping);
				ctx.LogMod.Log($"[REJECT] {remoteAddress}:{remotePort}", VLogger.LogLevel.information);
			}

			if (!stopping) server.BeginAccept(new AsyncCallback(AcceptClient), null);
		}

		private void ReadPackets(IAsyncResult ar)
		{
			ReadObj obj = (ReadObj)ar.AsyncState;
			Socket client = obj.s;
			byte[] buffer = obj.buffer;
			int read = -1;
			try
			{
				read = client.EndReceive(ar);
			}
			catch (Exception)
			{
				KillSocket(client, !stopping);
				ctx.LogMod.Log("[DISCONNECT] Client Disconnected from server", VLogger.LogLevel.information);
				return;
			}
			if (read == 0)
			{
				try { if (client.Connected) client.BeginReceive(obj.buffer, 0, obj.buffer.Length, SocketFlags.None, new AsyncCallback(ReadPackets), obj); }
				catch (Exception e)
				{
					KillSocket(client, !stopping);
					Console.WriteLine("Client aborted session!" + Environment.NewLine + e.Message);
				}
				return;
			}

			string text = Encoding.ASCII.GetString(buffer, 0, read);
			Request r;
			bool sslHandlerStarted = false;

			if (obj.request != null)
			{
				if (obj.request.notEnded)
				{
					string des = obj.request.full;
					des += text;
					r = new Request(des);
				}
				else r = new Request(text);
			}
			else r = new Request(text);

			if (!r.notEnded && !r.bogus)
			{
				ctx.LogMod.Log("<target> [HTTP]", VLogger.LogLevel.request, r);
				Tunnel t = new Tunnel(Tunnel.Mode.HTTP, httpMode, httpsMode, ctx, client, console);
				t.CreateMinimalTunnel(r);
				if (t.sslRead && httpMode == Mode.MITM) //Handle MITM SSL Connections
				{
					string host = t.GetHost();
					NetworkStream clientNS = new NetworkStream(client);
					VSslHandler vsh = new VSslHandler(ctx, console);
					VSslHandler.Error errCode = vsh.InitSslStream(clientNS, host);
					if (errCode != VSslHandler.Error.Success)
					{
						ctx.LogMod.Log("Init SSL Stream failed\r\nError Code: " + errCode.ToString(), VLogger.LogLevel.error);
					}
					else
					{
						sslHandlerStarted = true;
						vsh.InitAsyncRead();
						console.Debug("SSL Tunnel MITM Started");
						return;
					}
				}
				else if (t.sslRead && httpsMode == Mode.forward) //Handle HTTPS normal
				{
					t.InitHTTPS(client);
					return;
				}

				if (httpMode == Mode.MITM) //Handle HTTP MITM
				{
					Request httpSend = new Request(t.FormatRequest(r));
					Tunnel.Send("", Tunnel.Mode.HTTP, ctx, httpSend, new NetworkStream(client));
				}
				else if (httpMode == Mode.forward) //Handle HTTP normal
				{
					t.SendHTTP(r, client);
					return;
				}
			}
			else if (r.notEnded) obj.request = r;
			Array.Clear(buffer, 0, buffer.Length);
			try { if (client.Connected && !sslHandlerStarted) client.BeginReceive(obj.buffer, 0, obj.buffer.Length, SocketFlags.None, new AsyncCallback(ReadPackets), obj); }
			catch (Exception e)
			{
				KillSocket(client, !stopping);
				Console.WriteLine("Client aborted session!" + Environment.NewLine + e.Message);
			}
		}

		private IPEndPoint CreateEndPoint(string ep_addr) => ep_addr switch
		{
			"loopback" => new IPEndPoint(IPAddress.Loopback, port),
			"any" => new IPEndPoint(IPAddress.Any, port),
			"localhost" => new IPEndPoint(IPAddress.Parse("127.0.0.1"), port),
			_ => new IPEndPoint(IPAddress.Parse(ipv4Addr), port),
		};

		// Public static methods

		public static Mode StringToMode(string input)
		{
			input = input.ToLower();
			if (input == "mitm" || input == "man-in-the-middle") return Mode.MITM;
			else if (input == "forward" || input == "normal") return Mode.forward;
			return Mode.Undefined;
		}

		public static string ModeToString(Mode mode) => mode == Mode.forward ? "forward" : mode == Mode.MITM ? "mitm" : "undefined";
	}
}
