using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace JerpDoesBots
{

	class queueSystem : botModule
	{
		public const string QUEUE_MODE_NORMAL		= "all";
		public const string QUEUE_MODE_FOLLOWERS	= "followers";
		public const string QUEUE_MODE_SUBS	    	= "subs";

		public const string QUEUE_TYPE_PLAIN		= "plain";
		public const string QUEUE_TYPE_GENERIC		= "generic";
		public const string QUEUE_TYPE_MARIOMAKER	= "mariomaker";
        public const string QUEUE_TYPE_MARIOMAKER2  = "mariomaker2";

        public Regex marioMakerCodeReg = new Regex(@"^[a-zA-Z0-9]{4}-[a-zA-Z0-9]{4}-[a-zA-Z0-9]{4}-[a-zA-Z0-9]{4}$");
        public Regex marioMaker2CodeReg = new Regex(@"^[a-zA-Z0-9]{3}-[a-zA-Z0-9]{3}-[a-zA-Z0-9]{3}$");

        public struct queueData
		{
			public userEntry user;
			public string data;

			public queueData(userEntry aUser, string aData = null)
			{
				user = aUser;
				data = aData;
			}
		}

		private long messageThrottle = 120000;
		private long messageLast = 0;
        private readonly object messageLastLock = new object();
        private bool isActive = false;
		private List<queueData> m_EntryList;
		private int m_ListMax = 10;
        private bool m_UpdateImmediately = true;

		private uint m_MaxPerUser = 1;

		private List<queueData> usersAddedRecently;
		private bool userAddedRecently = false;
		private long lastLineCount = -2;
		private long lineCountMinimum = 8;

		private string description;
		private string m_QueueType = QUEUE_TYPE_PLAIN;
		private string m_QueueMode = QUEUE_MODE_NORMAL;

		private string joinString()
		{
            if (isActive)
            {
                switch (m_QueueType)
                {
                    case QUEUE_TYPE_MARIOMAKER:
                        return modeString() + "type !queue ####-####-####-#### to enter.";
                    case QUEUE_TYPE_MARIOMAKER2:
                        return modeString() + "type !queue ###-###-### to enter.";
                    case QUEUE_TYPE_GENERIC:
                        return modeString() + "!queue [message] to enter.";
                    default:
                        return modeString() + "type !queue to enter.";
                }
            }

            return "";
		}

        private string modeString()
        {
            switch (m_QueueMode)
            {
                case QUEUE_MODE_SUBS:
                    return "Queue is open to subs & mods only.  ";
                case QUEUE_MODE_FOLLOWERS:
                    return "Queue is open to followers, subs, & mods.  ";
                default:
                    return "Queue is open to all viewers.  ";
            }
        }

		public void reset(bool announce = true)
		{
			m_EntryList.Clear();
			usersAddedRecently.Clear();
			if (announce)
				m_BotBrain.sendDefaultChannelMessage("queue has been reset.");
		}

		public void resetEntries(userEntry commandUser, string argumentString)
		{
			reset(true);
		}

        public bool validateUser(userEntry queueUser)
        {
            switch(m_QueueMode)
            {
                case QUEUE_MODE_FOLLOWERS:
                    return (queueUser.IsBroadcaster || queueUser.IsModerator || queueUser.IsSubscriber || queueUser.IsFollower);
                case QUEUE_MODE_SUBS:
                    return (queueUser.IsBroadcaster || queueUser.IsModerator || queueUser.IsSubscriber);
                default:
                    return true;
            }
        }

		public bool validateInput(string input)
		{
			bool isValid = false;
			switch (m_QueueType)
			{
				case QUEUE_TYPE_PLAIN:
					isValid = true;
					break;
				case QUEUE_TYPE_MARIOMAKER:
					if (marioMakerCodeReg.IsMatch(input))
						isValid = true;
					break;
                case QUEUE_TYPE_MARIOMAKER2:
                    if (marioMaker2CodeReg.IsMatch(input))
                        isValid = true;
                    break;
                case QUEUE_TYPE_GENERIC:
					isValid = !string.IsNullOrEmpty(input);
					break;
			}

			return isValid;
		}

        public void setMode(userEntry commandUser, string argumentString)
        {
            bool isValid = false;

            if (!string.IsNullOrEmpty(argumentString))
            {
                switch(argumentString)
                {
                    case QUEUE_MODE_SUBS:
                        isValid = true;
                        break;
                    case QUEUE_MODE_FOLLOWERS:
                        isValid = true;
                        break;
                    case QUEUE_MODE_NORMAL:
                        isValid = true;
                        break;
                    default:
                        isValid = false;
                        break;
                }
            }

            if (isValid)
            {
                reset(false);
                m_QueueMode = argumentString;

                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage("Queue cleared and set to mode '" + m_QueueMode + "' - " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage("Queue cleared and set to mode '" + m_QueueMode + "'");
            }
        }


        public void setType(userEntry commandUser, string argumentString)
		{
			bool isValid = false;

			if (!string.IsNullOrEmpty(argumentString))
			{

				switch (argumentString)
				{
					case QUEUE_TYPE_PLAIN:
					case QUEUE_TYPE_MARIOMAKER:
                    case QUEUE_TYPE_MARIOMAKER2:
                    case QUEUE_TYPE_GENERIC:
						isValid = true;
						break;
				}
			}

			if (isValid)
			{
				reset(false);
				m_QueueType = argumentString;

				if (isActive)
					m_BotBrain.sendDefaultChannelMessage("Queue cleared and set to type '" + m_QueueType + "' - " + joinString());
				else
					m_BotBrain.sendDefaultChannelMessage("Queue cleared and set to type '" + m_QueueType + "'");
			}
		}

		public void about(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(description))
				m_BotBrain.sendDefaultChannelMessage("This Queue: " + description);
			else
				m_BotBrain.sendDefaultChannelMessage("This queue has not yet been described");
		}

		public void describe(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				description = argumentString;
				m_BotBrain.sendDefaultChannelMessage("Queue description updated.");
			}
		}

		public void count(userEntry commandUser, string argumentString)
		{
			int userCount = m_EntryList.Count();
			if (isActive)
				m_BotBrain.sendDefaultChannelMessage("There are " + userCount + " entries in the queue.  (" + m_ListMax + " max, " + m_MaxPerUser + " per user)  " + joinString());
			else
				m_BotBrain.sendDefaultChannelMessage("There are " + userCount + " entries in the queue.  (" + m_ListMax + " max, " + m_MaxPerUser + " per user)  ");
		}

		public void enter(userEntry commandUser, string argumentString)
		{
			if (isActive)
			{
				int existsCount = 0;
				foreach (queueData queueEntry in m_EntryList)
				{
					if (queueEntry.user.Nickname == commandUser.Nickname)
					{
						existsCount++;
					}
				}

				if (m_EntryList.Count < m_ListMax)
				{
					if (existsCount < m_MaxPerUser)
					{
						if (validateUser(commandUser) && validateInput(argumentString))
						{
							queueData newData = new queueData(commandUser, argumentString);
							m_EntryList.Add(newData);
							usersAddedRecently.Add(newData);
							userAddedRecently = true;

                            if (m_UpdateImmediately)
                            {
                                m_BotBrain.sendDefaultChannelMessage(commandUser.Nickname + ": new entry added in position " + m_EntryList.Count);
                            }
						}
					}
                    else
                    {
                        m_BotBrain.sendDefaultChannelMessage(commandUser.Nickname + ": Max allowable entries reached! (" + m_MaxPerUser + " allowed per user.)");
                    }
				}
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(commandUser.Nickname + ": The queue is full!  Please try again later. (" + m_EntryList.Count + " entries total.)");
                }
			}
            else
            {
                outputGenericClosedMessage(commandUser);
            }
		}

        private int getPosition(userEntry commandUser, out int totalEntries)
        {
            int curPos = 0;
            int matchCount = 0;
            int posFirst = -1;
            foreach (queueData curEntry in m_EntryList)
            {
                curPos++;
                if (curEntry.user == commandUser)
                {
                    if (posFirst == -1)
                        posFirst = curPos;

                    matchCount++;
                }
            }

            totalEntries = matchCount;
            return posFirst;
        }

        public void position(userEntry commandUser, string argumentString)
        {
            int totalEntries;
            int position = getPosition(commandUser, out totalEntries);

            if (totalEntries > 0)
            {
                if (totalEntries > 1)
                {
                    m_BotBrain.sendDefaultChannelMessage(commandUser.Nickname + "'s next entry is in position " + position + ".  They have " + totalEntries + " entries total.");
                }
                {
                    m_BotBrain.sendDefaultChannelMessage(commandUser.Nickname + "'s in position " + position);
                }
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage("Could not find " + commandUser.Nickname + " in the queue.");
            }
        }

        public void open(userEntry commandUser, string argumentString)
		{
            lock(messageLastLock)
            {
                bool resetEntries = false;
                if (!string.IsNullOrEmpty(argumentString) && argumentString == "reset")
                    resetEntries = true;

                string newJoinString = joinString();
                if (resetEntries)
                {
                    reset(false);
                    m_BotBrain.sendDefaultChannelMessage("Queue has been reset and opened.  " + newJoinString);
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage("Queue has been opened.  " + newJoinString);
                }

                messageLast = m_BotBrain.ActionTimer.ElapsedMilliseconds;

                isActive = true;
            }
		}

		public void close(userEntry commandUser, string argumentString)
		{
			isActive = false;
			m_BotBrain.sendDefaultChannelMessage("Queue closed.");
		}

        public void setMaxCount(userEntry commandUser, string argumentString)
        {
            int newListMax;
            if (Int32.TryParse(argumentString, out newListMax))
            {
                m_ListMax = newListMax;
                m_BotBrain.sendDefaultChannelMessage("Max entries set to " + m_ListMax);
            }
        }

        private string closedMessage(userEntry commandUser)
        {
            return "Sorry " + commandUser.Nickname + ", no queue is currently open.";
        }

        private void outputGenericClosedMessage(userEntry commandUser)
        {
            m_BotBrain.sendDefaultChannelMessage(closedMessage(commandUser));
        }

        private string getEntryString(queueData queueEntry, int position)
        {
            switch (m_QueueType)
            {
                case QUEUE_TYPE_MARIOMAKER:
                case QUEUE_TYPE_MARIOMAKER2:
                    return position + ") " + queueEntry.user.Nickname + " ["+ queueEntry.data +"]";
                case QUEUE_TYPE_GENERIC:
                    string appendEllipses = "";

                    if (queueEntry.data.Length > 10)
                        appendEllipses = "...";

                    return position + ") " + queueEntry.user.Nickname + " [" + queueEntry.data.Substring(0, Math.Min(queueEntry.data.Length, 10)) + appendEllipses + "]";
            }

            // case QUEUE_TYPE_PLAIN:
            return position + ") " + queueEntry.user.Nickname;
        }

        public void list(userEntry commandUser, string argumentString)
        {

            if (m_EntryList.Count > 0)
            {
                int curPos = 0;
                string listString = "";
                foreach (queueData curEntry in m_EntryList)
                {
                    curPos++;

                    if (curPos > 1)
                        listString += ", ";

                    listString += getEntryString(curEntry, curPos);
                }

                m_BotBrain.sendDefaultChannelMessage("Queue Entries: " + listString);
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage("No entries in the queue.  " + joinString());
            }

        }

        public void leave(userEntry commandUser, string argumentString)
        {
            int removeCount = 0;
            foreach (queueData queueEntry in m_EntryList.ToList())
            {
                if (queueEntry.user.Nickname == commandUser.Nickname)
                {
                    m_EntryList.Remove(queueEntry);
                    removeCount++;
                }
            }
            if (removeCount == 1)
            {
                m_BotBrain.sendDefaultChannelMessage(commandUser.Nickname + " has been removed from the queue.");
            }
            else if (removeCount > 1)
            {
                m_BotBrain.sendDefaultChannelMessage(commandUser.Nickname + " has been removed from the queue ("+removeCount+" entries total).");
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(commandUser.Nickname + " doesn't appear to be in the queue.");
            }
        }

        public void next(userEntry commandUser, string argumentString)
		{
			int userCount = m_EntryList.Count();

			if (userCount > 0)
			{
				queueData nextEntry = m_EntryList[0];
                m_EntryList.Remove(nextEntry);
				switch (m_QueueType)
				{
					case QUEUE_TYPE_PLAIN:
						m_BotBrain.sendDefaultChannelMessage(nextEntry.user.Nickname + " has been chosen and removed from the queue.");
						break;
					case QUEUE_TYPE_MARIOMAKER:
                    case QUEUE_TYPE_MARIOMAKER2:
                        m_BotBrain.sendDefaultChannelMessage(nextEntry.user.Nickname + " has been chosen with level code: " + nextEntry.data);
						break;
					case QUEUE_TYPE_GENERIC:
						m_BotBrain.sendDefaultChannelMessage(nextEntry.user.Nickname + " has been chosen: " + nextEntry.data);
						break;
				}
			} else
			{
				if (isActive)
					m_BotBrain.sendDefaultChannelMessage("No entries in this queue.  " + joinString());
				else
					m_BotBrain.sendDefaultChannelMessage("No entries in the queue. How about opening it back up again so people can enter?");
			}
		}

        private List<queueData> getSubList()
        {
            List<queueData> subList = new List<queueData>();

            foreach (queueData mainListEntry in m_EntryList)
            {
                if (mainListEntry.user.IsSubscriber)
                    subList.Add(mainListEntry);
            }

            return subList;
        }


        public void subNext(userEntry commandUser, string argumentString)
        {
            List<queueData> subList = getSubList();
            queueData nextEntry = new queueData(); // Cannot be null
            bool foundEntry = false;

            if (subList.Count > 0)
            {
                nextEntry = subList[0];
                foundEntry = true;
            }

            if (foundEntry)
            {
                m_EntryList.Remove(nextEntry);

                switch (m_QueueType)
                {
                    case QUEUE_TYPE_PLAIN:
                        m_BotBrain.sendDefaultChannelMessage(nextEntry.user.Nickname + " has been chosen and removed from the queue.");
                        break;
                    case QUEUE_TYPE_MARIOMAKER:
                    case QUEUE_TYPE_MARIOMAKER2:
                        m_BotBrain.sendDefaultChannelMessage(nextEntry.user.Nickname + " has been chosen with level code: " + nextEntry.data);
                        break;
                    case QUEUE_TYPE_GENERIC:
                        m_BotBrain.sendDefaultChannelMessage(nextEntry.user.Nickname + " has been chosen: " + nextEntry.data);
                        break;
                }
            }
            else
            {
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage("No subscriber entries in this queue.  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage("No subscriber entries in the queue. How about opening it back up again so people can enter?");
            }

        }

        public void subRandom(userEntry commandUser, string argumentString)
        {
            int userCount = m_EntryList.Count();
            queueData nextEntry = new queueData(); // Cannot be null
            bool foundEntry = false;

            List<queueData> subList = getSubList();

            if (subList.Count > 0)
            {
                int selectID = m_BotBrain.randomizer.Next(0, subList.Count - 1);
                nextEntry = m_EntryList[selectID];
                foundEntry = true;
            }

            if (foundEntry)
            {
                m_EntryList.Remove(nextEntry);

                switch (m_QueueType)
                {
                    case QUEUE_TYPE_PLAIN:
                        m_BotBrain.sendDefaultChannelMessage(nextEntry.user.Nickname + " has been randomly chosen and removed from the queue.");
                        break;
                    case QUEUE_TYPE_MARIOMAKER:
                    case QUEUE_TYPE_MARIOMAKER2:
                        m_BotBrain.sendDefaultChannelMessage(nextEntry.user.Nickname + " has been randomly chosen with level code: " + nextEntry.data);
                        break;
                    case QUEUE_TYPE_GENERIC:
                        m_BotBrain.sendDefaultChannelMessage(nextEntry.user.Nickname + " has been randomly chosen: " + nextEntry.data);
                        break;
                }
            }
            else
            {
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage("No subscriber entries in this queue.  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage("No subscriber entries in the queue. How about opening it back up again so people can enter?");
            }
        }

        public void random(userEntry commandUser, string argumentString)
        {
            int userCount = m_EntryList.Count();

            if (userCount > 0)
            {
                int selectID = m_BotBrain.randomizer.Next(0, userCount - 1);

                queueData nextEntry = m_EntryList[selectID];
                m_EntryList.Remove(nextEntry);
                switch (m_QueueType)
                {
                    case QUEUE_TYPE_PLAIN:
                        m_BotBrain.sendDefaultChannelMessage(nextEntry.user.Nickname + " has been randomly chosen and removed from the queue.");
                        break;
                    case QUEUE_TYPE_MARIOMAKER:
                    case QUEUE_TYPE_MARIOMAKER2:
                        m_BotBrain.sendDefaultChannelMessage(nextEntry.user.Nickname + " has been randomly chosen with level code: " + nextEntry.data);
                        break;
                    case QUEUE_TYPE_GENERIC:
                        m_BotBrain.sendDefaultChannelMessage(nextEntry.user.Nickname + " has been randomly chosen: " + nextEntry.data);
                        break;
                }
            }
            else
            {
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage("No entries in this queue.  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage("No entries in the queue. How about opening it back up again so people can enter?");
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
                            string newJoinString = joinString();
                            if (!m_UpdateImmediately && userAddedRecently)
                            {
                                m_BotBrain.sendDefaultChannelMessage(usersAddedRecently.Count + " entries have been added to the queue since last update.  " + newJoinString);
                                usersAddedRecently.Clear();
                                userAddedRecently = false;
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(description))
                                    m_BotBrain.sendDefaultChannelMessage("A queue is currently open.  " + newJoinString);
                                else
                                    m_BotBrain.sendDefaultChannelMessage("A queue is currently open. (" + description + ")  " + newJoinString);

                            }

                            messageLast = m_BotBrain.ActionTimer.ElapsedMilliseconds;
                            lastLineCount = m_BotBrain.LineCount;
                        }
                    }
                }
			}
		}

		public queueSystem(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			m_EntryList = new List<queueData>();
			usersAddedRecently = new List<queueData>();

			chatCommandDef tempDef = new chatCommandDef("queue", enter, true, true);
			tempDef.addSubCommand(new chatCommandDef("open", open, true, false));
			tempDef.addSubCommand(new chatCommandDef("close", close, true, false));
			tempDef.addSubCommand(new chatCommandDef("reset", resetEntries, true, false));
			tempDef.addSubCommand(new chatCommandDef("about", about, true, true));
            tempDef.addSubCommand(new chatCommandDef("position", position, true, true));
            tempDef.addSubCommand(new chatCommandDef("describe", describe, true, false));
			tempDef.addSubCommand(new chatCommandDef("next", next, true, false));
			tempDef.addSubCommand(new chatCommandDef("type", setType, true, false));
			tempDef.addSubCommand(new chatCommandDef("count", count, true, false));
            tempDef.addSubCommand(new chatCommandDef("mode", setMode, true, false));
            tempDef.addSubCommand(new chatCommandDef("setmax", setMaxCount, true, false));
            tempDef.addSubCommand(new chatCommandDef("list", list, true, true));
            tempDef.addSubCommand(new chatCommandDef("leave", leave, true, true));
            tempDef.addSubCommand(new chatCommandDef("random", random, true, true));
            tempDef.addSubCommand(new chatCommandDef("subrandom", subRandom, true, true));
            tempDef.addSubCommand(new chatCommandDef("subNext", subNext, true, true));
            tempDef.UseGlobalCooldown = false;
			m_BotBrain.addChatCommand(tempDef);

		}
	}
}
