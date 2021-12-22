using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace JerpDoesBots
{
	class raffle : botModule
	{
		private throttler m_Throttler;
        private readonly object messageLastLock = new object();
        private bool isActive = false;
		private Dictionary<string, userEntry> userList;
		private List<userEntry> usersAddedRecently;
		private bool userAddedRecently = false;
		string description;
		string m_SharedJoinSuffix;

		public void about(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(description))
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("raffleDescriptionAnnounce"), description));
			else
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("raffleDescriptionEmpty"));
		}

		public void describe(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				description = argumentString;
                if (isActive)
                {
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("raffleDescriptionSet"));
                }
			}
		}

		public void addUser(userEntry commandUser, string argumentString)
		{
			if (isActive)
			{
				if (!userList.ContainsKey(commandUser.Nickname))
				{
					userList.Add(commandUser.Nickname, commandUser);
					usersAddedRecently.Add(commandUser);
					userAddedRecently = true;
				}
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
			m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("raffleCleared"));
		}

		public void open(userEntry commandUser, string argumentString)
		{
            lock(messageLastLock)
            {
                resetEntries();
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("raffleOpenedCleared") + m_SharedJoinSuffix);

				m_Throttler.trigger();

                isActive = true;
            }

		}

		public void close(userEntry commandUser, string argumentString)
		{
			isActive = false;
			m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("raffleClosed"));
		}

		public void count(userEntry commandUser, string argumentString)
		{
			int userCount = userList.Count();
			if (isActive)
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("raffleUserCount"), userCount) + m_SharedJoinSuffix);
			else
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("raffleUserCount"), userCount));
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

				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("raffleUserSelected"), chosenUser.Nickname));
			}
			else
			{
				if (isActive)
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("raffleCountEmpty") + m_SharedJoinSuffix);
				else
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("raffleCountEmpty") + "  " + m_BotBrain.Localizer.getString("raffleHintOpen"));
			}

		}

		public override void frame()
		{
			if (isActive)
			{
                lock (messageLastLock)
                {
					if (m_Throttler.isReady)
					{
						if (userAddedRecently)
						{
							m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("raffleAnnounceAddedRecently"), usersAddedRecently.Count) + m_SharedJoinSuffix);
							usersAddedRecently.Clear();
							userAddedRecently = false;
						}
						else
							m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("raffleAnnounceOpen") + m_SharedJoinSuffix);

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

			m_SharedJoinSuffix = "  " + m_BotBrain.Localizer.getString("raffleHintJoin");

			userList = new Dictionary<string, userEntry>();
			usersAddedRecently = new List<userEntry>();

			chatCommandDef tempDef = new chatCommandDef("raffle", addUser, true, true);
			tempDef.addSubCommand(new chatCommandDef("open", open, true, false));
			tempDef.addSubCommand(new chatCommandDef("close", close, true, false));
			tempDef.addSubCommand(new chatCommandDef("clear", reset, true, false));
			tempDef.addSubCommand(new chatCommandDef("draw", draw, true, false));
			tempDef.addSubCommand(new chatCommandDef("count", count, true, false));
			tempDef.addSubCommand(new chatCommandDef("describe", describe, true, false));
			tempDef.addSubCommand(new chatCommandDef("about", about, true, true));
			tempDef.UseGlobalCooldown = false;
			m_BotBrain.addChatCommand(tempDef);

		}

	}
}
