//#pragma warning disable CA1031 // Do not catch general exception types
//#pragma warning disable CA1051 // Do not declare visible instance fields
//#pragma warning disable CA1060 // Move pinvokes to native methods class
//#pragma warning disable CA1062 // Validate arguments of public methods
//#pragma warning disable CA1303 // Do not pass literals as localized parameters
//#pragma warning disable CA1304 // Specify CultureInfo
//#pragma warning disable CA1305 // Specify IFormatProvider
//#pragma warning disable CA1307 // Specify StringComparison
//#pragma warning disable CA1401 // P/Invokes should not be visible
//#pragma warning disable CA1707 // Identifiers should not contain underscores
//#pragma warning disable CA1717 // Only FlagsAttribute enums should have plural names
//#pragma warning disable CA1721 // Property names should not match get methods
//#pragma warning disable CA1815 // Override equals and operator equals on value types
//#pragma warning disable CA1822 // Member does not access instance data and can be marked as static !!
//#pragma warning disable CA2000 // Dispose objects before losing scope
//#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
//#pragma warning disable CA2211 // Non-constant fields should not be visible
//#pragma warning disable CA2227 // Collection properties should be read only
//#pragma warning disable CA3075 // Insecure DTD processing in XML
//#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
//#pragma warning disable IDE0003 // Remove qualification
#pragma warning disable IDE0007 // Use implicit type
//#pragma warning disable IDE0021 // Use expression body for constructors
//#pragma warning disable IDE0022 // Use expression body for methods
//#pragma warning disable IDE0025 // Use expression body for properties
//#pragma warning disable IDE0027 // Use expression body for accessors
//#pragma warning disable IDE0047 // Remove unnecessary parentheses
//#pragma warning disable IDE0049 // Simplify Names
//#pragma warning disable IDE0051 // Remove unused private members
//#pragma warning disable IDE0052 // Remove unread private members
//#pragma warning disable IDE0059 // Unnecessary assignment of a value
//#pragma warning disable IDE0063 // Use simple 'using' statement
//#pragma warning disable IDE1006 // Naming Styles

using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using Microsoft.Win32.SafeHandles;
using proxyServer.Interfaces;

namespace proxyServer
{
	#region Classes


	public class Response : IDisposable, IFilter
	{
		//IDisposable Implementation

		bool disposed = false;
		SafeFileHandle handle = new SafeFileHandle(IntPtr.Zero, true);

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				handle.Dispose();
				filterNames.Clear();
				filterNames = null;
				_vfmanager = null;
				FullText = null;
				Array.Clear(FullBytes, 0, FullBytes.Length);
				FullBytes = null;
				version = null;
				statusCode = 0;
				httpMessage = null;
				headers.Clear();
				headers = null;
				Array.Clear(body, 0, body.Length);
				bodyText = null;
				console = null;
			}

