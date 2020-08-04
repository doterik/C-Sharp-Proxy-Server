//#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable IDE0007 // Use implicit type

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32.SafeHandles;

namespace proxyServer
{
	public class VDictionary : VBase, IDisposable // String only
	{

		// IDisposable Implementation
		protected override void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				handle.Dispose();
				lkvp.Clear();
				lkvp = null;
			}
			disposed = true;
		}

		List<KeyValuePair<string, string>> lkvp = new List<KeyValuePair<string, string>>();
		public IEnumerable<KeyValuePair<string, string>> Items
		{
			get
			{
				foreach (var kvp in lkvp)					yield return kvp;
			}
		}
		public int Count => lkvp.Count;
		public List<string> Keys => lkvp.Select(kvp => kvp.Key).ToList();
		public List<string> Values => lkvp.Select(kvp => kvp.Value).ToList();

		public string this[string index]
		{
			get => At(index);
			set => SetOne(index, value);
		}

		public void SetOne(string key, string newText)
		{
			int index = 0;
			bool canSet = false;

			foreach (var kvp in lkvp)
			{
				if (kvp.Key == key)
				{
					canSet = true;
					break;
				}
				index++;
			}

			if (canSet) SetByIndex(index, newText);
		}

		public void SetByIndex(int index, string newText) => lkvp[index] = new KeyValuePair<string, string>(lkvp[index].Key, newText);

		public void SetByIndex(int[] indicies, string[] newText)
		{
			for (int i = 0; i < indicies.Length; i++)
			{
				SetByIndex(indicies[i], newText[i]);
			}
		}

		public void SetAll(string key, string value)
		{
			foreach (var kvp in lkvp)
			{
				if (kvp.Key == key) SetOne(key, value);
			}
		}

		/// <summary>
		/// Add's an element into the Dictionary
		/// </summary>
		/// <param name="key">The key of the element (can be a duplicate)</param>
		/// <param name="value">The value of the element (can be a duplicate)</param>
		public void Add(string key, string value) => lkvp.Add(new KeyValuePair<string, string>(key, value));

		/// <summary>
		/// Remove's the first element having the same key as specified
		/// </summary>
		/// <param name="key">The key of the element to be removed</param>
		public void RemoveByKey(string key)
		{
			int index = 0;
			bool canRemove = false;
			foreach (var kvp in lkvp)
			{
				if (kvp.Key == key)
				{
					canRemove = true;
					break;
				}

				index++;
			}

			if (canRemove) lkvp.RemoveAt(index);
		}

		/// <summary>
		/// Remove's all element having the same key as specified
		/// </summary>
		/// <param name="key">The key of the element(s) you want to remove</param>
		public void RemoveAllByKey(string key)
		{
			var temp = new List<int>();
			int index = 0;

			foreach (var kvp in lkvp)
			{
				if (kvp.Key == key) temp.Add(index); // TODO: hmm..

				index++;
			}

			if (temp.Count > 0) RemoveByIndex(temp.ToArray());
		}

		/// <summary>
		/// Remove's all element from the dictionary
		/// </summary>
		public void Clear() => lkvp.Clear();

		/// <summary>
		/// Remove's an element with the specified index form the dictionary
		/// </summary>
		/// <param name="index">The index of the item you want ot remove</param>
		public void RemoveByIndex(int index) => lkvp.RemoveAt(index);

		/// <summary>
		/// Remove's multiple items specified by the indices array
		/// </summary>
		/// <param name="indicies">The int array of the element id's which you want to remove</param>
		public void RemoveByIndex(int[] indicies)
		{
			for (int i = 0; i < indicies.Length; i++)
			{
				int cIndex = indicies[i];
				lkvp.RemoveAt(cIndex);
				for (int c = i; c < indicies.Length; c++)
				{
					if (indicies[c] > cIndex) indicies[c] -= 1;
				}
			}
		}

		/// <summary>
		/// Read's the first element with the specified key
		/// </summary>
		/// <param name="key">The key of the element</param>
		/// <returns>String value</returns>
		public string At(string key)
		{
			int index = 0;

			foreach (var kvp in lkvp)
			{
				if (kvp.Key == key) return At(index);

				index++;
			}

			return null;
		}

		/// <summary>
		/// Read's the value of an element based on the index specified
		/// </summary>
		/// <param name="index">Index of the element</param>
		/// <returns>String value</returns>

		public string At(int index) => index < lkvp.Count && lkvp.Count != 0 ? lkvp[index].Value : null;

		/// <summary>
		/// Read's multiple items with the same key
		/// </summary>
		/// <param name="key">The key of the item(s)</param>
		/// <returns>String array of values</returns>
		public IEnumerable<string> GetMultipleItems(string key)
		{
			int index = 0;

			foreach (var kvp in lkvp)
			{
				if (kvp.Key == key) yield return At(index);

				index++;
			}
		}

		/// <summary>
		/// Read's multiple items based on the indeicies
		/// </summary>
		/// <param name="indicies">The indicies of the requested values</param>
		/// <returns>String array of values</returns>
		public IEnumerable<string> GetMultipleItems(int[] indicies)
		{
			foreach (int i in indicies)
			{
				yield return lkvp[i].Value;
			}
		}

		/// <summary>
		/// Read's wheter you have at least one element with the specified key
		/// </summary>
		/// <param name="key">The key of the element you want to search</param>
		/// <returns>True if element with the key is present</returns>
		public bool ContainsKey(string key)
		{
			foreach (var kvp in lkvp)
			{
				if (kvp.Key == key) return true;
			}

			return false;
		}

		/// <summary>
		/// Read's wheter at least one element with the same value exists
		/// </summary>
		/// <param name="value">The value of the element to search</param>
		/// <returns>True if the value is in at least on of the elements</returns>
		public bool ContainsValue(string value)
		{
			foreach (var kvp in lkvp)
			{
				if (kvp.Value == value) return true; // TODO: .any()
			}

			return false;
		}
	}
}
