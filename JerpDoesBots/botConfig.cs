using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
    public class twitchAPIConfig
    {
        public string client_id { get; set; }
        public int channel_id { get; set; }

        // Required Scopes:
        // channel:manage:redemptions		Channel Point rewards
        // channel:edit:commercial			Start a commercial
        // channel:manage:broadcast			Change title, game, language, etc. (also stream markers)
        // channel:read:predictions			Read predictions
        // channel:manage:predictions		Manage predictions
        // moderator:manage:announcements
        // moderator:manage:shoutouts


        // https://id.twitch.tv/oauth2/authorize?client_id=[client_id]&redirect_uri=http://localhost&response_type=token&scope=channel:manage:redemptions+channel:edit:commercial+channel:manage:broadcast+channel:read:predictions+channel:manage:predictions+moderator:manage:announcements+moderator:manage:shoutouts

        // Eventually?:
        // channel:read:polls				Read polls
        // channel:manage:polls				Create/end polls

        // Chat Bot requires:

        // chat_login
        // moderator:manage:announcements
        // moderator:manage:shoutouts

        // https://id.twitch.tv/oauth2/authorize?client_id=[client_id]&redirect_uri=http://localhost&response_type=token&scope=chat_login+moderator:manage:announcements+moderator:manage:shoutouts

        public string oauth { get; set; }
	}

	public class botConnection
	{
		public string username { get; set; }
		public string nickname { get; set; }
		public int channel_id { get; set; }
		public string oauth { get; set; }
		public string server { get; set; }
		public int port { get; set; }
		public List<string> channels { get; set; }
	}

    public class pubSubConfig
    {
		public string oauth { get; set; }
	}

	public class botConfigData
	{
		public twitchAPIConfig twitch_api { get; set; }
		public List<botConnection> connections { get; set; }
		public pubSubConfig pubsub { get; set; }
		public bool updateTwitchIDsOnUserJoins { get; set; }
		public long followerStaleCheckSeconds { get; set; }
		public bool announceFollowEvents { get; set; }
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
