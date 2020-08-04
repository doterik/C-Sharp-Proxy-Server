//#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable IDE0007 // Use implicit type

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using proxyServer.Interfaces;

namespace proxyServer
{
	public class VRegEx : VBase, IService, ISettings, IHelp, IDisposable
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
				_list.Clear();
				_list = null;
				logger = null;
			}
			disposed = true;
		}

		// IHelp Implementation

		//private string _helpFile = "";
		//public string HelpFile
		//{
		//	get => _helpFile;
		//	set { if (File.Exists(value)) _helpFile = value; }
		//}

		// ISettings Implementation

		public void LoadSettings(KeyValuePair<string, string> kvp)
		{
			string key = kvp.Key.ToLower();
			string value = kvp.Value.ToLower();

			if (key == "state") Started = value == "true";
			if (key == "def_name")
			{
				if (!_list.ContainsKey(kvp.Value))
				{
					_list.Add(kvp.Value, new RegList { list = new List<Regex>() });
				}
			}
			if (key.StartsWith("reg_name_"))
			{
				string entryName = kvp.Key.Substring(9);
				string entryValue = kvp.Value;
				if (!_list.ContainsKey(entryName))
				{
					_list.Add(entryName, new RegList { list = new List<Regex>() });
				}

				RegList current = _list[entryName];
				Regex expression = new Regex(entryValue);
				current.list.Add(expression);
				_list[entryName] = current;
			}
		}

		public void WriteSettings(System.Xml.XmlWriter xml)
		{
			xml.WriteStartElement("settings_start");

			xml.WriteElementString("state", Started ? "true" : "false");

			foreach (KeyValuePair<string, RegList> kvp in _list)
			{
				string name = kvp.Key;
				xml.WriteElementString("def_name", name);

				foreach (Regex reg in kvp.Value.list)
				{
					string value = reg.ToString();
					xml.WriteElementString($"reg_name_{name}", value);
				}
			}

			xml.WriteEndElement();
		}

		// IService Implementation
		public bool SelfInteractive { get; set; } = false;
		public bool Started { get; set; } = true;
		public string PRestore { get; set; } = "";
		public void WarningMessage() => logger.Log("Regex Manager Service is not started!", VLogger.LogLevel.warning);

		// Main RegEx Manager class
		private struct RegList
		{
			public List<Regex> list;
		}

		private Dictionary<string, RegList> _list = new Dictionary<string, RegList>();
		private VLogger logger;

		public VRegEx(VLogger log) => logger = log;

		public bool Add(string groupName)
		{
			if (_list.ContainsKey(groupName)) return false;

			_list.Add(groupName, new RegList { list = new List<Regex>() });

			return true;
		}

		public bool AddExpression(string groupName, string expression)
		{
			if (!_list.ContainsKey(groupName)) return false;
			RegList rl = _list[groupName];
			Regex rx = new Regex(expression);
			rl.list.Add(rx);
			_list[groupName] = rl;

			return true;
		}

		public bool RunAnd(string input, string group)
		{
			if (!_list.ContainsKey(group)) return false;

			foreach (Regex r in _list[group].list)
			{
				if (!r.Match(input).Success) return false;
			}

			return true;
		}

		public bool RunOr(string input, string group)
		{
			if (!_list.ContainsKey(group)) return false;

			foreach (Regex r in _list[group].list)
			{
				if (r.Match(input).Success) return true;
			}

			return false;
		}

		public bool Remove(string groupName)
		{
			if (!_list.ContainsKey(groupName)) return false;

			_list.Remove(groupName);

			return true;
		}

		public bool RemoveExpression(string groupName, string expression)
		{
			if (!_list.ContainsKey(groupName)) return false;

			RegList rl = _list[groupName];
			int index = 0;
			bool canRemove = false;

			foreach (Regex r in rl.list)
			{
				if (r.ToString() == expression)
				{
					canRemove = true;
					break;
				}

				index++;
			}

			if (canRemove) rl.list.RemoveAt(index);

			return true;
		}

		public bool IsRegexEmpty(string group) => group == null || !_list.ContainsKey(group) || _list[group].list.Count <= 0;

		public string ListExpressions(string group)
		{
			if (!_list.ContainsKey(group)) return null;
			if (_list[group].list == null) return null;

			string result = $"==Start of Regular Expressions List==\r\nCount: {_list[group].list.Count}\r\n";

			foreach (Regex rx in _list[group].list)
			{
				result += $"{rx}\r\n";
			}

			result += "==End of Regular Expressions List==\r\n";

			return result;
		}

		public string ListGroups()
		{
			string result = $"==Start of RegEx group list==\r\nCount: {_list.Keys.Count}\r\n";

			foreach (string s in _list.Keys)
			{
				result += $"{s}\r\n";
			}

			result += "==End fo RegEx group list==\r\n";

			return result;
		}
	}
}
