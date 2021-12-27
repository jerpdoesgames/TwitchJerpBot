using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System;

namespace JerpDoesBots
{
    class streamProfileEntry
	{
		public string title { get; set; }
		public string category { get; set; }
		public List<string> tags { get; set; }
	}

	class streamProfilesConfig
	{
		public List<string> tagsCommon { get; set; }
		public Dictionary<string, streamProfileEntry> entries { get; set; }
	}

	class streamProfiles : botModule
	{
		private streamProfilesConfig configData;
		public bool loaded = false;
		public const int TAGS_MAX = 5;

		public void applyProfile(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(argumentString))
            {
				streamProfileEntry useProfile;
				if (configData.entries.ContainsKey(argumentString))
                {
					useProfile = configData.entries[argumentString];

					TwitchLib.Api.Helix.Models.Channels.ModifyChannelInformation.ModifyChannelInformationRequest newChannelInfoRequest = new TwitchLib.Api.Helix.Models.Channels.ModifyChannelInformation.ModifyChannelInformationRequest()
					{
						Title = useProfile.title,
						GameId = useProfile.category
					};
					
					List<string> newTags = useProfile.tags.GetRange(0, Math.Min(useProfile.tags.Count, TAGS_MAX));
					int tagsCommonCount = TAGS_MAX - newTags.Count;

					for (int i = 0; i < tagsCommonCount; i++)
                    {
						newTags.Add(configData.tagsCommon[i]);
                    }

					m_BotBrain.updateChannelInfo(newChannelInfoRequest, newTags);
				}
				else
                {
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("modifyChannelInfoFailProfileNotFound"));
                }
			}
		}

		public streamProfiles(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_streamprofiles.json");
			if (File.Exists(configPath))
			{
				string configFileString = File.ReadAllText(configPath);
				if (!string.IsNullOrEmpty(configFileString))
				{
					configData = new JavaScriptSerializer().Deserialize<streamProfilesConfig>(configFileString);
					loaded = true;
				}
			}

			if (loaded)
			{
				chatCommandDef tempDef = new chatCommandDef("profile", applyProfile, false, false);
				m_BotBrain.addChatCommand(tempDef);
			}
		}
	}
}
