//#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE1006 // Naming Styles

using System;
using System.Collections.Generic;
using proxyServer.Interfaces;

namespace proxyServer
{
	public class VFilter : VBase, ISettings, IHelp, IDisposable
	{
		// Implement IDisposable
		protected override void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				handle.Dispose();

				ResetAllFilter();
				console = null;
				logger = null;
				filters = null;
				ctx = null;
			}
			disposed = true;
		}

		// Implement IHelp

		//private string _helpFile = "";

		//public string HelpFile
		//{
		//	get { return _helpFile; }
		//	set
		//	{
		//		if (File.Exists(value)) _helpFile = value;
		//	}
		//}

		// Implement ISettings

		private string _lfnameCache = "";
		private Operation _lopCache = Operation.Undefined;

		public void LoadSettings(KeyValuePair<string, string> kvp)
		{
			string key = kvp.Key.ToLower();
			string value = kvp.Value.ToLower();

			if (key == "filter_state") started = value == "true";
			if (key == "f_name")
			{
				_lfnameCache = kvp.Value;
				CreateFilter(kvp.Value);
			}

			if (key == "f_equal") _lopCache = Operation.Equals;
			if (key == "f_starts_with") _lopCache = Operation.StartsWith;
			if (key == "f_contains") _lopCache = Operation.Contains;
			if (key == "f_not_equal") _lopCache = Operation.NotEquals;
			if (key == "f_rule")
			{
				Addfilter(_lfnameCache, _lopCache, kvp.Value);
			}
		}

		public void WriteSettings(System.Xml.XmlWriter xml)
		{
			xml.WriteStartElement("settings_start");
			xml.WriteElementString("filter_state", started ? "true" : "false");
			GetSettings(xml);
			xml.WriteEndElement();
		}

		// Main VFilter class

		Form1 ctx;
		VConsole console;
		VLogger logger;
		public string pRestore;
		public bool selfInteractive;
		public bool started;

		Dictionary<string, Filter> filters = new Dictionary<string, Filter>();

		public struct Filter
		{
			public List<string> equalFilter;
			public List<string> startsWithFilter;
			public List<string> notEqualFilter;
			public List<string> containsFilter;
		}

		public enum Operation : int
		{
			Equals = 0,
			StartsWith = 1,
			NotEquals = 2,
			Contains = 3,
			Undefined = 4
		}

		public VFilter(Form1 context, VConsole conmod)
		{
			ctx = context;
			logger = ctx.LogMod;
			console = conmod;
		}

		public bool IsFilterEmpty(string filterName)
		{
			if (filterName == null) return true;
			if (!filters.ContainsKey(filterName)) return true;

			Filter current = filters[filterName];
			return current.containsFilter.Count == 0 && current.startsWithFilter.Count == 0
				&& current.equalFilter.Count == 0 && current.notEqualFilter.Count == 0;
		}

		public bool CreateFilter(string filterName)
		{
			if (filters.ContainsKey(filterName)) return false;

			Filter f = new Filter
			{
				equalFilter = new List<string>(),
				startsWithFilter = new List<string>(),
				notEqualFilter = new List<string>(),
				containsFilter = new List<string>()
			};

			filters.Add(filterName, f);

			return true;
		}

		public bool Addfilter(string filterName, Operation operation, string value)
		{
			if (!filters.ContainsKey(filterName)) return false;

			Filter currentFilter = filters[filterName];

			if (operation == Operation.Equals) currentFilter.equalFilter.Add(value);
			if (operation == Operation.StartsWith) currentFilter.startsWithFilter.Add(value);
			if (operation == Operation.NotEquals) currentFilter.notEqualFilter.Add(value);
			if (operation == Operation.Contains) currentFilter.containsFilter.Add(value);

			filters[filterName] = currentFilter;

			return true;
		}

		public bool RemoveFilter(string filterName, Operation operation, string value)
		{
			if (!filters.ContainsKey(filterName)) return false;

			Filter currentFilter = filters[filterName];

			if (operation == Operation.Equals) currentFilter.equalFilter.Remove(value);
			if (operation == Operation.StartsWith) currentFilter.startsWithFilter.Remove(value);
			if (operation == Operation.NotEquals) currentFilter.notEqualFilter.Remove(value);
			if (operation == Operation.Contains) currentFilter.containsFilter.Remove(value);

			filters[filterName] = currentFilter;

			return true;
		}

		public bool DestroyFilter(string filterName)
		{
			if (!filters.ContainsKey(filterName)) return false;

			filters.Remove(filterName);

			return true;
		}

		public bool RunEqualCompare(string filterName, string inputValue, out bool isListEmpty)
		{
			isListEmpty = false;
			if (!filters.ContainsKey(filterName)) return false;
			Filter current = filters[filterName];
			if (current.equalFilter.Count == 0) isListEmpty = true;
			foreach (string entry in current.equalFilter)
			{
				if (entry == inputValue) return true;
			}

			return false;
		}

		public bool RunNotEqualCompare(string filterName, string inputValue, out bool isListEmpty)
		{
			isListEmpty = false;
			if (!filters.ContainsKey(filterName)) return false;

			Filter current = filters[filterName];
			if (current.notEqualFilter.Count == 0) isListEmpty = true;
			foreach (string entry in current.notEqualFilter)
			{
				if (entry != inputValue) return true;
			}

			return false;
		}

		public bool RunStartsWithCompare(string filterName, string inputValue, out bool isListEmpty)
		{
			isListEmpty = false;
			if (!filters.ContainsKey(filterName)) return false;
			Filter current = filters[filterName];
			if (current.startsWithFilter.Count == 0) isListEmpty = true;
			foreach (string entry in current.startsWithFilter)
			{
				if (inputValue.StartsWith(entry)) return true;
			}

			return false;
		}

		public bool RunContainsCompare(string filterName, string inputValue, out bool isListEmpty)
		{
			isListEmpty = false;
			if (!filters.ContainsKey(filterName)) return false;
			Filter current = filters[filterName];
			if (current.containsFilter.Count == 0) isListEmpty = true;
			foreach (string entry in current.containsFilter)
			{
				if (inputValue.Contains(entry)) return true;
			}

			return false;
		}

		public bool RunAllCompareAnd(string filterName, string inputValue)
		{
			bool r1 = RunEqualCompare(filterName, inputValue, out bool i1);
			bool r2 = RunNotEqualCompare(filterName, inputValue, out bool i2);
			bool r3 = RunStartsWithCompare(filterName, inputValue, out bool i3);
			bool r4 = RunContainsCompare(filterName, inputValue, out bool i4);

			if (i1) r1 = true;
			if (i2) r2 = true;
			if (i3) r3 = true;
			if (i4) r4 = true;

			return r1 && r2 && r3 && r4;
		}

		public bool RunAllCompareOr(string filterName, string inputValue)
			=> RunEqualCompare(filterName, inputValue, out _)
			|| RunNotEqualCompare(filterName, inputValue, out _)
			|| RunStartsWithCompare(filterName, inputValue, out _)
			|| RunContainsCompare(filterName, inputValue, out _);

		public void ResetAllFilter() => filters.Clear();

		public void PrintFilter()
		{
			WriteLine("===========Start of filter list===========");
			WriteLine($"Total {filters.Count} filters");

			foreach (KeyValuePair<string, Filter> kvp in filters)
			{
				WriteLine(kvp.Key);
			}

			WriteLine("============End of filter list============");
		}

		public void PrintRules(string fName)
		{
			if (!filters.ContainsKey(fName)) return;

			Filter current = filters[fName];

			WriteLine("Equal Rules:");
			foreach (string key in current.equalFilter) WriteLine($"\t{key}");

			WriteLine("Not Equal Rules:");
			foreach (string key in current.notEqualFilter) WriteLine($"\t{key}");

			WriteLine("Starts With Rules:");
			foreach (string key in current.startsWithFilter) WriteLine($"\t{key}");

			WriteLine("Contains Rules:");
			foreach (string key in current.containsFilter) WriteLine($"\t{key}");
		}

		public void GetSettings(System.Xml.XmlWriter xml)
		{
			foreach (KeyValuePair<string, Filter> kvp in filters)
			{
				// Write Start tag for new filter element with name
				xml.WriteElementString("f_name", kvp.Key);

				// Write Start tag for rules of the Filter object
				xml.WriteStartElement("f_equal");
				foreach (string rule in kvp.Value.equalFilter) xml.WriteElementString("f_rule", rule);

				xml.WriteEndElement();

				xml.WriteStartElement("f_not_equal");
				foreach (string rule in kvp.Value.notEqualFilter) xml.WriteElementString("f_rule", rule);

				xml.WriteEndElement();

				xml.WriteStartElement("f_starts_with");
				foreach (string rule in kvp.Value.startsWithFilter) xml.WriteElementString("f_rule", rule);

				xml.WriteEndElement();

				xml.WriteStartElement("f_contains");
				foreach (string rule in kvp.Value.containsFilter) xml.WriteElementString("f_rule", rule);

				xml.WriteEndElement();
			}
		}

		private void WriteLine(string text)
		{
			if (selfInteractive) console.WriteLine(text, "ig.vfman");
			else console.WriteLine(text);
		}
	}
}
