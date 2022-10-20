using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
    class raffle : botModule
	{
		private throttler m_Throttler;
        private readonly object messageLastLock = new object();
        private bool m_IsActive = false;
		private Dictionary<string, userEntry> userList;
		private List<userEntry> usersAddedRecently;
		private bool userAddedRecently = false;
		private string m_Description;
		private raffleConfig m_config;
		private bool m_LoadSuccessful;

        class raffleConfigRedemptionReward
        {
			public string title { get; set; }
			public string description { get; set; }
			public int cost { get; set; }
			public string backgroundColor { get; set; }
		}

		class raffleConfig
        {
			public raffleConfigRedemptionReward rewardInfo { get; set; }
			public bool requireRewardRedemptionOnLoad { get; set; }

		}

		private bool m_UsePointRedemption = false;

		private string getJoinString()
        {
			if (m_UsePointRedemption)
				return " " + string.Format(m_BotBrain.localizer.getString("raffleHintJoinReward"), m_config.rewardInfo.title);
			else
				return " " + m_BotBrain.localizer.getString("raffleHintJoin");
		}

		public void about(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(m_Description))
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("raffleDescriptionAnnounce"), m_Description));
			else
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("raffleDescriptionEmpty"));
		}

		private void setUseRedemptions(bool aUseRedemptions, bool aAnnounceUpdate = true)
        {
			if (m_LoadSuccessful)
            {
				m_UsePointRedemption = aUseRedemptions;

				if (aUseRedemptions)
				{
					if (aAnnounceUpdate)
						m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("rafflePointRedemptionEnabled"));

					if (m_IsActive)
                    {
						bool alreadyExists = false;
						checkCreateChannelPointRedemptionReward(out alreadyExists);

						if (alreadyExists)
							updateChannelPointRedemptionRewardEnabled(true);
					}
				}
				else
				{
					if (aAnnounceUpdate)
						m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("rafflePointRedemptionDisabled"));

					if (m_IsActive)
						updateChannelPointRedemptionRewardEnabled(false);
				}
			}
		}

		public void toggleUseRedemptions(userEntry commandUser, string argumentString)
        {
			setUseRedemptions(!m_UsePointRedemption);
		}

		private bool checkCreateChannelPointRedemptionReward(out bool bAlreadyExists)
        {
			bAlreadyExists = false;

			Task<TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward.GetCustomRewardsResponse> getRewardsTask = m_BotBrain.twitchAPI.Helix.ChannelPoints.GetCustomRewardAsync(m_BotBrain.ownerUserID);
			getRewardsTask.Wait();

			if (getRewardsTask.Result != null)
			{
				foreach (TwitchLib.Api.Helix.Models.ChannelPoints.CustomReward curReward in getRewardsTask.Result.Data)
				{
					if (curReward.Title == m_config.rewardInfo.title)
					{
						bAlreadyExists = true;
						return true;  // Already exists
					}
				}
			}

			// Doesn't already exist, create it
			TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest createRewardRequest = new TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest();
			createRewardRequest.Cost = m_config.rewardInfo.cost;
			createRewardRequest.Title = m_config.rewardInfo.title;
			createRewardRequest.Prompt = m_config.rewardInfo.description;
			createRewardRequest.BackgroundColor = m_config.rewardInfo.backgroundColor;
			createRewardRequest.IsEnabled = true;

			try
			{
				Task<TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsResponse> createRewardTask = m_BotBrain.twitchAPI.Helix.ChannelPoints.CreateCustomRewardsAsync(m_BotBrain.ownerUserID, createRewardRequest);
				createRewardTask.Wait();

				if (createRewardTask.Result == null)
				{
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("raffleRewardCreateFail"));
					return false;
				}
				else
				{
					return true;    // Successfully created
				}
			}
			catch (Exception e)
			{
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("raffleRewardCreateFail"));
				return false;
			}
		}

		private bool updateChannelPointRedemptionRewardEnabled(bool aEnabled)
        {
			TwitchLib.Api.Helix.Models.ChannelPoints.CustomReward raffleRedemptionReward = null;

			// grab and store reward ID if it exists
			Task<TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward.GetCustomRewardsResponse> getRewardsTask = m_BotBrain.twitchAPI.Helix.ChannelPoints.GetCustomRewardAsync(m_BotBrain.ownerUserID);
			getRewardsTask.Wait();

			if (getRewardsTask.Result != null)
            {
				foreach (TwitchLib.Api.Helix.Models.ChannelPoints.CustomReward curReward in getRewardsTask.Result.Data)
                {
					if (curReward.Title == m_config.rewardInfo.title)
                    {
						raffleRedemptionReward = curReward;
						break;	
					}
                }
            }

			if (raffleRedemptionReward != null)
			{
				TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardRequest updateRequest = new TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardRequest();
				updateRequest.IsEnabled = aEnabled;

				try
				{
					Task<TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardResponse> updateTask = m_BotBrain.twitchAPI.Helix.ChannelPoints.UpdateCustomRewardAsync(m_BotBrain.ownerUserID, raffleRedemptionReward.Id, updateRequest);
					updateTask.Wait();

					if (updateTask.Result == null)
					{
						m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("raffleRewardUpdateEnabledFail"));
					}
					else
                    {
						return true;
                    }
				}
				catch (Exception e)
				{
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("raffleRewardUpdateEnabledFail"));
				}
			}

			return false;
        }

		public void describe(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				m_Description = argumentString;
                if (m_IsActive)
                {
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("raffleDescriptionSet"));
                }
			}
		}

		private void addUserInternal(userEntry aNewUser)
        {
			if (m_IsActive)
			{
				if (!userList.ContainsKey(aNewUser.Nickname))
				{
					userList.Add(aNewUser.Nickname, aNewUser);
					usersAddedRecently.Add(aNewUser);
					userAddedRecently = true;
				}
			}
		}

		public void addUser(userEntry commandUser, string argumentString)
		{
			if (m_UsePointRedemption)
            {
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("raffleAddUserFailNeedRedemption"), commandUser.Nickname, m_config.rewardInfo.title));
            }
			else
            {
				addUserInternal(commandUser);
            }
		}

		public void resetEntries()
		{
			userList.Clear();
			usersAddedRecently.Clear();
		}

		public void reset(userEntry commandUser, string argumentString)
		{
			resetEntries();
			m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("raffleCleared"));
		}

		public void open(userEntry commandUser, string argumentString)
		{
			bool rewardAlreadyExists;

			lock (messageLastLock)
            {
				if (
					!m_UsePointRedemption ||
					(
						checkCreateChannelPointRedemptionReward(out rewardAlreadyExists) &&
						(!rewardAlreadyExists || updateChannelPointRedemptionRewardEnabled(true))
					)
				)
                {
					// TODO: Handle something like, if the reward exists but it's not required, disable the reward
					resetEntries();
					m_BotBrain.sendDefaultChannelAnnounce(m_BotBrain.localizer.getString("raffleOpenedCleared") + getJoinString());

					m_Throttler.trigger();

					m_IsActive = true;
				}
            }
		}

		public void close(userEntry commandUser, string argumentString)
		{
			m_IsActive = false;
			m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("raffleClosed"));

			if (m_UsePointRedemption)
				updateChannelPointRedemptionRewardEnabled(false);
		}

		public void count(userEntry commandUser, string argumentString)
		{
			int userCount = userList.Count();
			if (m_IsActive)
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("raffleUserCount"), userCount) + getJoinString());
			else
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("raffleUserCount"), userCount));
		}

		public void draw(userEntry commandUser, string argumentString)
		{
			int userCount = userList.Count();

			if (userCount > 0)
			{
				List<string> keyList = Enumerable.ToList(userList.Keys);

				string chosenKey = keyList[m_BotBrain.randomizer.Next(0, userCount - 1)];
				userEntry chosenUser = userList[chosenKey];

				userList.Remove(chosenKey);
				usersAddedRecently.Remove(chosenUser);

				if (usersAddedRecently.Count == 0)
					userAddedRecently = false;

				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("raffleUserSelected"), chosenUser.Nickname));
			}
			else
			{
				if (m_IsActive)
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("raffleCountEmpty") + getJoinString());
				else
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("raffleCountEmpty") + "  " + m_BotBrain.localizer.getString("raffleHintOpen"));
			}

		}
		
		private bool updateRaffleRewardRedemptionStatus(string aRewardID, string aRedemptionID, TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus aStatus)
        {

			List<string> redemptionIDs = new List<string>();
			redemptionIDs.Add(aRedemptionID);

			TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus.UpdateCustomRewardRedemptionStatusRequest updateRequest = new TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus.UpdateCustomRewardRedemptionStatusRequest();
			updateRequest.Status = aStatus;

			try
			{
				Task<TwitchLib.Api.Helix.Models.ChannelPoints.UpdateRedemptionStatus.UpdateRedemptionStatusResponse> refundRedemptionTask = m_BotBrain.twitchAPI.Helix.ChannelPoints.UpdateRedemptionStatusAsync(m_BotBrain.ownerUserID, aRewardID, redemptionIDs, updateRequest);
				refundRedemptionTask.Wait();

				if (refundRedemptionTask.Result != null)
				{
					return true;
				}
                else
                {
					Console.WriteLine("Failed channel point redemption refund request (API)");
					return false;
				}
			}
			catch (Exception e)
			{
				Console.WriteLine("Failed channel point redemption refund request (exception): " + e.Message);
			}

			return false;
        }

		public override void onChannelPointRedemption(userEntry aMessageUser, string aRewardTitle, int aRewardCost, string aRewardUserInput, string aRewardID, string aRedemptionID)
        {
			if (m_LoadSuccessful)
            {
				bool needRefund = false;
				string failReason = "";

				if (aRewardTitle == m_config.rewardInfo.title)
				{
					if (m_UsePointRedemption)
					{
						if (m_IsActive)
						{
							if (!userList.ContainsKey(aMessageUser.Nickname))
							{
								if (updateRaffleRewardRedemptionStatus(aRewardID, aRedemptionID, TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.FULFILLED))
									addUserInternal(aMessageUser);
								else
									m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("raffleRewardRedeemStatusFulfilledFail"), aMessageUser.Nickname));
							}
							else
							{

								failReason = m_BotBrain.localizer.getString("raffleRewardRedeemFailUserExists");
								needRefund = true;
							}
						}
						else
						{
							failReason = m_BotBrain.localizer.getString("raffleRewardRedeemFailInactive");
							needRefund = true;
						}
					}
					else
					{
						failReason = m_BotBrain.localizer.getString("raffleRewardRedeemFailNoRewardRequired");
						needRefund = true;
					}

					if (needRefund)
					{
						m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("raffleRewardRefund"),aMessageUser.Nickname , failReason));

						if (!updateRaffleRewardRedemptionStatus(aRewardID, aRedemptionID, TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.CANCELED))
							m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("raffleRewardRedeemStatusCanceledFail"), aMessageUser.Nickname));
					}
				}
			}
        }

		private bool load()
		{
			string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_raffles.json");
			if (File.Exists(configPath))
			{
				string queueConfigString = File.ReadAllText(configPath);
				if (!string.IsNullOrEmpty(queueConfigString))
				{
					m_config = new JavaScriptSerializer().Deserialize<raffleConfig>(queueConfigString);
					m_LoadSuccessful = true;

					setUseRedemptions(m_config.requireRewardRedemptionOnLoad, false);
					return true;
				}
			}
			return false;
		}

		public override void frame()
		{
			if (m_IsActive)
			{
                lock (messageLastLock)
                {
					if (m_Throttler.isReady)
					{
						if (userAddedRecently)
						{
							m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("raffleAnnounceAddedRecently"), usersAddedRecently.Count) + getJoinString());
							usersAddedRecently.Clear();
							userAddedRecently = false;
						}
						else
							m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("raffleAnnounceOpen") + getJoinString());

						m_Throttler.trigger();
					}
				}
			}
		}

		public raffle(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			m_Throttler = new throttler(aJerpBot);
			m_Throttler.waitTimeMax = 15000;
			m_Throttler.lineCountMinimum = 8;
			m_Throttler.messagesReduceTimer = false;

			userList = new Dictionary<string, userEntry>();
			usersAddedRecently = new List<userEntry>();

			load();

			chatCommandDef tempDef = new chatCommandDef("raffle", addUser, true, true);
			tempDef.addSubCommand(new chatCommandDef("open", open, true, false));
			tempDef.addSubCommand(new chatCommandDef("close", close, true, false));
			tempDef.addSubCommand(new chatCommandDef("clear", reset, true, false));
			tempDef.addSubCommand(new chatCommandDef("draw", draw, true, false));
			tempDef.addSubCommand(new chatCommandDef("count", count, true, false));
			tempDef.addSubCommand(new chatCommandDef("describe", describe, true, false));
			tempDef.addSubCommand(new chatCommandDef("about", about, true, true));
			tempDef.addSubCommand(new chatCommandDef("useredemptions", toggleUseRedemptions, false, false));
			tempDef.useGlobalCooldown = false;
			m_BotBrain.addChatCommand(tempDef);

		}

	}
}
