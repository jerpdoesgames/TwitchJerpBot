using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
	/// <summary>
	/// These specify profiles can be applied by default if certain conditions are met.
	/// </summary>
	class defaultProfileEntry
	{
		/// <summary>
		///  Unused.  Readable text name of the category.
		/// </summary>
		public string categoryName { get; set; }	// TODO: Allow this to be a fallback so I don't need to check category IDs?
		/// <summary>
		/// ID for the category on Twitch.  Can be empty/null to skip.
		/// </summary>
		public string categoryID { get; set; }
		/// <summary>
		/// Name the profile to be set if requirements are met for this entry.
		/// </summary>
		public string useProfile { get; set; }
		/// <summary>
		/// Whether this profile activates when the bot first loads.
		/// </summary>
		public bool activateOnBotLoad { get; set; }
		/// <summary>
		/// Whether this profile activates when changing categories.
		/// </summary>
		public bool activateOnCategoryChange { get; set; }
		/// <summary>
		/// Optional requirements for this profile to activate.
		/// </summary>
		public streamCondition requirements { get; set; }
	}

	/// <summary>
	/// A bundle of title, category, tags, and rewards that can be applied together.
	/// </summary>
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
		public List<defaultProfileEntry> gameDefaultProfiles { get; set; }

		public streamProfilesConfig()
		{
			gameDefaultProfiles = new List<defaultProfileEntry>();
		}
    }

	/// <summary>
	/// Module for applying Stream Profiles - essentially bundles of title, category, tags, channel point rewards, etc. which can all be assigned together.
	/// </summary>
	class streamProfiles : botModule
	{
		private streamProfilesConfig m_Config;
		private bool m_IsLoaded = false;
		public const int TAGS_MAX = 10;

        /// <summary>
        /// Internal function for applying profiles.
        /// </summary>
        /// <param name="aProfileName">Name of the profile to apply.</param>
        /// <param name="aSilent">Whether to have output on success.</param>
        /// <returns>Whether the profile was successfully applied.</returns>
        private bool applyProfileInternal(string aProfileName, bool aSilent = false)
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

                    if (!string.IsNullOrEmpty(useProfile.title) && useProfile.title != jerpBot.instance.Title)
                        newChannelInfoRequest.Title = useProfile.title;

                    if (!string.IsNullOrEmpty(useProfile.category) && useProfile.category != jerpBot.instance.CategoryID)
                        newChannelInfoRequest.GameId = useProfile.category;

                    if (useProfile.tags != null)
                        newChannelInfoRequest.Tags = newTags.ToArray();

                    jerpBot.instance.updateChannelInfo(newChannelInfoRequest, newTags, aSilent);
                }

                if (!string.IsNullOrEmpty(useProfile.rewardGroup))
                    applyRewardGroupInternal(useProfile.rewardGroup, aSilent);

				return true;
            }

            return false;
		}

		/// <summary>
		/// User-facing method for applying profiles
		/// </summary>
		/// <param name="commandUser">The user who's attempting to set a profile.</param>
		/// <param name="argumentString">Name of the profile being set.</param>
		/// <param name="aSilent">Whether to have output on success.</param>
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
					jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("modifyChannelInfoFailProfileNotFound"));
				}
			}
		}

        /// <summary>
		/// Internal function for applying an automatic/default profile
		/// </summary>
		/// <param name="aActivateViaCategoryChange">Whether to apply when the category is changed (false for applying as a result of chat bot loading).</param>
        private void attemptApplyDefaultProfile(bool aActivateViaCategoryChange = false)
		{
            bool hasGameDefaultProfile = false;
            foreach (defaultProfileEntry curProfile in m_Config.gameDefaultProfiles)
            {
                if (((aActivateViaCategoryChange && curProfile.activateOnCategoryChange) || (!aActivateViaCategoryChange && curProfile.activateOnBotLoad)) && (string.IsNullOrEmpty(curProfile.categoryID) || curProfile.categoryID == jerpBot.instance.CategoryID) && (curProfile.requirements == null || curProfile.requirements.isMet()))
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

		/// <summary>
		/// Checks for and applies a profile after changing categories.
		/// </summary>
        public override void onCategoryIDChanged()
        {
			attemptApplyDefaultProfile(true);
        }

		/// <summary>
		/// Checks for and applies a profile after the bot is first loaded.
		/// </summary>
        public override void onBotFullyLoaded()
        {
            attemptApplyDefaultProfile();
        }

		/// <summary>
		/// Internal function to apply a group of channel point rewards.
		/// </summary>
		/// <param name="aGroupName">Name for the group of rewards to apply.</param>
		/// <param name="aSilentMode">Whether to have output on success.</param>
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
				jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("channelPointRewardsCreatedRemoved"), pointRewardManager.lastUpdateRewardsAdded, pointRewardManager.lastUpdateRewardsRemoved, pointRewardManager.lastUpdateRewardsUpdated));
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

		/// <summary>
		/// Reloads the list of stream profiles, reward groups, etc. to reflect the current json config.
		/// </summary>
		/// <param name="commandUser">The user who's attempting to reload the profile list.</param>
		/// <param name="argumentString">Unused.</param>
		/// <param name="aSilent">Whether to output on success.</param>
		public void reload(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			load();

			if (m_IsLoaded)
            {
				if (!aSilent)
					jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("streamProfileReloadSuccess"));
            }
			else
            {
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("streamProfileReloadFail"));
            }
        }

		/// <summary>
		/// Initialize command entries for Stream Profiles.
		/// </summary>
		public streamProfiles() : base(true, true, false)
		{
			load();

			if (m_IsLoaded)
			{
				chatCommandDef tempDef = new chatCommandDef("profile", null, false, false);
				tempDef.addSubCommand(new chatCommandDef("reload", reload, false, false));
				tempDef.addSubCommand(new chatCommandDef("apply", applyProfile, false, false));
				tempDef.addSubCommand(new chatCommandDef("setrewards", applyRewardGroup, false, false));

				jerpBot.instance.addChatCommand(tempDef);
			}
		}
	}
}
