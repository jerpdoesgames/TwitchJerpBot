﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

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

        class queueConfigWeightedRandom
        {
            public float subModifier { get; set; }
            public float followModifier { get; set; }
            public float valueBase { get; set; }
            public float valuePerMinute { get; set; }
            public int maxMinutesPassed { get; set; }
        }

        class queueConfig
        {
            public queueConfigWeightedRandom weightedRandom { get; set; }
        }

        class queueData
		{
			public userEntry user;
			public string data;
            public DateTime addTime;
            public int randomWeight;
			public queueData(userEntry aUser, string aData = null)
			{
				user = aUser;
				data = aData;
                addTime = DateTime.Now.ToUniversalTime();
                randomWeight = 0;
            }
		}

        private throttler m_Throttler;
        private readonly object messageLastLock = new object();
        private bool isActive = false;
		private List<queueData> m_EntryList;
		private int m_ListMax = 10;
        private bool m_UpdateImmediately = true;
		private uint m_MaxPerUser = 1;
		private List<queueData> usersAddedRecently;
		private bool userAddedRecently = false;
		private string description;
		private string m_QueueType = QUEUE_TYPE_PLAIN;
		private string m_QueueMode = QUEUE_MODE_NORMAL;
        private bool m_LoadSuccessful = false;
        private queueData m_CurEntry;
        private queueConfig m_config;

        private string joinString()
		{
            if (isActive)
            {
                switch (m_QueueType)
                {
                    case QUEUE_TYPE_MARIOMAKER:
                        return modeString() + m_BotBrain.Localizer.getString("queueJoinHintMarioMaker");
                    case QUEUE_TYPE_MARIOMAKER2:
                        return modeString() + m_BotBrain.Localizer.getString("queueJoinHintMarioMaker2");
                    case QUEUE_TYPE_GENERIC:
                        return modeString() + m_BotBrain.Localizer.getString("queueJoinHintGeneric");
                    default:
                        return modeString() + m_BotBrain.Localizer.getString("queueJoinHintPlain");
                }
            }

            return "";
		}

        private string modeString()
        {
            switch (m_QueueMode)
            {
                case QUEUE_MODE_SUBS:
                    return m_BotBrain.Localizer.getString("queueModeSubOnly") + "  ";
                case QUEUE_MODE_FOLLOWERS:
                    return m_BotBrain.Localizer.getString("queueModeFollowers") + "  ";
                default:
                    return m_BotBrain.Localizer.getString("queueModeAll") + "  ";
            }
        }

		public void reset(bool announce = true)
		{
			m_EntryList.Clear();
			usersAddedRecently.Clear();
			if (announce)
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueReset"));
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
                    return (queueUser.isBroadcaster || queueUser.isModerator || queueUser.isSubscriber || queueUser.isFollower);
                case QUEUE_MODE_SUBS:
                    return (queueUser.isBroadcaster || queueUser.isModerator || queueUser.isSubscriber);
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
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueModeSet"), m_QueueMode) + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueModeSet"), m_QueueMode));
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
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueTypeSet"), m_QueueType) + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueTypeSet"), m_QueueType));
			}
		}

		public void about(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(description))
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueDescriptionDisplay"), description));
            else
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueDescriptionEmpty"));
		}

		public void describe(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				description = argumentString;
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueDescriptionUpdated"));
			}
		}

		public void count(userEntry commandUser, string argumentString)
		{
			int userCount = m_EntryList.Count();

            if (isActive)
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueCountAnnounce"), userCount, m_ListMax, m_MaxPerUser) + "  " + joinString());
			else
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueCountAnnounce"), userCount, m_ListMax, m_MaxPerUser));
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
                            string dataToEnter = argumentString;
                            if (m_QueueType == QUEUE_TYPE_MARIOMAKER || m_QueueType == QUEUE_TYPE_MARIOMAKER2)
                            {
                                dataToEnter = dataToEnter.ToUpper();
                            }
							queueData newData = new queueData(commandUser, dataToEnter);
							m_EntryList.Add(newData);
							usersAddedRecently.Add(newData);
							userAddedRecently = true;

                            if (m_UpdateImmediately)
                            {
                                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueEntrySuccess"), commandUser.Nickname, m_EntryList.Count));
                            }
						}
					}
                    else
                    {
                        m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueEntryFailMaxPerUser"), commandUser.Nickname, m_MaxPerUser));
                    }
				}
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueEntryFailQueueFull"), commandUser.Nickname, m_EntryList.Count));
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
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queuePositionSingle"), commandUser.Nickname, position, totalEntries));
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queuePositionMultiple"), commandUser.Nickname, position));
                }
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueFailUserNotFound"), commandUser.Nickname));
            }
        }

        public void replace(userEntry commandUser, string argumentString)
        {
            if (m_QueueType != QUEUE_TYPE_PLAIN)
            {
                int totalEntries;
                int position = getPosition(commandUser, out totalEntries);

                if (validateUser(commandUser) && totalEntries > 0)
                {
                    if (validateInput(argumentString))
                    {
                        queueData curEntry = m_EntryList[position - 1];
                        curEntry.data = argumentString;

                        if (totalEntries > 1)
                        {
                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueReplaceSuccessMultiple"), commandUser.Nickname, position, totalEntries));
                        }
                        else
                        {
                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueReplaceSuccess"), commandUser.Nickname, position));
                        }
                    } // TODO: Consider message for invalid entries.
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueFailUserNotFound"), commandUser.Nickname));
                }
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
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueOpenedReset") + "  " + newJoinString);
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueOpened") + "  " + newJoinString);
                }

                m_Throttler.trigger();

                isActive = true;
            }
		}

		public void close(userEntry commandUser, string argumentString)
		{
			isActive = false;
			m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueClosed"));
		}

        public void setMaxCount(userEntry commandUser, string argumentString)
        {
            int newListMax;
            if (Int32.TryParse(argumentString, out newListMax))
            {
                m_ListMax = newListMax;
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueMaxEntriesSet"), m_ListMax));
            }
        }

        private string closedMessage(userEntry commandUser)
        {
            
            return string.Format(m_BotBrain.Localizer.getString("queueClosedReply"), commandUser.Nickname);
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

                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueListDisplay"), listString));
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueNoEntries") + "  " + joinString());
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
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueLeaveSingle"), commandUser.Nickname));
            }
            else if (removeCount > 1)
            {
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueLeaveMultiple"), commandUser.Nickname, removeCount));
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueLeaveNotFound"), commandUser.Nickname));
            }
        }

        public void current(userEntry commandUser, string argumentString)
        {
            if (m_CurEntry != null)
            {
                switch (m_QueueType)
                {
                    case QUEUE_TYPE_PLAIN:
                        m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueCurEntryDisplayPlain"), m_CurEntry.user.Nickname));
                        break;
                    case QUEUE_TYPE_MARIOMAKER:
                    case QUEUE_TYPE_MARIOMAKER2:
                        m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueCurEntryDisplayMarioMaker"), m_CurEntry.user.Nickname, m_CurEntry.data));
                        break;
                    case QUEUE_TYPE_GENERIC:
                        m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueCurEntryDisplayGeneric"), m_CurEntry.user.Nickname, m_CurEntry.data));
                        break;
                }
            }
            else
            {
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueCurEntryDisplayEmpty") + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueCurEntryDisplayEmpty") + "  " + m_BotBrain.Localizer.getString("queueClosedOpenToEnter"));
            }
        }

        public void next(userEntry commandUser, string argumentString)
		{
			int userCount = m_EntryList.Count();

			if (userCount > 0)
			{
				queueData nextEntry = m_EntryList[0];
                m_CurEntry = nextEntry;
                m_EntryList.Remove(nextEntry);
                announceSelection(nextEntry);
            } else
			{
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueNoEntries") + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueNoEntries") + "  " + m_BotBrain.Localizer.getString("queueClosedOpenToEnter"));
            }
		}

        private List<queueData> getSubList()
        {
            List<queueData> subList = new List<queueData>();

            foreach (queueData mainListEntry in m_EntryList)
            {
                if (mainListEntry.user.isSubscriber)
                    subList.Add(mainListEntry);
            }

            return subList;
        }

        private void announceSelection(queueData aEntry, string aPrefix = "")
        {
            switch (m_QueueType)
            {
                case QUEUE_TYPE_PLAIN:
                    m_BotBrain.sendDefaultChannelMessage(aPrefix + string.Format(m_BotBrain.Localizer.getString("queueSelectPlain"), aEntry.user.Nickname));
                    break;
                case QUEUE_TYPE_MARIOMAKER:
                case QUEUE_TYPE_MARIOMAKER2:
                    m_BotBrain.sendDefaultChannelMessage(aPrefix + string.Format(m_BotBrain.Localizer.getString("queueSelectMarioMaker"), aEntry.user.Nickname, aEntry.data));
                    break;
                case QUEUE_TYPE_GENERIC:
                    m_BotBrain.sendDefaultChannelMessage(aPrefix + string.Format(m_BotBrain.Localizer.getString("queueSelectGeneric"), aEntry.user.Nickname, aEntry.data));
                    break;
            }
        }

        public void subNext(userEntry commandUser, string argumentString)
        {
            List<queueData> subList = getSubList();
            queueData nextEntry;
            bool foundEntry = false;

            if (subList.Count > 0)
            {
                nextEntry = subList[0];
                foundEntry = true;

                m_CurEntry = nextEntry;
                m_EntryList.Remove(nextEntry);
                announceSelection(nextEntry, m_BotBrain.Localizer.getString("queueSelectNoteSub") + " ");
            }

            if (!foundEntry)
            {
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueNoEntriesSub") +  "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueNoEntriesSub") + "  " + m_BotBrain.Localizer.getString("queueClosedOpenToEnter"));
            }

        }

        public void subRandom(userEntry commandUser, string argumentString)
        {
            int userCount = m_EntryList.Count();
            queueData nextEntry;
            bool foundEntry = false;

            List<queueData> subList = getSubList();

            if (subList.Count > 0)
            {
                int selectID = m_BotBrain.randomizer.Next(0, subList.Count - 1);
                nextEntry = m_EntryList[selectID];
                foundEntry = true;

                m_CurEntry = nextEntry;
                m_EntryList.Remove(nextEntry);

                announceSelection(nextEntry, m_BotBrain.Localizer.getString("queueSelectNoteSubRandom") + " ");
            }

            if (!foundEntry)
            {
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueNoEntriesSub") + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueNoEntriesSub") + "  " + m_BotBrain.Localizer.getString("queueClosedOpenToEnter"));
            }
        }

        public void random(userEntry commandUser, string argumentString)
        {
            int userCount = m_EntryList.Count();

            if (userCount > 0)
            {
                int selectID = m_BotBrain.randomizer.Next(0, userCount - 1);

                queueData nextEntry = m_EntryList[selectID];
                m_CurEntry = nextEntry;
                m_EntryList.Remove(nextEntry);
                announceSelection(nextEntry, m_BotBrain.Localizer.getString("queueSelectNoteRandom") + " ");
            }
            else
            {
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueNoEntries") + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueNoEntries") + "  " + m_BotBrain.Localizer.getString("queueClosedOpenToEnter"));
            }
        }

        float calculateUserWeight(queueData aData)
        {
            float outputWeight  = m_LoadSuccessful ? m_config.weightedRandom.valueBase : 100.0f;

            float valuePerMinute = m_LoadSuccessful ? m_config.weightedRandom.valuePerMinute : 2.0f;
            int maxMinutesPassed = m_LoadSuccessful ? m_config.weightedRandom.maxMinutesPassed : 120;

            DateTime curTime = DateTime.Now.ToUniversalTime();
            TimeSpan timeSinceAdd = curTime.Subtract(aData.addTime);
            int minutesSinceAdd = (int)timeSinceAdd.TotalMinutes;

            outputWeight += (Math.Min(minutesSinceAdd, maxMinutesPassed) * valuePerMinute);

            if (aData.user.isFollower)
                outputWeight *= (m_LoadSuccessful ? m_config.weightedRandom.followModifier : 1.25f);

            if (aData.user.isSubscriber)
                outputWeight *= (m_LoadSuccessful ? m_config.weightedRandom.subModifier : 1.15f);

            return outputWeight;
        }

        public int calculateTotalUserWeight()
        {
            int totalWeight = 0;
            queueData curEntry;

            for (int i = 0; i < m_EntryList.Count; i++)
            {
                curEntry = m_EntryList[i];
                curEntry.randomWeight = (int)Math.Round(calculateUserWeight(m_EntryList[i]));
                totalWeight += curEntry.randomWeight;
            }

            return totalWeight;
        }

        public void weightedRandom(userEntry commandUser, string argumentString)
        {
            int userCount = m_EntryList.Count();

            if (userCount > 0)
            {
                int totalWeight = calculateTotalUserWeight();
                int targValue = m_BotBrain.randomizer.Next(0, totalWeight);
                int curWeight = 0;
                queueData curEntry = m_EntryList[0];

                for (int i = 0; i < m_EntryList.Count; i++)
                {
                    curEntry = m_EntryList[i];
                    if (i == m_EntryList.Count - 1 || (targValue >= curWeight && targValue < (curWeight + curEntry.randomWeight)))
                    {
                        break;  // Found user
                    }
                    curWeight += curEntry.randomWeight;
                }

                m_CurEntry = curEntry;
                m_EntryList.Remove(curEntry);
                announceSelection(curEntry, m_BotBrain.Localizer.getString("queueSelectNoteWeightedRandom"));
            }
            else
            {
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueNoEntries") + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueNoEntries") + "  " + m_BotBrain.Localizer.getString("queueClosedOpenToEnter"));
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
                        string newJoinString = joinString();
                        if (!m_UpdateImmediately && userAddedRecently)
                        {
                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("queueAnnounceEntriesSinceUpdate"), usersAddedRecently.Count) + "  " + newJoinString);
                            usersAddedRecently.Clear();
                            userAddedRecently = false;
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(description))
                                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueAnnounceOpen") + "  " + newJoinString);
                            else
                                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("queueAnnounceOpen") + " (" + description + ")  " + newJoinString);

                        }

                        m_Throttler.trigger();
                    }
                }
			}
		}

        private bool load()
        {
            string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_queuesystem.json");
            if (File.Exists(configPath))
            {
                string queueConfigString = File.ReadAllText(configPath);
                if (!string.IsNullOrEmpty(queueConfigString))
                {
                    m_config = new JavaScriptSerializer().Deserialize<queueConfig>(queueConfigString);
                    return true;
                }
            }
            return false;
        }

		public queueSystem(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
            m_Throttler = new throttler(aJerpBot);
            m_Throttler.waitTimeMax = 120000;

            m_EntryList = new List<queueData>();
			usersAddedRecently = new List<queueData>();

            m_LoadSuccessful = load();

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
            tempDef.addSubCommand(new chatCommandDef("random", random, true, false));
            tempDef.addSubCommand(new chatCommandDef("weightedrandom", weightedRandom, true, false));
            tempDef.addSubCommand(new chatCommandDef("subrandom", subRandom, true, false));
            tempDef.addSubCommand(new chatCommandDef("replace", replace, true, true));
            tempDef.addSubCommand(new chatCommandDef("subNext", subNext, true, false));
            tempDef.addSubCommand(new chatCommandDef("current", current, true, true));
            tempDef.UseGlobalCooldown = false;
			m_BotBrain.addChatCommand(tempDef);

		}
	}
}