//#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE0063 // Use simple 'using' statement
#pragma warning disable IDE1006 // Naming Styles

using System;
using System.Collections.Generic;
using System.IO;
using proxyServer.Interfaces;

namespace proxyServer
{
	public class VSettings : VBase, IDisposable
	{
		// IDisposable Implementation
		protected override void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				handle.Dispose();
				defaultDir = null;
				fileLocation = null;
				console = null;
				ctx = null;
				pinManager = null;
				logger = null;
				if (objlist != null) Array.Clear(objlist, 0, objlist.Length);
				objlist = null;
			}
			disposed = true;
		}

		string defaultDir;
		string fileLocation;
		VConsole console;
		Form1 ctx;
		VPin pinManager;
		VLogger logger;
		object[] objlist;

		public VSettings(Form1 context, VConsole con, VPin pm, VLogger log)
		{
			ctx = context;
			console = con;
			pinManager = pm;
			logger = log;
		}

		public void DefineDirectory(string dir)
		{
			defaultDir = dir;
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
		}

		public void SetupObjects(params object[] arg) => objlist = arg;

		public void FindFile(string file)
		{
			foreach (string entry in Directory.GetFiles(defaultDir))
			{
				if (new FileInfo(entry).Name == file + ".xml")
				{
					fileLocation = entry;
				}
			}
		}

		public string GetFileLocation() => fileLocation;

		public void Load()
		{
			if (!File.Exists(fileLocation))
			{
				ctx.LogMod.Log($"File Not Found: {fileLocation}\r\n\tSetting not loaded!", VLogger.LogLevel.error);
				return;
			}

			using (System.Xml.XmlReader xml = System.Xml.XmlReader.Create(fileLocation))
			{
				List<KeyValuePair<string, string>> lkvp = new List<KeyValuePair<string, string>>();
				bool appendMode = false;
				int objListPointer = 0;

				while (xml.Read())
				{
					string elementName = xml.Name;

					if (xml.IsEmptyElement) continue;

					if (xml.IsStartElement())
					{
						if (appendMode)
						{
							string cElement = elementName;
							xml.Read();
							string nValue = xml.Value;

							while (nValue == "")
							{
								lkvp.Add(new KeyValuePair<string, string>(cElement, ""));
								cElement = xml.Name;
								xml.Read();
								nValue = xml.Value;
							}

							KeyValuePair<string, string> current = new KeyValuePair<string, string>(cElement, nValue);
							lkvp.Add(current);
						}

						switch (elementName)
						{
							case "settings_start":
								appendMode = true;
								lkvp.Clear();
								break;
						}
					}
					else
					{
						// Ending elements can be handled here

						if (elementName == "settings_start")
						{
							appendMode = false;
							ISettings currentObj = (ISettings)objlist[objListPointer];
							foreach (KeyValuePair<string, string> kvp in lkvp)
							{
								currentObj.LoadSettings(kvp);
							}

							lkvp.Clear();
							objListPointer++;
						}
					}
				}
			}
		}

		public void Save(string filename)
		{
			if (File.Exists(defaultDir + "\\" + filename + ".xml")) // TODO: concat dir
			{
				bool result = console.ChoicePrompt("The file name you specified already exists.\r\nDo you want to overwrite it?");
				if (result) File.Delete(defaultDir + "\\" + filename + ".xml");
				else return;
			}

			using (System.Xml.XmlWriter xml = System.Xml.XmlWriter.Create(defaultDir + "\\" + filename + ".xml"))
			{
				xml.WriteStartDocument();
				xml.WriteStartElement("proxyServer");
				foreach (object obj in objlist)
				{
					ISettings iso = (ISettings)obj;
					iso.WriteSettings(xml);
				}
				/*
				//Write Main Options (Form1)
				ctx.WriteSettings(xml);
				//Write Console Options (VConsole)
				console.WriteSettings(xml);
				//Write Pin Options (VPin)
				pinManager.WriteSettings(xml);
				if (ctx.server != null) ctx.server.WriteSettings(xml);
				//Write Filters (VFilter)
				ctx.vf.WriteSettings(xml);
				//Write Logger Options (VLogger)
				logger.WriteSettings(xml);
				//Write SSL Certificate Options (VSslCertificate)
				ctx.CertMod.WriteSettings(xml);
				//Write ending tags
				*/
				xml.WriteEndElement();
				xml.WriteEndDocument();
			}

			ctx.LogMod.Log("Settings Saved to: " + defaultDir + "\\" + filename + ".xml", VLogger.LogLevel.information);
		}

		private void CreateServer()
		{
			if (ctx.server == null) ctx.server = new ProxyServer(ctx.ip, ctx.port, ctx.pendingConnectionLimit, console, ctx);
		}
	}
}
