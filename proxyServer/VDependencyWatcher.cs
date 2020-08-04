//#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE1006 // Naming Styles

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace proxyServer
{
	public class VDependencyWatcher : VBase, IDisposable
	{
		// IDisposable Implementation
		protected override void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				handle.Dispose();
				StopWatcher();
				if (conditionList != null) conditionList.Clear();
				conditionList = null;
				if (alertMessages != null) alertMessages.Clear();
				alertMessages = null;
				_thread = null;
				if (alertsTriggerd != null) alertsTriggerd.Clear();
				alertsTriggerd = null;
				ctx = null;
			}
			disposed = true;
		}

		List<Func<bool>> conditionList = new List<Func<bool>>();
		List<VLogger.LogObj> alertMessages = new List<VLogger.LogObj>();
		Thread _thread;
		bool letRun = true;
		Dictionary<int, DateTime> alertsTriggerd = new Dictionary<int, DateTime>();
		DateTime waitForServer;
		bool ignoreServer = false;
		Form1 ctx;

		public VDependencyWatcher(Form1 context) => ctx = context;

		/// <summary>
		/// Add's a condition, which is if true a Dependency alert will trigger!
		/// </summary>
		/// <param name="condition">A condition to be tested (when true, warning will be popped)</param>
		public void AddCondition(Func<bool> condition, VLogger.LogObj alertMessage)
		{
			conditionList.Add(condition);
			alertMessages.Add(alertMessage);
		}

		/// <summary>
		/// Remove's a condition from the list
		/// </summary>
		/// <param name="index">The index of the condition to be removed</param>
		public void RemoveCondition(int index)
		{
			conditionList.RemoveAt(index);
			alertMessages.RemoveAt(index);
		}

		/// <summary>
		/// Remove's a condition from the list
		/// </summary>
		/// <param name="condition">Condition to be removed</param>
		public void RemoveCondition(Func<bool> condition)
		{
			int index = conditionList.IndexOf(condition);
			RemoveCondition(index);
		}

		/// <summary>
		/// Start's the watcher thread, to detect when a condition is true
		/// </summary>
		public void StartWatcher()
		{
			Thread t = new Thread(new ThreadStart(DWThread));
			t.Start();
		}

		/// <summary>
		/// Stop's the watcher thread, disables the condition checking
		/// </summary>
		public void StopWatcher()
		{
			letRun = false;
			if (_thread != null) _thread = null;
			alertsTriggerd.Clear();
		}

		private void DWThread()
		{
			int loopIndex = 0;

			while (letRun)
			{
				foreach (Func<bool> cond in conditionList)
				{
					if (cond())
					{
						TriggerAlert(loopIndex);
					}
					else
					{
						//Console.WriteLine("Condition is false");
					}

					loopIndex++;
				}

				loopIndex = 0;
				Thread.Sleep(1000);
			}
		}

		private void TriggerAlert(int index)
		{
			if (!ctx.isStarted && !ignoreServer)
			{
				if (waitForServer == default) waitForServer = DateTime.Now;
				TimeSpan timeElapsed = DateTime.Now - waitForServer;

				if (timeElapsed.Minutes <= 4) return;
				else ignoreServer = true;
			}

			if (alertsTriggerd.ContainsKey(index))
			{
				DateTime current = DateTime.Now;
				DateTime lastAlert = alertsTriggerd[index];
				TimeSpan p = current - lastAlert;
				if (p.TotalMinutes < 10) return;
				else alertsTriggerd[index] = current;
			}

			if (!alertsTriggerd.ContainsKey(index)) alertsTriggerd.Add(index, DateTime.Now);
			VLogger.LogObj lo = alertMessages[index];
			ctx.LogMod.Log(lo.message, lo.ll);
		}
	}
}
