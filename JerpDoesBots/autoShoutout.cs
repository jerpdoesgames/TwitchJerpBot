using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
    public enum autoShoutUserType : int
    {
        streamer,            // Default
        messageOnly
    };

    class autoShoutoutUser
	{
		public string name { get; set; }
		public string shoutMessage { get; set; }
		public long lastShouted = 0;
		public bool shoutedSinceLoad = false;
        public autoShoutUserType type { get; set; }
	}

	class autoShoutoutConfig
	{
		public List<autoShoutoutUser> users { get; set; }
	}

	class autoShoutout : botModule
	{
		private autoShoutoutConfig configData;
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

		public override void onUserMessage(userEntry shoutUserEntry, string aMessage)
		{
			autoShoutoutUser shoutEntry;
			for (int i=0; i < configData.users.Count; i++)
			{
				shoutEntry = configData.users[i];
				if (shoutEntry.name.ToLower() == shoutUserEntry.Nickname.ToLower())
				{
					if (!shoutEntry.shoutedSinceLoad || m_BotBrain.ActionTimer.ElapsedMilliseconds > (shoutEntry.lastShouted + shoutThrottle))
					{
						shoutEntry.shoutedSinceLoad = true;
						shoutEntry.lastShouted = m_BotBrain.ActionTimer.ElapsedMilliseconds;

						string lastGame = "";
						TwitchLib.Api.V5.Models.Channels.Channel channelInfo = m_BotBrain.getSingleChannelInfoByName(shoutUserEntry.Nickname);

						if (channelInfo != null && !string.IsNullOrEmpty(channelInfo.Game))
							lastGame = "  They were last playing " + channelInfo.Game;

						if (!string.IsNullOrEmpty(shoutEntry.shoutMessage))
						{
                            switch(shoutEntry.type)
                            {
                                case autoShoutUserType.messageOnly:
                                    m_BotBrain.sendDefaultChannelMessage(shoutEntry.shoutMessage);
                                break;


                                case autoShoutUserType.streamer:
                                    m_BotBrain.sendDefaultChannelMessage("Check out " + shoutUserEntry.Nickname + " : " + shoutEntry.shoutMessage + "  ( twitch.tv/" + shoutUserEntry.Nickname.ToLower() + " )" + lastGame);
                                break;
                            }
							
						}
						else
						{
							m_BotBrain.sendDefaultChannelMessage("Check out " + shoutUserEntry.Nickname + " and give 'em a follow!  ( twitch.tv/" + shoutUserEntry.Nickname.ToLower() + " )" + lastGame);
						}
						
					}
					return;
				}
			}
		}

		public autoShoutout(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_shoutouts.json");
			if (File.Exists(configPath))
			{
				string configFileString = File.ReadAllText(configPath);
				if (!string.IsNullOrEmpty(configFileString))
				{
					configData = new JavaScriptSerializer().Deserialize<autoShoutoutConfig>(configFileString);
					loaded = true;
				}
			}
		}
	}
}
