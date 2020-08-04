//#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE1006 // Naming Styles

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using proxyServer.Interfaces;

namespace proxyServer
{
	public class VPin : VBase, ISettings, IDisposable
	{
		// IDisposable Implementation
		protected override void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				handle.Dispose();
				pin = null;
				console = null;
				excludeList = null;
			}
			disposed = true;
		}

		// ISettings Implementation

		public void LoadSettings(KeyValuePair<string, string> kvp)
		{
			string key = kvp.Key.ToLower();
			string value = kvp.Value.ToLower();

			if (key == "pinmanager") isEnable = value == "true";
			if (key == "pin") SetPin(kvp.Value);
			if (key == "pin_exclude") Exclude(kvp.Value);
		}

		public void WriteSettings(System.Xml.XmlWriter xml)
		{
			xml.WriteStartElement("settings_start");
			xml.WriteElementString("pinManager", isEnable ? "true" : "false");
			if (isSet && pin != null) xml.WriteElementString("pin", Encrypt(pin));
			foreach (string entry in excludeList)
			{
				xml.WriteElementString("pin_exclude", entry);
			}
			xml.WriteEndElement();
		}

		// Main pin manager class

		private string pin;
		private VConsole console;
		public bool isSet = false;
		public bool isEnable = true;
		private string[] excludeList;
		private VLogger logger;

		public void SetConsole(VConsole cInterface) => console = cInterface;

		public void SetLogger(VLogger lInterface) => logger = lInterface;

		public void SetPin(string input)
		{
			Thread t = new Thread(new ParameterizedThreadStart(SetPinThread));
			t.Start(input);
		}

		private void SetPinThread(object obj)
		{
			string input = (string)obj;

			if (pin != "" && pin != null)
			{
				string backup = console.GetPrompt();

				console.SetPrompt("Type in the current pin: ");
				console.IgnoreNextInput();
				string chkPin = console.ReadLine();
				if (chkPin == pin)
				{
					/*console.SetPrompt("Type in the new pin: ");
					console.IgnoreNextInput();
					string newPin = console.ReadLine();
					pin = newPin;*/
					pin = input;
					console.WriteLine("PIN Changed!");
					console.SetPrompt(backup);
				}
				else
				{
					console.WriteLine("Invalid PIN!");
					console.SetPrompt(backup);
				}
			}
			else
			{
				pin = input;
				console.WriteLine("PIN Changed!");
			}

			if (!isSet) isSet = true;
		}

		public void Exclude(string command_starting_text)
		{
			if (excludeList == null)
			{
				excludeList = new string[1];
				List<string> s = excludeList.ToList();
				s.RemoveAt(0);
				excludeList = s.ToArray();
			}
			List<string> temp = excludeList.ToList();
			temp.Add(command_starting_text);
			excludeList = temp.ToArray();
		}

		public void ReInclude(string command_starting_text)
		{
			if (excludeList == null) return;
			List<string> temp = excludeList.ToList();
			temp.Remove(command_starting_text);
			excludeList = temp.ToArray();
		}

		private bool IsExclude(string command)
		{
			if (excludeList == null) return false;

			foreach (string text in excludeList)
			{
				if (command.StartsWith(text)) return true;
			}

			return false;
		}

		public bool CheckPin(string command)
		{
			bool isValid = false;

			if (IsExclude(command)) return true;

			string backup = console.GetPrompt();
			console.SetPrompt("Please type in the current PIN code: ");
			console.IgnoreNextInput();
			console.HideNextInput();
			string input = console.ReadLine();
			if (input == pin) isValid = true;
			else logger.Log("Invalid Pin!", VLogger.LogLevel.error);

			console.SetPrompt(backup);
			return isValid;
		}

		public string GetPin() => pin;

		private string Encrypt(string clearText)
		{
			string EncryptionKey = "adbuuibsauvauzfbai3246378634985723zsdibfasfsuzfYGSGDFYGVB";
			byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
			using (Aes encryptor = Aes.Create())
			{
				Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76, 0x66, 0x42, 0x22, 0x47, 0x88 });
				encryptor.Key = pdb.GetBytes(32);
				encryptor.IV = pdb.GetBytes(16);

				using MemoryStream ms = new MemoryStream();
				using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
				{
					cs.Write(clearBytes, 0, clearBytes.Length);
					cs.Close();
				}
				clearText = Convert.ToBase64String(ms.ToArray());
			}
			return clearText;
		}

		private string Decrypt(string cipherText)
		{
			try
			{
				string EncryptionKey = "adbuuibsauvauzfbai3246378634985723zsdibfasfsuzfYGSGDFYGVB";
				byte[] cipherBytes = Convert.FromBase64String(cipherText);
				using (Aes encryptor = Aes.Create())
				{
					Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76, 0x66, 0x42, 0x22, 0x47, 0x88 });
					encryptor.Key = pdb.GetBytes(32);
					encryptor.IV = pdb.GetBytes(16);
					using MemoryStream ms = new MemoryStream();
					using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
					{
						cs.Write(cipherBytes, 0, cipherBytes.Length);
						cs.Close();
					}
					cipherText = Encoding.Unicode.GetString(ms.ToArray());
				}
				return cipherText;
			}
			catch (Exception ex)
			{
				console.Debug($"decryption error: {ex.Message}");
				return cipherText;
			}
		}
	}
}
