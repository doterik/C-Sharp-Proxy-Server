//#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE1006 // Naming Styles

using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace proxyServer
{
	public class VSslHandler : VBase, IDisposable
	{
		// IDisposable Implementation
		protected override void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				handle.Dispose();
				ctx = null;
				certman = null;
				Close();
				_ssl = null;
				Array.Clear(buffer, 0, buffer.Length);
				buffer = null;
				console = null;
			}
			disposed = true;
		}

		private Form1 ctx;
		private VSslCertification certman;
		private SslStream _ssl;
		private byte[] buffer = new byte[2048];
		private VConsole console;

		public VSslHandler(Form1 context, VConsole con)
		{
			ctx = context;
			console = con;
		}

		public enum Error
		{
			CertificateManagerNotAvailable,
			Success,
			CertAutoGenerationFailed,
			CertRetrieveFailed,
			SslProtocolRetrieveFailed,
			SslServerAuthFailed,
			SslStreamCantWrite,
			SslStreamWriteFailed,
			SslStreamDisposed
		}

		public Error InitSslStream(NetworkStream ns, string targetHost)
		{
			var ssl = new SslStream(ns);
			certman = ctx.CertMod;
			if (certman == null || !certman.Started) return Error.CertificateManagerNotAvailable;
			X509Certificate2 cert = certman.GetCert(targetHost);
			if (cert == null) certman.BCGenerateCertificate(targetHost);
			cert = certman.GetCert(targetHost);
			if (cert == null) return Error.CertRetrieveFailed;
			SslProtocols sp = certman.GetProtocols();
			if (sp == SslProtocols.None) return Error.SslProtocolRetrieveFailed;
			try
			{
				ssl.AuthenticateAsServer(cert, false, sp, true);
				_ssl = ssl;
				return Error.Success;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				ctx.LogMod.Log($"SSL Server Init Error:\r\n{ex}", VLogger.LogLevel.error);
				return Error.SslServerAuthFailed;
			}
		}

		public void InitAsyncRead() => _ssl.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(ReadFromStream)
				, new ReadObj
				{
					full = "",
					r = null,
					requestHandled = false
				});

		public Error WriteSslStream(byte[] data)
		{
			if (_ssl == null) return Error.SslStreamDisposed;
			if (!_ssl.CanWrite) return Error.SslStreamCantWrite;

			try { _ssl.Write(data, 0, data.Length); }
			catch (Exception) { return Error.SslStreamWriteFailed; } 

			return Error.Success;
		}

		public void FlushSslStream() => _ssl.Flush();

		public Error Close()
		{
			if (_ssl == null) return Error.SslStreamDisposed;
			_ssl.Close();
			_ssl.Dispose();
			return Error.Success;
		}

		struct ReadObj
		{
			public string full;
			public Request r;
			public bool requestHandled;
		}

		private void ReadFromStream(IAsyncResult ar)
		{
			ReadObj ro = (ReadObj)ar.AsyncState;
			Request r = ro.r;
			int bytesRead = 0;
			try { bytesRead = _ssl.EndRead(ar); }
			catch (Exception) { return; }
			var read = new byte[bytesRead];
			Array.Copy(buffer, read, bytesRead);
			var text = Encoding.ASCII.GetString(read);

			if (bytesRead > 0)
			{
				if (r == null) r = new Request(text, true);

				if (r.notEnded)
				{
					if (ro.full == "") ro.full = text;
					else
					{
						ro.full += text;
						r = new Request(ro.full, true);
					}
				}

				if (!r.notEnded && !r.bogus)
				{
					if (ctx.mitmHttp.started) ctx.mitmHttp.DumpRequest(r);

					var requestString = r.Deserialize();

					Tunnel.Send(requestString, Tunnel.Mode.HTTPs, ctx, r, null, this);
					ro.full = "";
					ro.requestHandled = true;
				}
			}

			Array.Clear(buffer, 0, buffer.Length);
			if (!ro.requestHandled) ro.r = r;
			else
			{
				ro.r = null;
				ro.requestHandled = false;
			}
			try { _ssl.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(ReadFromStream), ro); }
			catch (Exception ex)
			{
				//ctx.LogMod.Log("Ssl stream error MITM\r\n" + ex.Message, VLogger.LogLevel.error);
				Console.WriteLine($"St: {ex.StackTrace}");
			}
		}
	}
}
