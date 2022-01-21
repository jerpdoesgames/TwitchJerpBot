using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
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
		public int globalCooldown { get; set; }
		public int maxPerUserPerStream { get; set; }
		public bool requireUserInput { get; set; }
		public bool autoFulfill { get; set; }
		public bool enabled { get; set; }
		public string rewardID { get; set; }	// ID on Twitch

		public streamProfileReward()
        {
			cost = 1;
			maxPerStream = -1;
			globalCooldown = -1;
			maxPerUserPerStream = -1;
			enabled = true;
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
			
			if (globalCooldown >= 1)
            {
				newRewardRequest.GlobalCooldownSeconds = globalCooldown;
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
					Task<TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsResponse> createRewardTask = aBotBrain.twitchAPI.Helix.ChannelPoints.CreateCustomRewardsAsync(aBotBrain.OwnerID, createRewardRequest);
					createRewardTask.Wait();

					if (createRewardTask.Result == null)
					{
						Console.WriteLine("Failed to create channel point reward named: " + title);
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
					Console.WriteLine(string.Format("Exception when trying to create channel point reward named: \"{0}\": {1}", title, e.Message));
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
				Task<TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward.GetCustomRewardsResponse> getRewardsTask = aBotBrain.twitchAPI.Helix.ChannelPoints.GetCustomRewardAsync(aBotBrain.OwnerID);
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
					Task removeRewardTask = aBotBrain.twitchAPI.Helix.ChannelPoints.DeleteCustomRewardAsync(aBotBrain.OwnerID, rewardID);
					removeRewardTask.Wait();

					aExisted = true;
					rewardID = null;
					return true;    // Successfully removed
				}
				catch (Exception e)
				{
					Console.WriteLine(string.Format("Exception when trying to remove channel point reward named: \"{0}\": {1}", title, e.Message));
					return false;
				}
			}
			Console.WriteLine("Unable to find ID for and thus remove reward named: " + title);
			return false;
		}
	}

	class streamProfilesConfig
	{
		public List<string> tagsCommon { get; set; }
		public Dictionary<string, streamProfileEntry> entries { get; set; }
		public Dictionary<string, List<streamProfileReward>> rewardGroups { get; set; }
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
					if (!string.IsNullOrEmpty(useProfile.rewardGroup))
                    {
						applyRewardGroupInternal(useProfile.rewardGroup);
                    }
				}
				else
                {
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("modifyChannelInfoFailProfileNotFound"));
                }
			}
		}

		public void applyRewardGroupInternal(string aGroupName)
        {
			int countRemoved = 0;
			int countAdded = 0;
			bool rewardExisted;

			// Clear rewards from other groups first
			foreach(string groupKey in configData.rewardGroups.Keys)
            {
				if (groupKey != aGroupName)
                {
					foreach (streamProfileReward curReward in configData.rewardGroups[groupKey])
                    {
						curReward.attemptRemoveRequest(m_BotBrain, out rewardExisted);
						if (rewardExisted)
							countRemoved++;
                    }
                }
            }

			// Add rewards from new group (if exists)
			if (configData.rewardGroups.ContainsKey(aGroupName))
            {
				foreach (streamProfileReward curReward in configData.rewardGroups[aGroupName])
                {
					curReward.attemptAddRequest(m_BotBrain, out rewardExisted);
					if (!rewardExisted)
						countAdded++;
                }
			}

			m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("channelPointRewardsCreatedRemoved"), countAdded, countRemoved));
        }

		public void applyRewardGroup(userEntry commandUser, string argumentString)
        {
			if (!string.IsNullOrEmpty(argumentString))
            {
				applyRewardGroupInternal(argumentString);
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
				chatCommandDef tempDef = new chatCommandDef("profile", null, false, false);
				tempDef.addSubCommand(new chatCommandDef("apply", applyProfile, false, false));
				tempDef.addSubCommand(new chatCommandDef("setrewards", applyRewardGroup, false, false));

				m_BotBrain.addChatCommand(tempDef);
			}
		}
	}
}