			disposed = true;
		}

		//IFilter Implementation

		private Dictionary<string, object> filterNames = new Dictionary<string, object>();
		private VFilter _vfmanager;

		public Dictionary<string, object> FilterName
		{
			get { return filterNames; }
			set { filterNames = value; }
		}

		public VFilter Manager
		{
			get { return _vfmanager; }
			set { _vfmanager = value; }
		}

		public string PushBindInfo()
		{
			string info = "";

			foreach (KeyValuePair<string, object> kvp in filterNames)
			{
				string part2 = kvp.Value.ToString();
				info += kvp.Key + ":" + part2 + ";";
			}

			if (info.Length > 0) info = info.Substring(0, info.Length - 1);

			return info;
		}

		public void PullBindInfo(string info)
		{
			if (info == "") return;
			string[] kvp = info.Split(';');
			foreach (string pairs in kvp)
			{
				string[] kvp2 = pairs.Split(':');
				string level = kvp2[1].ToString();
				string name = kvp2[0];
				filterNames.Add(name, level);
			}
		}

		public bool BindFilter(string validFilterName, object input)
		{
			string op = (string)input;
			if (op != "mime_white_list" && op != "mime_skip_list") return false;
			filterNames.Add(validFilterName, op);
			return true;
		}

		public bool SearchFilter(string sMethod, object searchParam, string input)
		{
			string p = (string)searchParam;
			string targetFilterName = "";
			foreach (KeyValuePair<string, object> pair in filterNames)
			{
				string comp = (string)pair.Value;
				if (comp == p)
				{
					targetFilterName = pair.Key;
					break;
				}
			}

			if (targetFilterName == "")
			{
				return true; // if target filter is not found output the text, perhaps there is no filter for a specific object
			}

			if (sMethod == "and") return Manager.RunAllCompareAnd(targetFilterName, input);
			else if (sMethod == "or") return Manager.RunAllCompareOr(targetFilterName, input);
			else
			{
				console.WriteLine("[ERROR] Invalid SearchFilter option sMethod", console.GetIntercativeGroup());
				return true;
			}
		}

		public bool UnBindFilter(string validFilterName)
		{
			if (!FilterName.ContainsKey(validFilterName)) return false;
			FilterName.Remove(validFilterName);
			return true;
		}

		public void BindList()
		{
			WriteLine("=========Start Of bind list=========");
			foreach (KeyValuePair<string, object> kvp in filterNames)
			{
				string ll = (string)kvp.Value;
				WriteLine($"{kvp.Key}:\t{ll}");
			}
			WriteLine("==========End Of bind list==========");
		}

		public void SetManager(VFilter fman) => Manager = fman;

		//Main response parser class

		public string FullText { get; private set; } = "";
		public byte[] FullBytes { get; private set; }
		public string version = "";
		public int statusCode = 0;
		public string httpMessage = "";
		public VDictionary headers = new VDictionary();
		public byte[] body = new byte[2048];
		public string bodyText = "";
		private VConsole console;
		public bool notEnded = false;
		public bool bogus = false;
		public bool skip = false;
		private VMitm mitm;

		public Response(int _statusCode, string _httpMessage, string _version, VDictionary _headers, string _body, byte[] fullBytes, VConsole con, VMitm mitmHttp)
		{
			statusCode = _statusCode;
			httpMessage = _httpMessage;
			version = _version;
			bodyText = _body;
			body = fullBytes;
			console = con;
			mitm = mitmHttp;
			headers = _headers;
		}

		public void CheckMimeAndSetBody()
		{
			if (headers.ContainsKey("Content-Length") && headers["Content-Length"] == "0") return;
			if (!headers.ContainsKey("Content-Type"))
			{
				body = new byte[0];
				return;
			}

			if (headers.ContainsKey("Content-Type"))
			{
				string cType = headers["Content-Type"];
				if (cType.Contains(";")) cType = cType.Substring(0, cType.IndexOf(';'));

				if (SearchFilter("or", "mime_skip_list", cType))
				{
					skip = true;
					bodyText = "";
				}

				if (!SearchFilter("or", "mime_white_list", cType))
				{
					bodyText = "";
				}
			}

			DecodeArray();
		}

		public void WriteLine(string text)
		{
			console.WriteLine(text, "ig.null"); // this class doesn't have interactive mode, so always write to ig.null
		}

		private void DecodeArray()
		{
			notEnded = false;
			string cType = headers["Content-Type"];
			if (cType.Contains(";")) cType = cType.Substring(0, cType.IndexOf(';'));
			VDecoder vd = new VDecoder();
			bool isConvertable = false;
			if (filterNames.Count > 0)
			{
				isConvertable = SearchFilter("or", "mime_white_list", cType);
			}
			if (isConvertable && !headers.ContainsKey("Content-Encoding"))
			{
				bodyText = vd.DecodeCharset(headers["Content-Type"], body, body.Length);
			}
			else if (isConvertable && headers.ContainsKey("Content-Encoding"))
			{
				string enc = headers["Content-Encoding"];
				if (enc == "gzip") body = vd.DecodeGzipToBytes(body);
				else if (enc == "deflate") body = vd.DecodeDeflate(body);
				else if (enc == "br") body = vd.DecodeBrotli(body);

				bodyText = vd.DecodeCharset(headers["Content-Type"], body, body.Length);
				//IMPORTANT: Use push end -- the data is converted to text correctly
			}
			else if (!isConvertable && headers.ContainsKey("Content-Encoding"))
			{

				//Decode contents to byte array
				string enc = headers["Content-Encoding"];
				if (enc == "gzip") body = vd.DecodeGzipToBytes(body);
				else if (enc == "deflate") body = vd.DecodeDeflate(body);
				else if (enc == "br") body = vd.DecodeBrotli(body);
			}
			else
			{
				//Data is in clearText, not convertable to printable (text) format for ex. image file, exe file
				bodyText = "";
			}
		}

		public void Deserialize(NetworkStream ns, Request req, VSslHandler vsh = null)
		{
			string sResult = version + " " + statusCode + " " + httpMessage + "\r\n";
			int ctLength = 0;

			//edit bodyText here

			VDecoder vd = new VDecoder();

			if (headers.ContainsKey("Content-Length") && headers["Content-Length"] != "0" && headers["Content-Length"] != null)
			{
				if (mitm != null && mitm.started) //MITM Media and Text injection
				{
					if (bodyText != "")
					{
						if (mitm.CheckBody(bodyText)) return;
						string cType = (headers.ContainsKey("Content-Type")) ? headers["Content-Type"] : null;
						if (cType != null)
						{
							string nt = "";
							nt = mitm.Inject(bodyText, headers["Content-Type"]);
							if (nt != null) bodyText = nt;
						}
					}
					else
					{
						byte[] n = mitm.MediaRewrite(this, req);
						if (n != null) body = n;
					}
				}

				if (bodyText != "" && headers.ContainsKey("Content-Encoding"))
				{
					Array.Clear(body, 0, body.Length);
					byte[] toCode = vd.EncodeCharset(headers["Content-Type"], bodyText);
					string enc = headers["Content-Encoding"];
					if (enc == "gzip") body = vd.EncodeGzip(toCode);
					else if (enc == "deflate") body = vd.EncodeDeflate(toCode);
					else if (enc == "br") body = vd.EncodeBrotli(toCode);
					Array.Clear(toCode, 0, toCode.Length);
				}
				else if (bodyText == "" && headers.ContainsKey("Content-Encoding"))
				{
					string enc = headers["Content-Encoding"];
					if (enc == "gzip") body = vd.EncodeGzip(body);
					else if (enc == "deflate") body = vd.EncodeDeflate(body);
					else if (enc == "br") body = vd.EncodeBrotli(body);
				}
				else if (bodyText != "" && !headers.ContainsKey("Content-Encoding"))
				{
					body = vd.EncodeCharset(headers["Content-Type"], bodyText);
				}

				ctLength = body.Length;
			}

			foreach (KeyValuePair<string, string> kvp in headers.Items)
			{
				string line = "";
				if (kvp.Key == "Content-Length" && ctLength > 0) line = $"Content-Length: {ctLength}\r\n";
				else if (kvp.Key == "Transfer-Encoding" && kvp.Value == "chunked" && ctLength > 0)
				{
					// insert the content-length and skip the transfer-encoding header, because we concatanated it.
					line = "Content-Length: " + ctLength.ToString() + "\r\n";
				}
				else line = kvp.Key + ": " + kvp.Value + "\r\n";

				sResult += line;
			}

			//console.Debug($"{req.target} - responded with content-type: {headers["Content-Type"]}");

			sResult += "\r\n";
			byte[] text = Encoding.ASCII.GetBytes(sResult);
			if (vsh == null)
			{
				ns.Write(text, 0, text.Length);
				if (ctLength > 0) ns.Write(body, 0, body.Length);
				ns.Flush();
			}
			else
			{
				//console.Debug("Handler " + vsh.HandlerID + " receiving " + (headers.ContainsKey("Content-Type") ? headers["Content-Type"] : "No content type sent"));
				vsh.WriteSslStream(text);
				if (ctLength > 0) vsh.WriteSslStream(body);
				vsh.FlushSslStream();
			}
		}
	}

	#endregion
}
