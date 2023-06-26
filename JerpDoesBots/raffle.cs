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

		class raffleConfig
        {
			public pointReward rewardInfo { get; set; }
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

		public void about(userEntry commandUser, string argumentString, bool aSilent = false)
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
				m_config.rewardInfo.enabled = m_UsePointRedemption && m_IsActive;
                createUpdateChannelPointRedemptionReward();

                if (aUseRedemptions)
				{
					if (aAnnounceUpdate)
						m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("rafflePointRedemptionEnabled"));
				}
				else
				{
					if (aAnnounceUpdate)
						m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("rafflePointRedemptionDisabled"));
				}
			}
		}

		public void toggleUseRedemptions(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			setUseRedemptions(!m_UsePointRedemption, !aSilent);
		}

		private void createUpdateChannelPointRedemptionReward()
        {
			m_config.rewardInfo = pointRewardManager.addUpdatePointReward(m_config.rewardInfo);
			pointRewardManager.updateRemoteRewardsFromLocalData();
		}

		public void describe(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				m_Description = argumentString;
                if (m_IsActive)
                {
					if (!aSilent)
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

		public void addUser(userEntry commandUser, string argumentString, bool aSilent = false)
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

		public void reset(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			resetEntries();
			if (!aSilent)
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("raffleCleared"));
		}

		public void open(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			lock (messageLastLock)
            {
				m_config.rewardInfo.enabled = m_UsePointRedemption;
				pointRewardManager.updateRemoteRewardsFromLocalData();

                resetEntries();

				if (!aSilent)
					m_BotBrain.sendDefaultChannelAnnounce(m_BotBrain.localizer.getString("raffleOpenedCleared") + getJoinString());

                m_Throttler.trigger();

                m_IsActive = true;
            }
		}

		public void close(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			m_IsActive = false;

			if (!aSilent)
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("raffleClosed"));

			m_config.rewardInfo.enabled = false;
            createUpdateChannelPointRedemptionReward();

        }

		public void count(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			int userCount = userList.Count();

			if (m_IsActive)
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("raffleUserCount"), userCount) + getJoinString());
			else
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("raffleUserCount"), userCount));
		}

		public void draw(userEntry commandUser, string argumentString, bool aSilent = false)
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
								if (pointRewardManager.updateRewardRedemptionStatus(aRewardID, aRedemptionID, TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.FULFILLED))
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

						if (!pointRewardManager.updateRewardRedemptionStatus(aRewardID, aRedemptionID, TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.CANCELED))
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
					m_config.rewardInfo.shouldExistOnTwitch = true;
					m_config.rewardInfo.enabled = false;
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
