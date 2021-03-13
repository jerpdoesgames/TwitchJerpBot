using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
    public class twitchAPIConfig
    {
        public string client_id { get; set; }
        public int channel_id { get; set; }
        public string oauth { get; set; }
	}

	public class botConnection
	{
		public string username { get; set; }
		public string nickname { get; set; }
		public string oauth { get; set; }
		public string server { get; set; }
		public int port { get; set; }
		public List<string> channels { get; set; }
	}

	public class botConfigData
	{
		public twitchAPIConfig twitch_api { get; set; }
		public List<botConnection> connections { get; set; }
	}

	class botConfig
	{
		public botConfigData configData;
		public bool loaded = false;

		public botConfig()
		{
			string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_config.json");
			if (File.Exists(configPath))
			{
				string configFileString = File.ReadAllText(configPath);
				if (!string.IsNullOrEmpty(configFileString))
				{
					configData = new JavaScriptSerializer().Deserialize<botConfigData>(configFileString);
					loaded = true;
				}
			}
		}
	}
}
