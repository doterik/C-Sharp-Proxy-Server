using System.Collections.Generic;

namespace proxyServer.Interfaces
{
	internal interface IRegEx
	{
		Dictionary<string, object> RegExName { get; set; }
		VRegEx Rxmanager { get; set; }
		bool BindRegEx(string filterName, object value);
		bool UnBindRegEx(string filterName);
		bool MatchRegex(string sMethod, object parameter, string input);
		void BindListR();
		void SetManager(VRegEx manager);
		string PushRBindInfo(); // TODO: prop
		void PullRBindInfo(string bInfo);
	}
}
