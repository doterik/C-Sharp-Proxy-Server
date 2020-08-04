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
//#pragma warning disable IDE0059 // Unnecessary assignment of a value
//#pragma warning disable IDE0063 // Use simple 'using' statement
//#pragma warning disable IDE1006 // Naming Styles

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32.SafeHandles;
using System.IO;
using proxyServer.Interfaces;

namespace proxyServer
{
	public class VDump : VBase, IFilter, IService, ISettings, IHelp, IDisposable
	{
		// IDisposable Implementation
		protected override void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				handle.Dispose();
				_helpFile = null;
				PRestore = null;
				filterNames = null;
				_vfmanager = null;
				ctx = null;
				console = null;
				logger = null;
				dumpFiles.Clear();
				fName.Clear();
				Dir = null;
			}
			disposed = true;
		}

		// ISettings implementation

		public void LoadSettings(KeyValuePair<string, string> kvp)
		{
			string key = kvp.Key.ToLower();
			string value = kvp.Value.ToLower();

			if (key == "state") Started = (value == "true") ? true : false;
			if (key == "dumper_file") dumpFiles.Add(kvp.Value);
			if (key == "dumper_fname") fName.Add(kvp.Value);
		}

		public void WriteSettings(System.Xml.XmlWriter xml)
		{
			xml.WriteStartElement("settings_start");
			xml.WriteStartElement("dumper");
			xml.WriteElementString("state", Started ? "true" : "false");

			foreach (string file in dumpFiles)
			{
				xml.WriteElementString("dumper_file", file);
			}

			foreach (string fn in fName)
			{
				xml.WriteElementString("dumper_fname", fn);
			}
			xml.WriteEndElement();
			xml.WriteEndElement();
		}

		// IService implementation
		public bool Started { get; set; } = false;
		public bool SelfInteractive { get; set; } = false;
		public string PRestore { get; set; } = "";

		public void WarningMessage() => logger.Log("Service Dump is not started", VLogger.LogLevel.warning);

		//IFilter implementation

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
				info += $"{kvp.Key}:{part2};";
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
				int level = int.Parse(kvp2[1]);
				string name = kvp2[0];
				filterNames.Add(name, level);
			}
		}

		public bool BindFilter(string validFilterName, object input)
		{
			int op = (int)input;
			if (dumpFiles.Count < op) return false;
			filterNames.Add(validFilterName, op);
			return true;
		}

		public bool SearchFilter(string sMethod, object searchParam, string input)
		{
			int p = (int)searchParam;
			string targetFilterName = "";
			foreach (KeyValuePair<string, object> pair in filterNames)
			{
				int comp = (int)pair.Value;
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
			console.WriteLine("=========Start Of bind list=========");
			foreach (KeyValuePair<string, object> kvp in filterNames)
			{
				int ll = (int)kvp.Value;
				console.WriteLine($"{kvp.Key}:\t{dumpFiles[ll]}");
			}
			console.WriteLine("==========End Of bind list==========");
		}

		public void SetManager(VFilter fman) => Manager = fman;

		//Main Dumper Class

		private Form1 ctx;
		private VConsole console;
		private VLogger logger;
		private List<string> dumpFiles;
		private List<string> fName;
		private bool disposedValue;

		public string Dir { get; private set; } = "";

		public VDump(Form1 context, VConsole con, VLogger log)
		{
			ctx = context;
			console = con;
			logger = log;
			dumpFiles = new List<string>();
			fName = new List<string>();
		}

		public void ListDumpers()
		{
			console.WriteLine("==Dump Manager Dump Files List==", console.GetIntercativeGroup());
			if (Dir != "") console.WriteLine($"Relative directory: {Dir}", console.GetIntercativeGroup());

			foreach (string file in dumpFiles)
			{
				int index = GetFileIndex(file);
				string fname = "";
				foreach (string entry in fName)
				{
					if (GetFId(entry) == index) fname = GetFName(entry);
				}

				string output = file;
				if (Dir != "") output = new FileInfo(file).Name;
				if (fname != "") output += $" - {fname}";

				console.WriteLine(output, console.GetIntercativeGroup());
			}

			console.WriteLine("==End of Dump Files list==", console.GetIntercativeGroup());
		}

		public void DefineDirectory(string dir)
		{
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
			Dir = dir;
		}

		public void AddFile(string fileName, string friendlyName = null, bool useQuestion = true)
		{
			if (Dir != "") fileName = Dir + "\\" + fileName;

			if (!File.Exists(fileName))
			{
				File.Create(fileName).Close();
				dumpFiles.Add(fileName);
				if (fName != null)
				{
					int lstIndex = dumpFiles.Count - 1;
					string entry = friendlyName + ":" + lstIndex;
					fName.Add(entry);
				}
			}
			else
			{
				bool overwrite = false;

				if (useQuestion)
				{
					string p = console.GetPrompt();
					console.SetPrompt("[Y/N]");
					overwrite = console.ChoicePrompt("File already exists.\r\nDo you want to override it? [Y/N]");
					console.SetPrompt(p);
				}

				if (overwrite)
				{
					File.Delete(fileName);
					AddFile(fileName, friendlyName, useQuestion);
				}
				else
				{
					dumpFiles.Add(fileName);
					if (fName != null)
					{
						int lstIndex = dumpFiles.Count - 1;
						string entry = friendlyName + ":" + lstIndex;
						fName.Add(entry);
					}
				}
			}
		}

		public void AssignFriendlyName(string fileName, string friendlyName)
		{
			if (Dir != "") fileName = Dir + "\\" + fileName;
			int index = GetFileIndex(fileName);
			string entry = friendlyName + ":" + index.ToString();
			fName.Add(entry);
		}

		public void RemoveFriendlyName(string friendlyName)
		{
			int currentIndex = 0;
			foreach (string fn in fName)
			{
				if (GetFName(fn) == friendlyName) break;

				currentIndex++;
			}

			fName.RemoveAt(currentIndex);
		}

		public void Dump(string text, string friendlyName)
		{
			if (dumpFiles.Count <= 0) return;
			int index = -1;
			foreach (string fn in fName)
			{
				if (GetFName(fn) == friendlyName)
				{
					index = GetFId(fn);
					break;
				}
			}

			if (index != -1) LDump(text, dumpFiles[index]);
		}

		public void Dump(string text)
		{
			if (dumpFiles.Count <= 0) return;
			LDump(text, dumpFiles[0]);
		}

		public void Dump(string text, int filePathId)
		{
			if (dumpFiles.Count <= 0) return;
			LDump(text, dumpFiles[filePathId]);
		}

		public int GetIndexByFilePath(string fp)
		{
			if (Dir != "") fp = Dir + "\\" + fp;
			return GetFileIndex(fp);
		}

		public int GetIndexByFriendlyName(string fn)
		{
			foreach (string f in fName)
			{
				if (GetFName(f) == fn)
				{
					return GetFId(f);
				}
			}

			return -1;
		}

		public void RemoveFile(int fp)
		{
			if (dumpFiles.Count < fp)
			{
				int counter = 0;
				foreach (string fn in fName)
				{
					if (GetFId(fn) == fp) break;
					counter++;
				}

				fName.RemoveAt(counter); // remove friendly name too
				dumpFiles.RemoveAt(fp);
			}
		}

		public bool CheckFileByFriendlyName(string FriendlyName)
		{
			foreach (string fn in fName)
			{
				if (GetFName(fn) == FriendlyName)
				{
					int fIndex = GetFId(fn);
					return CheckFileByPath(dumpFiles[fIndex]);
				}
			}

			return false;
		}

		public bool CheckFileByPath(string filePath)
		{
			if (dumpFiles.Contains(filePath)) return true;
			return false;
		}

		//Private methods

		private void LDump(string text, string lFile)
		{
			if (!Started) return;
			int findex = GetFileIndex(lFile);
			if (filterNames.Count > 0)
			{
				if (SearchFilter("or", findex, text)) return;
			}
			string old = File.ReadAllText(lFile);
			string nl = Environment.NewLine;
			string n = "";
			if (old == "") n = text;
			else n = old + nl + text;
			File.WriteAllText(lFile, n);
		}

		private int GetFileIndex(string fileName)
		{
			int index = 0;
			foreach (string f in dumpFiles)
			{
				if (f == fileName) return index;
				index++;
			}

			return -1;
		}

		private string GetFName(string input) => input.Contains(':') ? input.Split(':')[0] : null;

		private int GetFId(string input)
		{
			if (input.Contains(':'))
			{
				string text = input.Split(':')[1];
				int.TryParse(text, out int id);
				return id;
			}
			else return -1;
		}
	}
}
