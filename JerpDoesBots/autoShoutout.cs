﻿using System;
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
					if (!shoutEntry.shoutedSinceLoad || m_BotBrain.actionTimer.ElapsedMilliseconds > (shoutEntry.lastShouted + shoutThrottle))
					{
						shoutEntry.shoutedSinceLoad = true;
						shoutEntry.lastShouted = m_BotBrain.actionTimer.ElapsedMilliseconds;

						string lastGame = "";
						TwitchLib.Api.Helix.Models.Channels.GetChannelInformation.ChannelInformation channelInfo = m_BotBrain.getSingleChannelInfoByName(shoutUserEntry.Nickname);

						if (channelInfo != null && !string.IsNullOrEmpty(channelInfo.GameName))
							lastGame = "  " + string.Format(m_BotBrain.localizer.getString("shoutoutLastPlaying"), channelInfo.GameName);

						if (!string.IsNullOrEmpty(shoutEntry.shoutMessage))
						{
                            switch(shoutEntry.type)
                            {
                                case autoShoutUserType.messageOnly:
                                    m_BotBrain.sendDefaultChannelMessage(shoutEntry.shoutMessage);
                                break;


                                case autoShoutUserType.streamer:
									m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("shoutoutMessageCustom"), channelInfo.BroadcasterName, shoutEntry.shoutMessage, channelInfo.BroadcasterName.ToLower()) + lastGame);
									break;
                            }
						}
						else
						{
							m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("shoutoutMessage"), channelInfo.BroadcasterName, channelInfo.BroadcasterName.ToLower()) + lastGame);
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
