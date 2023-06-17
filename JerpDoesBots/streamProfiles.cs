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

	class streamProfileReward // TODO: Move this somewhere centralized and use it for raffles/etc.
	{
		public string title { get; set; }
		public string description { get; set; }
		public int cost { get; set; }
		public int maxPerStream { get; set; }
		public string backgroundColor { get; set; }
		public int globalCooldownSeconds { get; set; }
		public int maxPerUserPerStream { get; set; }
		public bool requireUserInput { get; set; }
		public bool autoFulfill { get; set; }
		public bool enabled { get; set; }
		public string rewardID { get; set; }	// ID on Twitch

		public streamProfileReward()
        {
			cost = 1;
			maxPerStream = -1;
			globalCooldownSeconds = -1;
			maxPerUserPerStream = -1;
			enabled = true;
			autoFulfill = false;
        }

		public TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest getCreateRequest()
        {
			TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest newRewardRequest = new TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest();
			newRewardRequest.Title = title;
			newRewardRequest.Cost = cost;

			if (!string.IsNullOrEmpty(description))
				newRewardRequest.Prompt = description;

			if (!string.IsNullOrEmpty(backgroundColor))
				newRewardRequest.BackgroundColor = backgroundColor;

			if (maxPerUserPerStream >= 1)
            {
				newRewardRequest.MaxPerUserPerStream = maxPerUserPerStream;
				newRewardRequest.IsMaxPerUserPerStreamEnabled = true;
            }

			if (maxPerStream >= 1)
            {
				newRewardRequest.MaxPerStream = maxPerStream;
				newRewardRequest.IsMaxPerStreamEnabled = true;
            }
			
			if (globalCooldownSeconds >= 1)
            {
				newRewardRequest.GlobalCooldownSeconds = globalCooldownSeconds;
				newRewardRequest.IsGlobalCooldownEnabled = true;
            }

			newRewardRequest.ShouldRedemptionsSkipRequestQueue = autoFulfill;
			newRewardRequest.IsUserInputRequired = requireUserInput;

			newRewardRequest.IsEnabled = enabled;

			return newRewardRequest;
		}

		public bool attemptAddRequest(jerpBot aBotBrain, out bool aAlreadyExists)
        {
			aAlreadyExists = false;
			if (!updateInfoTask(aBotBrain))
            {
				try
				{
					TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest createRewardRequest = getCreateRequest();
					Task<TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsResponse> createRewardTask = aBotBrain.twitchAPI.Helix.ChannelPoints.CreateCustomRewardsAsync(aBotBrain.ownerUserID, createRewardRequest);
					createRewardTask.Wait();

					if (createRewardTask.Result == null)
					{
						aBotBrain.logWarningsErrors.writeAndLog("Failed to create channel point reward named: " + title);
						return false;
					}
					else
					{
						rewardID = createRewardTask.Result.Data[0].Id;
						return true;    // Successfully created
					}
				}
				catch (Exception e)
				{
					aBotBrain.logWarningsErrors.writeAndLog(string.Format("Exception when trying to create channel point reward named: \"{0}\": {1}", title, e.Message));
					return false;
				}
			}
			else
            {
				aAlreadyExists = true;
				return true;	// Already exists
            }
        }

		public bool updateInfoTask(jerpBot aBotBrain)
        {
			if (string.IsNullOrEmpty(rewardID))
            {
				Task<TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward.GetCustomRewardsResponse> getRewardsTask = aBotBrain.twitchAPI.Helix.ChannelPoints.GetCustomRewardAsync(aBotBrain.ownerUserID);
				getRewardsTask.Wait();

				if (getRewardsTask.Result != null)
				{
					foreach (TwitchLib.Api.Helix.Models.ChannelPoints.CustomReward curReward in getRewardsTask.Result.Data)
					{
						if (curReward.Title == title)
						{
							rewardID = curReward.Id;
							return true;
						}
					}
				}
			}
			else
            {
				return true;
            }

			return false;
		}

		public bool attemptRemoveRequest(jerpBot aBotBrain, out bool aExisted)
        {
			aExisted = false;
			if (updateInfoTask(aBotBrain))
            {
				try
				{
					Task removeRewardTask = aBotBrain.twitchAPI.Helix.ChannelPoints.DeleteCustomRewardAsync(aBotBrain.ownerUserID, rewardID);
					removeRewardTask.Wait();

					aExisted = true;
					rewardID = null;
					return true;    // Successfully removed
				}
				catch (Exception e)
				{
					aBotBrain.logWarningsErrors.writeAndLog(string.Format("Exception when trying to remove channel point reward named: \"{0}\": {1}", title, e.Message));
					return false;
				}
			}
			aBotBrain.logWarningsErrors.writeAndLog("Unable to find ID for and thus remove reward named: " + title);
			return false;
		}
	}

	class streamProfilesConfig
	{
		public List<string> tagsCommon { get; set; }
		public Dictionary<string, streamProfileEntry> entries { get; set; }
		public Dictionary<string, List<streamProfileReward>> rewardGroups { get; set; }
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

		public void applyProfile(userEntry commandUser, string argumentString)
		{
			if (m_IsLoaded && !string.IsNullOrEmpty(argumentString))
			{
				if (m_Config.entries.ContainsKey(argumentString))
				{
					applyProfileInternal(argumentString);
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
			int countRemoved = 0;
			int countAdded = 0;
			bool rewardExisted;

			// Clear rewards from other groups first
			foreach(string groupKey in m_Config.rewardGroups.Keys)
            {
				if (groupKey != aGroupName)
                {
					foreach (streamProfileReward curReward in m_Config.rewardGroups[groupKey])
                    {
						curReward.attemptRemoveRequest(m_BotBrain, out rewardExisted);
						if (rewardExisted)
							countRemoved++;
                    }
                }
            }

			// Add rewards from new group (if exists)
			if (m_Config.rewardGroups.ContainsKey(aGroupName))
            {
				foreach (streamProfileReward curReward in m_Config.rewardGroups[aGroupName])
                {
					curReward.attemptAddRequest(m_BotBrain, out rewardExisted);
					if (!rewardExisted)
						countAdded++;
                }
			}

			if (!aSilentMode)
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("channelPointRewardsCreatedRemoved"), countAdded, countRemoved));
        }

		public void applyRewardGroup(userEntry commandUser, string argumentString)
        {
			if (m_IsLoaded && !string.IsNullOrEmpty(argumentString))
            {
				applyRewardGroupInternal(argumentString);
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
					m_IsLoaded = true;
				}
			}
		}

		public void reload(userEntry commandUser, string argumentString)
        {
			load();

			if (m_IsLoaded)
            {
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
