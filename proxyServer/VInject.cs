﻿//#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE0059 // Unnecessary assignment of a value
#pragma warning disable IDE1006 // Naming Styles

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.Win32.SafeHandles;
using proxyServer.Interfaces;

namespace proxyServer
{
	public class VInject :VBase, IFilter, IRegEx, ISettings, IHelp, IDisposable
	{
		// IDisposable Implementation
		protected override void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				handle.Dispose();
				_helpFile = null;
				RegExName.Clear();
				RegExName = null;
				Rxmanager = null;
				FilterName.Clear();
				FilterName = null;
				Manager = null;
				reg = null;
				mediaReplace.Clear();
				mediaReplace = null;
				payloadReplace.Clear();
				payloadReplace = null;
				autoPayload = null;
				console = null;
			}
			disposed = true;
		}

		// IHelp implementation

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

			if (key == "match_mode") mMode = StringToMatchMode(value);
			if (key == "match_option") mOption = StringToMatchOption(value);
			if (key == "match_file") filePathOption = StringToMatchOption(value);
			if (key == "match_engine") mEngine = StringToMatchEngine(value);
			if (key == "auto_payload") autoPayload = kvp.Value;
			if (key == "r_bind") PullRBindInfo(kvp.Value);
			if (key == "f_bind") PullBindInfo(kvp.Value);
			if (key.StartsWith("payload_rep_"))
			{
				string k = kvp.Key.Substring(12);
				string v = kvp.Value;
				if (!payloadReplace.ContainsKey(k)) payloadReplace.Add(k, v);
			}
			if (key.StartsWith("media_rep_"))
			{
				string k = kvp.Key.Substring(10);
				string v = kvp.Value;
				if (!mediaReplace.ContainsKey(k)) mediaReplace.Add(k, v);
			}
		}

		public void WriteSettings(System.Xml.XmlWriter xml)
		{
			xml.WriteStartElement("settings_start");

			xml.WriteElementString("match_mode", MatchModeToString(mMode));
			xml.WriteElementString("match_option", MatchOptionToString(mOption));
			xml.WriteElementString("match_engine", MatchEngineToString(mEngine));
			xml.WriteElementString("match_file", MatchOptionToString(filePathOption));
			xml.WriteElementString("auto_payload", autoPayload);
			xml.WriteElementString("r_bind", PushRBindInfo());
			xml.WriteElementString("f_bind", PushBindInfo());
			foreach (KeyValuePair<string, string> kvp in payloadReplace)
			{
				xml.WriteElementString($"payload_rep_{kvp.Key}", kvp.Value);
			}
			foreach (KeyValuePair<string, string> kvp in mediaReplace)
			{
				xml.WriteElementString($"media_rep_{kvp.Key}", kvp.Value);
			}

			xml.WriteEndElement();
		}

		// IRegEx Implementation
		public VRegEx Rxmanager { get; set; }
		public Dictionary<string, object> RegExName { get; set; } = new Dictionary<string, object>();

		public bool BindRegEx(string regexName, object parameter)
		{
			if (RegExName.ContainsKey(regexName)) return false;
			RegExName.Add(regexName, parameter);
			return true;
		}

		public bool UnBindRegEx(string regexName)
		{
			if (!RegExName.ContainsKey(regexName)) return false;
			RegExName.Remove(regexName);

			return true;
		}

		public void SetManager(VRegEx regex) => Rxmanager = regex;

		public void BindListR()
		{
			console.WriteLine("==Start of RegEx bind List==", "ig.inject");
			foreach (KeyValuePair<string, object> kvp in RegExName)
			{
				console.WriteLine($"{kvp.Key}:\t{kvp.Value}", "ig.inject");
			}
			console.WriteLine("==End of RegEx bind list==", "ig.inject");
		}

		public bool MatchRegex(string mode, object parameter, string value)
		{
			bool canSet = false;
			int index = 0;

			foreach (object v in RegExName.Values)
			{
				if (v.ToString() == parameter.ToString())
				{
					canSet = true;
					break;
				}

				index++;
			}

			string realRegexName;
			if (canSet) realRegexName = RegExName.Keys.ToArray()[index];
			else return false;

			bool result = false;

			if (mode == "and")
			{
				result = Rxmanager.RunAnd(value, realRegexName);
			}
			else if (mode == "or")
			{
				result = Rxmanager.RunOr(value, realRegexName);
			}

			return result;
		}

		public string PushRBindInfo()
		{
			string finalResult = "";

			foreach (KeyValuePair<string, object> kvp in RegExName)
			{
				string key = kvp.Key;
				string value = kvp.Value.ToString();
				finalResult += key + ":" + value + ";";
			}

			if (finalResult.Length > 0) finalResult = finalResult.Substring(0, finalResult.Length - 1);

			return finalResult;
		}

		public void PullRBindInfo(string info)
		{
			string[] data = info.Split(';');

			foreach (string d in data)
			{
				string[] subData = d.Split(':');
				string key = subData[0];
				string value = subData[1];

				BindRegEx(key, value);
			}
		}

		// IFilter Implementation

		public Dictionary<string, object> FilterName { get; set; } = new Dictionary<string, object>();

		public VFilter Manager { get; set; }

		public string PushBindInfo()
		{
			string info = "";

			foreach (KeyValuePair<string, object> kvp in FilterName)
			{
				info += $"{kvp.Key}:{kvp.Value};";
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
				string level = kvp2[1];
				string name = kvp2[0];
				FilterName.Add(name, level);
			}
		}

		public bool BindFilter(string validFilterName, object input)
		{
			string op = (string)input;
			FilterName.Add(validFilterName, op);
			return true;
		}

		public bool SearchFilter(string sMethod, object searchParam, string input)
		{
			string p = (string)searchParam;
			string targetFilterName = "";
			foreach (KeyValuePair<string, object> pair in FilterName)
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
				return false; // if target filter is not found deny, because we don't want to inject at random places
			}

			if (sMethod == "and")
			{
				return Manager.RunAllCompareAnd(targetFilterName, input);
			}
			else if (sMethod == "or")
			{
				return Manager.RunAllCompareOr(targetFilterName, input);
			}
			else
			{
				//console.WriteLine("[ERROR] Invalid SearchFilter option sMethod", console.GetIntercativeGroup());
				return false;
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
			console.WriteLine("=========Start Of bind list=========", "ig.inject");
			foreach (KeyValuePair<string, object> kvp in FilterName)
			{
				string ll = (string)kvp.Value;
				console.WriteLine(kvp.Key + ":\t" + ll, "ig.inject");
			}
			console.WriteLine("==========End Of bind list==========", "ig.inject");
		}

		public void SetManager(VFilter fman) => Manager = fman;

		//Main inject class

		public enum Mode
		{
			HTML,
			Javascript,
			CSS
		}

		public enum MatchMode
		{
			Replace,
			InjectAfter,
			InjectBefore
		}

		public enum MatchEngine
		{
			RegEx,
			Filters
		}

		public enum MatchOptions
		{
			And,
			Or,
			Both,
			Undefined
		}

		public MatchEngine mEngine = MatchEngine.Filters;
		public MatchOptions mOption = MatchOptions.And;
		public MatchOptions filePathOption = MatchOptions.And;
		public MatchMode mMode = MatchMode.InjectAfter;
		private VConsole console;
		private VRegEx reg;
		private Dictionary<string, string> mediaReplace = new Dictionary<string, string>();
		private Dictionary<string, string> payloadReplace = new Dictionary<string, string>();
		public string autoPayload = "";

		public VInject(VConsole con, VRegEx rx, VMitm mitm, VDependencyWatcher dw, Form1 ctx)
		{
			console = con;
			reg = rx;
			dw.AddCondition(() => mitm.CheckServiceState(VMitm.InjectServices.AutoInjection) && autoPayload == "", ctx.CreateLog("Auto injection is enabled, but no payload is set", VLogger.LogLevel.warning));
			dw.AddCondition(() => mitm.CheckServiceState(VMitm.InjectServices.MatchInjection) && payloadReplace.Count == 0, ctx.CreateLog("Match Injection is enabled, but no payload is set", VLogger.LogLevel.warning));
			dw.AddCondition(() => mitm.CheckServiceState(VMitm.InjectServices.MediaInjection) && mediaReplace.Count == 0, ctx.CreateLog("Media Injection is enabled, but no file is set", VLogger.LogLevel.warning));
			dw.AddCondition(() => !mitm.IsAllOfflineI() && !mitm.started, ctx.CreateLog("One or more injection service is enabled, but mitm service is not running!", VLogger.LogLevel.warning));
			dw.AddCondition(() => mitm.CheckServiceState(VMitm.InjectServices.MatchInjection) && mitm.CheckServiceState(VMitm.InjectServices.AutoInjection), ctx.CreateLog("Both Match and Auto injection is enabled, this may produce unexpected results!", VLogger.LogLevel.warning));
		}

		public string AutoInject(string originalText, string payload, Mode iMode)
		{
			string finalResult = "";

			if (iMode == Mode.HTML)
			{
				string[] lines = originalText.Split('\n');
				string[] tempLines = originalText.Split('\n');
				string keyElement = "";
				int lnIndex = 0;

				if (lines.Contains("</body>") && keyElement == "") keyElement = "</body>";
				if (lines.Contains("</head>") && keyElement == "") keyElement = "</head>";

				foreach (string tline in lines)
				{
					var line = tline.EndsWith("\r") ? tline.Replace("\r", string.Empty) : tline;

					if (keyElement == "") finalResult = $"{originalText}{(originalText.EndsWith("\r\n") ? "" : "\r\n")}{payload}";
					else
					{
						if (line == keyElement)
						{
							tempLines[lnIndex] = $"{payload}\r\n{keyElement}\r";

							foreach (string text in tempLines)
							{
								finalResult += $"{text}\n";
							}

							break;
						}
					}

					lnIndex++;
				}
			}

			if (iMode == Mode.Javascript || iMode == Mode.CSS) finalResult = $"{originalText}\r\n{payload}\r\n";

			return finalResult;
		}

		public string MatchAndInject(string original, string payload, MatchMode mMode, MatchOptions opt)
		{
			string finalResult = "";

			string[] lines = original.Split('\n');
			string[] tempLines = original.Split('\n');
			int lnIndex = 0;

			foreach (string tline in lines)
			{
				var line = tline.EndsWith("\r") ? tline.Replace("\r", string.Empty) : tline;

				if (mEngine == MatchEngine.Filters)
				{
					bool isOrEmpty = false;
					bool isAndEmpty = false;
					string andName = GetFilterByParam("inject_and");
					string orName = GetFilterByParam("inject_or");
					isOrEmpty = Manager.IsFilterEmpty(orName);
					isAndEmpty = Manager.IsFilterEmpty(andName);
					bool result = false;

					if (!isOrEmpty && !isAndEmpty)
					{
						bool r1 = SearchFilter("or", "inject_or", line);
						bool r2 = SearchFilter("and", "inject_and", line);

						result = (opt == MatchOptions.Both && r1 && r2) || (opt == MatchOptions.Or && r1) || (opt == MatchOptions.And && r2);
					}
					else if (!isOrEmpty && (opt == MatchOptions.Both || opt == MatchOptions.Or)) result = SearchFilter("or", "inject_or", line);
					else if (!isAndEmpty && (opt == MatchOptions.Both || opt == MatchOptions.And)) result = SearchFilter("and", "inject_and", line);
					else
					{
						return original;
					}

					if (result)
					{
						if (mMode == MatchMode.Replace) tempLines[lnIndex] = $"{payload}\r";
						else if (mMode == MatchMode.InjectAfter) tempLines[lnIndex] = $"{line}\r\n{payload}\r";
						else if (mMode == MatchMode.InjectBefore) tempLines[lnIndex] = $"{payload}\r\n{line}\r";

						foreach (string sline in tempLines) finalResult += $"{sline}\n";

						return finalResult;
					}
				}
				else if (mEngine == MatchEngine.RegEx)
				{
					bool isOrEmpty = false;
					bool isAndEmpty = false;
					string andName = GetRegexByParam("inject_and");
					string orName = GetRegexByParam("inject_or");
					isOrEmpty = Rxmanager.IsRegexEmpty(orName);
					isAndEmpty = Rxmanager.IsRegexEmpty(andName);
					bool result = false;

					if (!isOrEmpty && !isAndEmpty)
					{
						bool r1 = MatchRegex("or", "inject_or", line);
						bool r2 = MatchRegex("and", "inject_and", line);

						result = (opt == MatchOptions.Both && r1 && r2) || (opt == MatchOptions.Or && r1) || (opt == MatchOptions.And && r2);
					}
					else if (!isOrEmpty && (opt == MatchOptions.Both || opt == MatchOptions.Or)) result = MatchRegex("or", "inject_or", line);
					else if (!isAndEmpty && (opt == MatchOptions.Both || opt == MatchOptions.And)) result = MatchRegex("and", "inject_and", line);
					else
					{
						return original;
					}

					if (result)
					{
						if (mMode == MatchMode.Replace) tempLines[lnIndex] = $"{payload}\r";
						else if (mMode == MatchMode.InjectAfter) tempLines[lnIndex] = $"{line}\r\n{payload}\r";
						else if (mMode == MatchMode.InjectBefore) tempLines[lnIndex] = $"{payload}\r\n{line}\r";

						foreach (string sline in tempLines) finalResult += $"{sline}\n";

						return finalResult;
					}
				}

				lnIndex++;
			}

			return finalResult;
		}

		public bool MediaReplace(Request r, MatchOptions filePathMatching)
		{
			bool andResult, orResult;

			if (mEngine == MatchEngine.Filters)
			{
				andResult = SearchFilter("and", "inject_media_and", r.target);
				orResult = SearchFilter("or", "inject_media_or", r.target);
			}
			else if (mEngine == MatchEngine.RegEx)
			{
				andResult = MatchRegex("and", "inject_media_and", r.target);
				orResult = MatchRegex("or", "inject_media_or", r.target);
			}
			else return false;

			return (filePathMatching == MatchOptions.Both && andResult && orResult)
				|| (filePathMatching == MatchOptions.And && andResult)
				|| (filePathMatching == MatchOptions.Or && orResult);
		}

		public byte[] GetMediaHijack(Request r)
		{
			string targetFile = r.target;
			bool canContinue = false;
			string _lfilterName = "";
			foreach (string fname in mediaReplace.Keys)
			{
				string mode = fname.Contains("or") ? "or" : "and";
				bool result = (mode == "and") ? Manager.RunAllCompareAnd(fname, targetFile) : Manager.RunAllCompareOr(fname, targetFile);
				if (result)
				{
					canContinue = true;
					_lfilterName = fname;
					break;
				}
			}
			if (!canContinue) return null;
			string newFile = mediaReplace[_lfilterName];
			if (IsLocalFile(newFile)) return File.ReadAllBytes(newFile);
			else
			{
				try
				{
					WebClient wc = new WebClient
					{
						Proxy = null
					};
					byte[] file = wc.DownloadData(newFile);
					return file;
				}
				catch (Exception)
				{
					console.Debug("Web Image Inject failed!");
					return null;
				}
			}
		}

		public bool AssignPayload(string filterName, string payload)
		{
			if (payloadReplace.ContainsKey(filterName)) return false;
			payloadReplace.Add(filterName, payload);
			return true;
		}

		public bool RemovePayload(string filterName)
		{
			if (!payloadReplace.ContainsKey(filterName)) return false;
			payloadReplace.Remove(filterName);
			return true;
		}

		public void ListPayload()
		{
			console.WriteLine("==Start of payload list==", "ig.inject");
			console.WriteLine($"Count: {payloadReplace.Count}", "ig.inject");
			foreach (KeyValuePair<string, string> kvp in payloadReplace)
			{
				console.WriteLine($"{kvp.Key}:\t{kvp.Value}", "ig.inject");
			}
			console.WriteLine("==End of payload list==", "ig.inject");
		}

		public bool AssignFilterToFile(string filterName, string filePath)
		{
			if (mediaReplace.ContainsKey(filterName)) return false;
			//if (!File.Exists(filePath)) return false;

			mediaReplace.Add(filterName, filePath);

			return true;
		}

		public bool RemoveFilterToFile(string filterName)
		{
			if (!mediaReplace.ContainsKey(filterName)) return false;

			mediaReplace.Remove(filterName);

			return false;
		}

		public void FilterToFileList()
		{
			console.WriteLine("==Start of filter->file bind list==", "ig.inject");
			console.WriteLine($"Count: {mediaReplace.Count}", "ig.inject");

			foreach (KeyValuePair<string, string> kvp in mediaReplace)
			{
				console.WriteLine($"{kvp.Key}:\t{kvp.Value}", "ig.inject");
			}

			console.WriteLine("==End of list==", "ig.inject");
		}

		public string GetCurrentPayload()
		{
			string xname = "";
			string suffix = "_";
			if (mOption == MatchOptions.And) suffix += "and";
			else if (mOption == MatchOptions.Or) suffix += "or";
			else if (mOption == MatchOptions.Both) suffix += "both";
			else return null;
			if (suffix == "_both")
			{
				string xname2 = "";
				if (mEngine == MatchEngine.Filters)
				{
					xname = GetFilterByParam("inject_and");
					xname2 = GetFilterByParam("inject_or");
				}
				else
				{
					xname = GetRegexByParam("inject_and");
					xname2 = GetRegexByParam("inject_or");
				}

				if (xname != null && xname2 != null)
				{
					string p1 = null;
					string p2 = null;
					if (payloadReplace.ContainsKey(xname)) p1 = payloadReplace[xname];
					if (payloadReplace.ContainsKey(xname2)) p2 = payloadReplace[xname2];

					if (p1 != null && p2 != null && p1 == p2) return p1;
					else if (p1 != null && p2 != null)
					{
						console.Debug("And, or inject list payload mismatch!");
						return p1;
					}
					else return p1 == null ? p2
							  : p2 == null ? p1
							  : null;
				}
				else return
						xname != null
							? payloadReplace.ContainsKey(xname) ? payloadReplace[xname] : null
						: xname2 != null
							? payloadReplace.ContainsKey(xname2) ? payloadReplace[xname2] : null
						: null;
			}
			else
			{
				xname = mEngine == MatchEngine.Filters ? GetFilterByParam("inject" + suffix) : GetRegexByParam("inject" + suffix);

				return payloadReplace.ContainsKey(xname) ? payloadReplace[xname] : null;
			}
		}

		private bool IsLocalFile(string filePath)
		{
			bool colonFound = false;
			bool bslashFound = false;

			for (int i = 0; i < filePath.Length; i++)
			{
				char c = filePath[i];
				if (c == ':' && i == 1) colonFound = true;
				if (c == '\\' && i == 2) bslashFound = true;

				if (colonFound && bslashFound) return true;
				if (i > 10) return false;
			}

			return false;
		}

		private string GetRegexByParam(string param)
		{
			if (RegExName.Count <= 0) return null;
			int index = 0;
			bool canSet = false;

			foreach (KeyValuePair<string, object> kvp in RegExName)
			{
				if (kvp.Value.ToString() == param)
				{
					canSet = true;
					break;
				}

				index++;
			}

			if (canSet)
			{
				string result = RegExName.Keys.ToArray()[index];
				return result;
			}

			return null;
		}

		private string GetFilterByParam(string param)
		{
			if (FilterName.Count <= 0) return null;
			if (FilterName.ContainsValue(param))
			{
				int index = 0;
				foreach (object f in FilterName.Values)
				{
					if (f.ToString() == param)
					{
						return FilterName.Keys.ToArray()[index];
					}
				}
			}

			return null;
		}

		public static string MatchModeToString(MatchMode mm)
			=> mm == MatchMode.InjectAfter ? "after" : mm == MatchMode.InjectBefore ? "before" : "replace";

		public static MatchMode StringToMatchMode(string input)
		{
			input = input.ToLower();
			return input == "after" ? MatchMode.InjectAfter : input == "before" ? MatchMode.InjectBefore : MatchMode.Replace;
		}

		public static string MatchOptionToString(MatchOptions mo)
			=> mo == MatchOptions.And ? "and" : mo == MatchOptions.Or ? "or" : mo == MatchOptions.Both ? "both" : "undefined";

		public static MatchOptions StringToMatchOption(string value)
		{
			value = value.ToLower();
			return value == "and" ? MatchOptions.And
				 : value == "or" ? MatchOptions.Or
				 : value == "both" ? MatchOptions.Both : MatchOptions.Undefined;
		}

		public static string MatchEngineToString(MatchEngine me) => me == MatchEngine.Filters ? "filter" : "regex";

		public static MatchEngine StringToMatchEngine(string value)
		{
			value = value.ToLower();
			return value == "filter" ? MatchEngine.Filters : MatchEngine.RegEx;
		}
	}
}
