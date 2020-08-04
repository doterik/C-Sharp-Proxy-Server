using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using proxyServer.Interfaces;

namespace proxyServer
{
	public abstract class VBase : IHelp, IDisposable
	{
		// IDisposable Implementation

		internal bool disposed;
		internal readonly SafeFileHandle handle = new SafeFileHandle(IntPtr.Zero, true);
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		protected abstract void Dispose(bool disposing);

		// IHelp Implementation

		internal string _helpFile = string.Empty;
		public string HelpFile
		{
			get => _helpFile;
			set { if (File.Exists(value)) _helpFile = value; }
		}
	}
}
