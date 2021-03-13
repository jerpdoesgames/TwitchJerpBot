using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace JerpDoesBots
{
	class raffle : botModule
	{
		private long messageThrottle = 15000;
		private long messageLast = 0;
        private readonly object messageLastLock = new object();
        private bool isActive = false;
		private Dictionary<string, userEntry> userList;
		private List<userEntry> usersAddedRecently;
		private bool userAddedRecently = false;
		private long lastLineCount = -2;
		private long lineCountMinimum = 8;
		string description;

		public void about(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(description))
				m_BotBrain.sendDefaultChannelMessage("This raffle: " + description);
			else
				m_BotBrain.sendDefaultChannelMessage("This raffle has not yet been described");
		}

		public void describe(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				description = argumentString;
                if (isActive)
                {
                    m_BotBrain.sendDefaultChannelMessage("Raffle description updated.");
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
			m_BotBrain.sendDefaultChannelMessage("Raffle has been cleared.");
		}

		public void open(userEntry commandUser, string argumentString)
		{
            lock(messageLastLock)
            {
                resetEntries();
                m_BotBrain.sendDefaultChannelMessage("Raffle has been reset and opened.  Type !raffle to enter.");

                messageLast = m_BotBrain.ActionTimer.ElapsedMilliseconds;

                isActive = true;
            }

		}

		public void close(userEntry commandUser, string argumentString)
		{
			isActive = false;
			m_BotBrain.sendDefaultChannelMessage("Raffle closed.");
		}

		public void count(userEntry commandUser, string argumentString)
		{
			int userCount = userList.Count();
			if (isActive)
				m_BotBrain.sendDefaultChannelMessage("There are " + userCount + " user(s) in the raffle.  Type !raffle to join.");
			else
				m_BotBrain.sendDefaultChannelMessage("There are " + userCount + " user(s) in the raffle.");
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

				m_BotBrain.sendDefaultChannelMessage(chosenUser.Nickname + " has been chosen and removed from the list.");
			} else
			{
				if (isActive)
					m_BotBrain.sendDefaultChannelMessage("No users are in this raffle. People need to type !raffle to join");
				else
					m_BotBrain.sendDefaultChannelMessage("No users are in this raffle. How about opening it back up again so people can enter?");
			}

		}

		public override void frame()
		{
			if (isActive)
			{
                lock (messageLastLock)
                {
                    if (m_BotBrain.LineCount > lastLineCount + lineCountMinimum)
                    {
                        if (m_BotBrain.ActionTimer.ElapsedMilliseconds > messageLast + messageThrottle)
                        {
                            if (userAddedRecently)
                            {
                                m_BotBrain.sendDefaultChannelMessage(usersAddedRecently.Count + " user(s) have been added to the raffle since last update.  Type !raffle to be added.");
                                usersAddedRecently.Clear();
                                userAddedRecently = false;
                            }
                            else
                                m_BotBrain.sendDefaultChannelMessage("A raffle is currently open.  Type !raffle to be added.");

                            messageLast = m_BotBrain.ActionTimer.ElapsedMilliseconds;
                            lastLineCount = m_BotBrain.LineCount;
                        }

                    }
                }
			}
		}

		public raffle(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
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
