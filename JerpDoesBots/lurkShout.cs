using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
    public enum lurkShoutUserType : int
    {
        defaultType
    };

    class lurkShoutoutUser
	{
		public string name { get; set; }
		public string shoutMessage { get; set; }
		public long lastShouted = 0;
		public bool shoutedSinceLoad = false;
        public lurkShoutUserType type { get; set; }
	}

	class lurkShoutoutConfig
	{
		public List<lurkShoutoutUser> users { get; set; }
	}

	// This exists pretty much for the sole purpose of meme-ing on Knutaf.
	class lurkShoutout : botModule
	{
		private lurkShoutoutConfig configData;
		public bool loaded = false;
		private long shoutThrottle = 14400000;	// 4 hours, I think?

		private bool isValidShoutUser(string checkUser)
		{
			for (int i=0; i < configData.users.Count; i++)
			{
				if (configData.users[i].name == checkUser)
				{
					return true;
				}
			}

			return false;
		}

		public override void onUserJoin(userEntry shoutUserEntry)
		{
			lurkShoutoutUser shoutEntry;
			for (int i=0; i < configData.users.Count; i++)
			{
				shoutEntry = configData.users[i];
				if (shoutEntry.name.ToLower() == shoutUserEntry.Nickname.ToLower())
				{
					if (!shoutEntry.shoutedSinceLoad || m_BotBrain.ActionTimer.ElapsedMilliseconds > (shoutEntry.lastShouted + shoutThrottle))
					{
						shoutEntry.shoutedSinceLoad = true;
						shoutEntry.lastShouted = m_BotBrain.ActionTimer.ElapsedMilliseconds;
						if (!string.IsNullOrEmpty(shoutEntry.shoutMessage))
						{
                            switch(shoutEntry.type)
                            {

                                default:
                                case lurkShoutUserType.defaultType:
                                    m_BotBrain.sendDefaultChannelMessage(shoutEntry.shoutMessage);
                                    break;
                            }
						}
					}
					return;
				}
			}
		}

		public lurkShoutout(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_lurkshouts.json");
			if (File.Exists(configPath))
			{
				string configFileString = File.ReadAllText(configPath);
				if (!string.IsNullOrEmpty(configFileString))
				{
					configData = new JavaScriptSerializer().Deserialize<lurkShoutoutConfig>(configFileString);
					loaded = true;
				}
			}
		}
	}
}
