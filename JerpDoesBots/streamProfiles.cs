using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
	class gameDefaultProfileEntry
	{
		public string categoryName { get; set; }
		public string categoryID { get; set; }
		public string useProfile { get; set; }
		public bool activateOnBotLoad { get; set; }
		public bool activateOnCategoryChange { get; set; }
	}

    class streamProfileEntry
	{
		public string title { get; set; }
		public string category { get; set; }
		public List<string> tags { get; set; }
		public string rewardGroup { get; set; }
	}

	// =====================================================================/

	class streamProfilesConfig
	{
		public List<string> tagsCommon { get; set; }
		public Dictionary<string, streamProfileEntry> entries { get; set; }
		public Dictionary<string, List<pointReward>> rewardGroups { get; set; }
		public string profileNameDefault { get; set; }
		public List<gameDefaultProfileEntry> gameDefaultProfiles { get; set; }

		public streamProfilesConfig()
		{
			gameDefaultProfiles = new List<gameDefaultProfileEntry>();
		}
    }

	class streamProfiles : botModule
	{
		private streamProfilesConfig m_Config;
		private bool m_IsLoaded = false;
		public const int TAGS_MAX = 10;

		private bool applyProfileInternal(string aProfileName, bool aSilentMode = false)
		{
			if (m_Config.entries.ContainsKey(aProfileName))
			{
                streamProfileEntry useProfile = m_Config.entries[aProfileName];

                if (
                    !string.IsNullOrEmpty(useProfile.title) ||
                    !string.IsNullOrEmpty(useProfile.category) ||
                    useProfile.tags != null
                )
                {
                    List<string> newTags = useProfile.tags.GetRange(0, Math.Min(useProfile.tags.Count, TAGS_MAX));
                    int tagsCommonCount = TAGS_MAX - newTags.Count;

                    for (int i = 0; i < Math.Min(m_Config.tagsCommon.Count, tagsCommonCount); i++)
                        newTags.Add(m_Config.tagsCommon[i]);

                    TwitchLib.Api.Helix.Models.Channels.ModifyChannelInformation.ModifyChannelInformationRequest newChannelInfoRequest = new TwitchLib.Api.Helix.Models.Channels.ModifyChannelInformation.ModifyChannelInformationRequest();

                    if (!string.IsNullOrEmpty(useProfile.title) && useProfile.title != m_BotBrain.Title)
                        newChannelInfoRequest.Title = useProfile.title;

                    if (!string.IsNullOrEmpty(useProfile.category) && useProfile.category != m_BotBrain.CategoryID)
                        newChannelInfoRequest.GameId = useProfile.category;

                    if (useProfile.tags != null)
                        newChannelInfoRequest.Tags = newTags.ToArray();

                    m_BotBrain.updateChannelInfo(newChannelInfoRequest, newTags, aSilentMode);
                }

                if (!string.IsNullOrEmpty(useProfile.rewardGroup))
                    applyRewardGroupInternal(useProfile.rewardGroup, aSilentMode);

				return true;
            }

            return false;
		}

		public void applyProfile(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			if (m_IsLoaded && !string.IsNullOrEmpty(argumentString))
			{
				if (m_Config.entries.ContainsKey(argumentString))
				{
					applyProfileInternal(argumentString, aSilent);
                    // TODO: Error if applyProfileInternal returns false for something about failing to apply?
                }
                else
				{
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("modifyChannelInfoFailProfileNotFound"));
				}
			}
		}

        public override void onCategoryIDChanged()	// TODO: Should consolidate this and onBotFullyLoaded
        {
            bool hasGameDefaultProfile = false;
            foreach (gameDefaultProfileEntry curProfile in m_Config.gameDefaultProfiles)
            {
                if (curProfile.categoryID == m_BotBrain.CategoryID && curProfile.activateOnCategoryChange)
                {
                    applyProfileInternal(curProfile.useProfile, true);
                    hasGameDefaultProfile = true;
                    break;
                }
            }

            if (!hasGameDefaultProfile && !string.IsNullOrEmpty(m_Config.profileNameDefault))
            {
                applyProfileInternal(m_Config.profileNameDefault, true);
            }
        }

        public override void onBotFullyLoaded()
        {
			bool hasGameDefaultProfile = false;
			foreach (gameDefaultProfileEntry curProfile in m_Config.gameDefaultProfiles)
			{
				if (curProfile.categoryID == m_BotBrain.CategoryID && curProfile.activateOnBotLoad)
				{
					applyProfileInternal(curProfile.useProfile, true);
					hasGameDefaultProfile = true;
					break;
				}
			}

			if (!hasGameDefaultProfile && !string.IsNullOrEmpty(m_Config.profileNameDefault))
			{
				applyProfileInternal(m_Config.profileNameDefault, true);
			}
        }

        public void applyRewardGroupInternal(string aGroupName, bool aSilentMode = false)
        {
			// Clear rewards from other groups first
			foreach(string groupKey in m_Config.rewardGroups.Keys)
            {

                foreach (pointReward curReward in m_Config.rewardGroups[groupKey])
                {
					curReward.shouldExistOnTwitch = groupKey == aGroupName;
                }
            }

			pointRewardManager.updateRemoteRewardsFromLocalData();

			if (!aSilentMode && (pointRewardManager.lastUpdateRewardsAdded > 0 || pointRewardManager.lastUpdateRewardsRemoved > 0 || pointRewardManager.lastUpdateRewardsUpdated > 0))
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("channelPointRewardsCreatedRemoved"), pointRewardManager.lastUpdateRewardsAdded, pointRewardManager.lastUpdateRewardsRemoved, pointRewardManager.lastUpdateRewardsUpdated));
        }

		public void applyRewardGroup(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			if (m_IsLoaded && !string.IsNullOrEmpty(argumentString))
            {
				applyRewardGroupInternal(argumentString, aSilent);
            }
        }

		private void load()
        {
			m_IsLoaded = false;
			string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_streamprofiles.json");
			if (File.Exists(configPath))
			{
				string configFileString = File.ReadAllText(configPath);
				if (!string.IsNullOrEmpty(configFileString))
				{
					m_Config = new JavaScriptSerializer().Deserialize<streamProfilesConfig>(configFileString);


					Dictionary<string, List<pointReward>> updatedRewardGroups = new Dictionary<string, List<pointReward>>();
                    foreach (string groupKey in m_Config.rewardGroups.Keys)
                    {
						List<pointReward> updatedRewardList = new List<pointReward>();	// To repopulate in case rewards already exist (on reload)
                        foreach (pointReward curReward in m_Config.rewardGroups[groupKey])
                        {
							updatedRewardList.Add(pointRewardManager.addUpdatePointReward(curReward));
                        }

						updatedRewardGroups[groupKey] = updatedRewardList;
                    }

					m_Config.rewardGroups = updatedRewardGroups;

                    m_IsLoaded = true;
				}
			}
		}

		public void reload(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			load();

			if (m_IsLoaded)
            {
				if (!aSilent)
					m_BotBrain.sendDefaultChannelMessage("Stream Profiles reloaded");
            }
			else
            {
				m_BotBrain.sendDefaultChannelMessage("Stream Profiles reload failed");
			}
        }

		public streamProfiles(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			load();

			if (m_IsLoaded)
			{
				chatCommandDef tempDef = new chatCommandDef("profile", null, false, false);
				tempDef.addSubCommand(new chatCommandDef("reload", reload, false, false));
				tempDef.addSubCommand(new chatCommandDef("apply", applyProfile, false, false));
				tempDef.addSubCommand(new chatCommandDef("setrewards", applyRewardGroup, false, false));

				m_BotBrain.addChatCommand(tempDef);
			}
		}
	}
}
