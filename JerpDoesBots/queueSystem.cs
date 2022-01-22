using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
    class queueData
    {
        public userEntry user;
        public string data;
        public DateTime addTime;
        public int randomWeight;
        private marioMakerLevelInfo m_LevelInfo;

        public marioMakerLevelInfo levelInfo
        {
            get
            {
                if (m_LevelInfo == null && !string.IsNullOrEmpty(data))
                {
                    m_LevelInfo = marioMakerAPI.getLevelInfo(data);
                    return m_LevelInfo;
                }
                return null;
            }
        }

        public queueData(userEntry aUser, string aData = null)
        {
            user = aUser;
            data = aData;
            addTime = DateTime.Now.ToUniversalTime();
            randomWeight = 0;
        }
    }

    class queueSystem : botModule
    {
        public const string QUEUE_MODE_NORMAL = "all";
        public const string QUEUE_MODE_FOLLOWERS = "followers";
        public const string QUEUE_MODE_SUBS = "subs";

        public const string QUEUE_TYPE_PLAIN = "plain";
        public const string QUEUE_TYPE_GENERIC = "generic";
        public const string QUEUE_TYPE_MARIOMAKER = "mariomaker";
        public const string QUEUE_TYPE_MARIOMAKER2 = "mariomaker2";

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

        class marioMakerTagCombination
        {
            public List<marioMakerLevelTag> tags { get; set;}
        }

        class queueConfigMarioMaker2
        {
            public bool useAPI { get; set; }
            public bool useFilter { get; set; }
            public List<marioMakerTagCombination> tagCombinationsAllow { get; set; }
            public List<marioMakerTagCombination> tagCombinationsExlude { get; set; }
            public List<marioMakerLevelTag> tagsExclude { get; set; }
            public float clearPercentageMax { get; set; }
            public float clearPercentageMin { get; set; }
            public int clearsMin { get; set; }
            public int clearsMax { get; set; }
            public int playsMin { get; set; }
            public int playsMax { get; set; }
            public int attemptsMin { get; set; }
            public int attemptsMax { get; set; }
            public float likePercentageMin { get; set; }
            public float likePercentageMax { get; set; }
            public double fastestTimeMin { get; set; }
            public double fastestTimeMax { get; set; }
            public double levelInfoCacheTime { get; set; }
            public marioMakerClearConditionRequirement clearConditionRequirement { get; set; }

            public queueConfigMarioMaker2()
            {
                clearPercentageMax = -1;
                clearPercentageMin = -1;
                clearsMin = -1;
                clearsMax = -1;
                playsMin = -1;
                playsMax = -1;
                likePercentageMin = -1;
                likePercentageMax = -1;
                fastestTimeMin = -1;
                fastestTimeMax = -1;
                attemptsMin = -1;
                attemptsMax = -1;
            }
        }

        class queueConfig
        {
            public int maxEntries { get; set; }
            public queueConfigWeightedRandom weightedRandom { get; set; }
            public queueConfigMarioMaker2 marioMaker2 { get; set; }
            public double permitNoFilterTime { get; set; }
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
        private Dictionary<userEntry, DateTime> m_PermitList;
        private Dictionary<string, marioMakerLevelInfo> m_MarioMaker2LevelInfoCache;

        private string joinString()
		{
            if (isActive)
            {
                switch (m_QueueType)
                {
                    case QUEUE_TYPE_MARIOMAKER:
                        return modeString() + m_BotBrain.localizer.getString("queueJoinHintMarioMaker");
                    case QUEUE_TYPE_MARIOMAKER2:
                        return modeString() + m_BotBrain.localizer.getString("queueJoinHintMarioMaker2");
                    case QUEUE_TYPE_GENERIC:
                        return modeString() + m_BotBrain.localizer.getString("queueJoinHintGeneric");
                    default:
                        return modeString() + m_BotBrain.localizer.getString("queueJoinHintPlain");
                }
            }

            return "";
		}

        private string modeString()
        {
            switch (m_QueueMode)
            {
                case QUEUE_MODE_SUBS:
                    return m_BotBrain.localizer.getString("queueModeSubOnly") + "  ";
                case QUEUE_MODE_FOLLOWERS:
                    return m_BotBrain.localizer.getString("queueModeFollowers") + "  ";
                default:
                    return m_BotBrain.localizer.getString("queueModeAll") + "  ";
            }
        }

		public void reset(bool announce = true)
		{
			m_EntryList.Clear();
			usersAddedRecently.Clear();
			if (announce)
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueReset"));
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
                    return (queueUser.isBroadcaster || queueUser.isModerator || queueUser.isSubscriber || m_BotBrain.checkUpdateIsFollower(queueUser));
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
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueModeSet"), m_QueueMode) + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueModeSet"), m_QueueMode));
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
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueTypeSet"), m_QueueType) + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueTypeSet"), m_QueueType));
			}
		}

		public void about(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(description))
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueDescriptionDisplay"), description));
            else
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueDescriptionEmpty"));
		}

		public void describe(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				description = argumentString;
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueDescriptionUpdated"));
			}
		}

		public void count(userEntry commandUser, string argumentString)
		{
			int userCount = m_EntryList.Count();

            if (isActive)
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueCountAnnounce"), userCount, m_ListMax, m_MaxPerUser) + "  " + joinString());
			else
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueCountAnnounce"), userCount, m_ListMax, m_MaxPerUser));
		}

        private bool isValidFilterLevel(marioMakerLevelInfo aLevelInfo, out string reasonString)
        {
            reasonString = "";
            bool isValid = true;
            List<string> reasonList = new List<string>();

            if (m_config.marioMaker2.tagsExclude != null && m_config.marioMaker2.tagsExclude.Count > 0)
            {
                List<string> foundInvalidTags = new List<string>();
                foreach (marioMakerLevelTag curTag in m_config.marioMaker2.tagsExclude)
                {
                    for(int i = 0; i < aLevelInfo.tags.Count; i++)
                    {
                        if (curTag == aLevelInfo.tags[i])
                            foundInvalidTags.Add(aLevelInfo.tags_name[i]);
                    }
                }

                if (foundInvalidTags.Count > 0)
                {
                    reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidTag"), string.Join(", ", foundInvalidTags)));
                    isValid = false;
                }
            }

            if (m_config.marioMaker2.clearConditionRequirement == marioMakerClearConditionRequirement.forbidden && !string.IsNullOrEmpty(aLevelInfo.clear_condition_name))
            {
                reasonList.Add(m_BotBrain.localizer.getString("marioMakerLevelInvalidClearConditionExluded"));
                isValid = false;
            }

            if (m_config.marioMaker2.clearConditionRequirement == marioMakerClearConditionRequirement.mandatory && string.IsNullOrEmpty(aLevelInfo.clear_condition_name))
            {
                reasonList.Add(m_BotBrain.localizer.getString("marioMakerLevelInvalidClearConditionOnly"));
                isValid = false;
            }

            if (m_config.marioMaker2.tagCombinationsAllow != null && m_config.marioMaker2.tagCombinationsAllow.Count > 0)
            {
                bool foundTagPair = false;

                foreach (marioMakerTagCombination allowPair in m_config.marioMaker2.tagCombinationsAllow)
                {
                    if (aLevelInfo.hasAllTags(allowPair.tags))
                    {
                        foundTagPair = true;
                        break;
                    }
                }

                if (!foundTagPair)
                {
                    reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidTagCombinationAllow"), string.Join(", ", aLevelInfo.tags_name)));
                    isValid = false;
                }
            }

            if (m_config.marioMaker2.tagCombinationsExlude != null && m_config.marioMaker2.tagCombinationsExlude.Count > 0)
            {
                foreach (marioMakerTagCombination excludePair in m_config.marioMaker2.tagCombinationsExlude)
                {
                    if (aLevelInfo.hasAllTags(excludePair.tags))
                    {
                        reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidTagCombinationExlude"), string.Join(", ", aLevelInfo.tags_name)));
                        isValid = false;
                        break;
                    }
                }
            }

            if (
                m_config.marioMaker2.fastestTimeMin != -1 &&
                m_config.marioMaker2.fastestTimeMax != -1 &&
                (
                    aLevelInfo.fastestClearTime.TotalSeconds < m_config.marioMaker2.fastestTimeMin ||
                    aLevelInfo.fastestClearTime.TotalSeconds > m_config.marioMaker2.fastestTimeMax
                )
            )
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidClearTime"), marioMakerAPI.durationString(aLevelInfo.fastestClearTime), TimeSpan.FromSeconds(m_config.marioMaker2.fastestTimeMin), TimeSpan.FromSeconds(m_config.marioMaker2.fastestTimeMax)));
                isValid = false;
            }
            else if (m_config.marioMaker2.fastestTimeMin != -1 && aLevelInfo.fastestClearTime.TotalSeconds < m_config.marioMaker2.fastestTimeMin)
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidClearTimeMin"), marioMakerAPI.durationString(aLevelInfo.fastestClearTime), TimeSpan.FromSeconds(m_config.marioMaker2.fastestTimeMin)));
                isValid = false;
            }
            else if (m_config.marioMaker2.fastestTimeMax != -1 && aLevelInfo.fastestClearTime.TotalSeconds > m_config.marioMaker2.fastestTimeMax)
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidClearTimeMax"), marioMakerAPI.durationString(aLevelInfo.fastestClearTime), TimeSpan.FromSeconds(m_config.marioMaker2.fastestTimeMax)));
                isValid = false;
            }

            if (
                m_config.marioMaker2.likePercentageMin != -1 &&
                m_config.marioMaker2.likePercentageMax != -1 &&
                (
                    aLevelInfo.likePercentage < m_config.marioMaker2.likePercentageMin ||
                    aLevelInfo.likePercentage > m_config.marioMaker2.likePercentageMax
                )
            )
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidLikePercent"), Math.Round(aLevelInfo.likePercentage * 100, 2), Math.Round(m_config.marioMaker2.likePercentageMin * 100, 2), Math.Round(m_config.marioMaker2.likePercentageMax * 100, 2)));
                isValid = false;
            }
            else if (m_config.marioMaker2.likePercentageMin != -1 && aLevelInfo.likePercentage < m_config.marioMaker2.likePercentageMin)
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidLikePercentMin"), Math.Round(aLevelInfo.likePercentage * 100, 2), Math.Round(m_config.marioMaker2.likePercentageMin * 100, 2)));
                isValid = false;
            }
            else if (m_config.marioMaker2.likePercentageMax != -1 && aLevelInfo.likePercentage > m_config.marioMaker2.likePercentageMax)
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidLikePercentMax"), Math.Round(aLevelInfo.likePercentage * 100, 2), Math.Round(m_config.marioMaker2.likePercentageMax * 100, 2)));
                isValid = false;
            }

            if (
                m_config.marioMaker2.clearPercentageMin != -1 &&
                m_config.marioMaker2.clearPercentageMax != -1 &&
                (
                    aLevelInfo.clearPercentage < m_config.marioMaker2.clearPercentageMin ||
                    aLevelInfo.clearPercentage > m_config.marioMaker2.clearPercentageMax
                )
            )
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidClearPercent"), Math.Round(aLevelInfo.clearPercentage * 100, 2), Math.Round(m_config.marioMaker2.clearPercentageMin * 100, 2), Math.Round(m_config.marioMaker2.clearPercentageMax * 100, 2)));
                isValid = false;
            }
            else if (m_config.marioMaker2.clearPercentageMin != -1 && aLevelInfo.clearPercentage < m_config.marioMaker2.clearPercentageMin)
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidClearPercentMin"), Math.Round(aLevelInfo.clearPercentage * 100, 2), Math.Round(m_config.marioMaker2.clearPercentageMin * 100, 2)));
                isValid = false;
            }
            else if (m_config.marioMaker2.clearPercentageMax != -1 && aLevelInfo.clearPercentage > m_config.marioMaker2.clearPercentageMax)
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidClearPercentMax"), Math.Round(aLevelInfo.clearPercentage * 100, 2), Math.Round(m_config.marioMaker2.clearPercentageMax * 100, 2)));
                isValid = false;
            }

            if (
                m_config.marioMaker2.attemptsMin != -1 &&
                m_config.marioMaker2.attemptsMax != -1 &&
                (
                    aLevelInfo.attempts < m_config.marioMaker2.attemptsMin ||
                    aLevelInfo.attempts > m_config.marioMaker2.attemptsMax
                )
            )
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidAttempts"), aLevelInfo.attempts, m_config.marioMaker2.attemptsMin, m_config.marioMaker2.attemptsMax));
                isValid = false;
            }
            else if (m_config.marioMaker2.attemptsMin != -1 && aLevelInfo.attempts < m_config.marioMaker2.attemptsMin)
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidAttemptsMin"), aLevelInfo.attempts, m_config.marioMaker2.attemptsMin));
                isValid = false;
            }
            else if (m_config.marioMaker2.attemptsMax != -1 && aLevelInfo.attempts > m_config.marioMaker2.attemptsMax)
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidAttemptsMax"), aLevelInfo.attempts, m_config.marioMaker2.attemptsMax));
                isValid = false;
            }

            if (
                m_config.marioMaker2.playsMin != -1 &&
                m_config.marioMaker2.playsMax != -1 &&
                (
                    aLevelInfo.plays < m_config.marioMaker2.playsMin ||
                    aLevelInfo.plays > m_config.marioMaker2.playsMax
                )
            )
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidPlays"), aLevelInfo.plays, m_config.marioMaker2.playsMin, m_config.marioMaker2.playsMax));
                isValid = false;
            }
            else if (m_config.marioMaker2.playsMin != -1 && aLevelInfo.plays < m_config.marioMaker2.playsMin)
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidPlaysMin"), aLevelInfo.plays, m_config.marioMaker2.playsMin));
                isValid = false;
            }
            else if (m_config.marioMaker2.playsMax != -1 && aLevelInfo.plays > m_config.marioMaker2.playsMax)
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidPlaysMax"), aLevelInfo.plays, m_config.marioMaker2.playsMax));
                isValid = false;
            }

            if (
                m_config.marioMaker2.clearsMin != -1 &&
                m_config.marioMaker2.clearsMax != -1 &&
                (
                    aLevelInfo.clears < m_config.marioMaker2.clearsMin ||
                    aLevelInfo.clears > m_config.marioMaker2.clearsMax
                )
            )
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidClears"), aLevelInfo.clears, m_config.marioMaker2.clearsMin, m_config.marioMaker2.clearsMax));
                isValid = false;
            }
            else if (m_config.marioMaker2.clearsMin != -1 && aLevelInfo.clears < m_config.marioMaker2.clearsMin)
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidClearsMin"), aLevelInfo.clears, m_config.marioMaker2.clearsMin));
                isValid = false;
            }
            else if (m_config.marioMaker2.clearsMax != -1 && aLevelInfo.clears > m_config.marioMaker2.clearsMax)
            {
                reasonList.Add(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalidClearsMax"), aLevelInfo.clears, m_config.marioMaker2.clearsMax));
                isValid = false;
            }

            if (!isValid)
            {
                reasonString = string.Join("  ", reasonList);
            }

            return isValid;
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

                            marioMakerLevelInfo newLevelInfo;
                            bool entryPassedFilter = true;

                            if (m_QueueType == QUEUE_TYPE_MARIOMAKER2 && m_config.marioMaker2.useAPI)
                            {
                                if (!m_MarioMaker2LevelInfoCache.ContainsKey(dataToEnter) || DateTime.Now.Subtract(m_MarioMaker2LevelInfoCache[dataToEnter].queryTime).TotalSeconds > m_config.marioMaker2.levelInfoCacheTime)
                                {
                                    newLevelInfo = marioMakerAPI.getLevelInfo(dataToEnter);
                                    if (newLevelInfo != null)
                                        m_MarioMaker2LevelInfoCache[dataToEnter] = newLevelInfo;
                                }
                                else
                                {
                                    newLevelInfo = m_MarioMaker2LevelInfoCache[dataToEnter];
                                }

                                if (newLevelInfo != null)
                                {
                                    if (m_config.marioMaker2.useFilter && (!m_PermitList.ContainsKey(commandUser) || DateTime.Now.Subtract(m_PermitList[commandUser]).TotalSeconds > m_config.permitNoFilterTime))
                                    {
                                        string filterFailReasons;
                                        if (!isValidFilterLevel(newLevelInfo, out filterFailReasons))
                                        {
                                            entryPassedFilter = false;
                                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("marioMakerLevelInvalid"), commandUser.Nickname, filterFailReasons));
                                        }
                                    }
                                }
                                else
                                {
                                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("marioMakerLevelNotFound"), commandUser.Nickname));
                                    entryPassedFilter = false;
                                }
                            }

                            if (entryPassedFilter)
                            {
                                queueData newData = new queueData(commandUser, dataToEnter);
                                m_EntryList.Add(newData);
                                usersAddedRecently.Add(newData);
                                userAddedRecently = true;

                                if (m_UpdateImmediately)
                                {
                                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueEntrySuccess"), commandUser.Nickname, m_EntryList.Count));
                                }
                            }
						}
					}
                    else
                    {
                        m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueEntryFailMaxPerUser"), commandUser.Nickname, m_MaxPerUser));
                    }
				}
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueEntryFailQueueFull"), commandUser.Nickname, m_EntryList.Count));
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
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queuePositionSingle"), commandUser.Nickname, position, totalEntries));
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queuePositionMultiple"), commandUser.Nickname, position));
                }
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueFailUserNotFound"), commandUser.Nickname));
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
                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueReplaceSuccessMultiple"), commandUser.Nickname, position, totalEntries));
                        }
                        else
                        {
                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueReplaceSuccess"), commandUser.Nickname, position));
                        }
                    } // TODO: Consider message for invalid entries.
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueFailUserNotFound"), commandUser.Nickname));
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
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueOpenedReset") + "  " + newJoinString);
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueOpened") + "  " + newJoinString);
                }

                m_Throttler.trigger();

                isActive = true;
            }
		}

		public void close(userEntry commandUser, string argumentString)
		{
			isActive = false;
			m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueClosed"));
		}

        public void setMaxCount(userEntry commandUser, string argumentString)
        {
            int newListMax;
            if (Int32.TryParse(argumentString, out newListMax))
            {
                m_ListMax = newListMax;
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueMaxEntriesSet"), m_ListMax));
            }
        }

        private string closedMessage(userEntry commandUser)
        {
            
            return string.Format(m_BotBrain.localizer.getString("queueClosedReply"), commandUser.Nickname);
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

                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueListDisplay"), listString));
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueNoEntries") + "  " + joinString());
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
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueLeaveSingle"), commandUser.Nickname));
            }
            else if (removeCount > 1)
            {
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueLeaveMultiple"), commandUser.Nickname, removeCount));
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueLeaveNotFound"), commandUser.Nickname));
            }
        }

        public void viewLevel(userEntry commandUser, string argumentString)
        {
            if (m_QueueType == QUEUE_TYPE_MARIOMAKER2)
            {
                if (m_CurEntry != null)
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("marioMakerViewLevelDisplay"), m_CurEntry.data));
                else
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueCurEntryDisplayEmpty"));
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("marioMakerViewLevelDisplayFailInvalidType"));
            }
        }

        public void reloadSettings(userEntry commandUser, string argumentString)
        {
            if (load())
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueReloadSuccess"));
            else
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueReloadFail"));
        }

        public void current(userEntry commandUser, string argumentString)
        {
            if (m_CurEntry != null)
            {
                switch (m_QueueType)
                {
                    case QUEUE_TYPE_PLAIN:
                        m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueCurEntryDisplayPlain"), m_CurEntry.user.Nickname));
                        break;
                    case QUEUE_TYPE_MARIOMAKER:
                    case QUEUE_TYPE_MARIOMAKER2:
                        if (m_config.marioMaker2.useAPI)
                        {
                            marioMakerLevelInfo curLevel = marioMakerAPI.getLevelInfo(m_CurEntry.data);
                            if (curLevel != null)
                            {
                                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueCurEntryDisplayMarioMakerAPI"), m_CurEntry.user.Nickname, m_CurEntry.data, m_CurEntry.levelInfo.name, Math.Round(curLevel.clearPercentage * 100, 2), marioMakerAPI.durationString(curLevel.fastestClearTime), string.Join(", ", curLevel.tags_name)));
                            }
                            else
                            {
                                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("marioMakerLevelNotFound"));
                            }
                        }
                        else
                        {
                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueCurEntryDisplayMarioMaker"), m_CurEntry.user.Nickname, m_CurEntry.data));
                        }

                        break;
                    case QUEUE_TYPE_GENERIC:
                        m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueCurEntryDisplayGeneric"), m_CurEntry.user.Nickname, m_CurEntry.data));
                        break;
                }
            }
            else
            {
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueCurEntryDisplayEmpty") + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueCurEntryDisplayEmpty") + "  " + m_BotBrain.localizer.getString("queueClosedOpenToEnter"));
            }
        }

        public void next(userEntry commandUser, string argumentString)
		{
            List<queueData> entryList = getEntryList();
            int userCount = entryList.Count();

			if (userCount > 0)
			{
				queueData nextEntry = entryList[0];
                m_CurEntry = nextEntry;
                m_EntryList.Remove(nextEntry);
                announceSelection(nextEntry);
            }
            else
			{
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueNoEntries") + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueNoEntries") + "  " + m_BotBrain.localizer.getString("queueClosedOpenToEnter"));
            }
		}

        private void announceSelection(queueData aEntry, string aPrefix = "")
        {
            switch (m_QueueType)
            {
                case QUEUE_TYPE_PLAIN:
                    m_BotBrain.sendDefaultChannelMessage(aPrefix + string.Format(m_BotBrain.localizer.getString("queueSelectPlain"), aEntry.user.Nickname));
                    break;
                case QUEUE_TYPE_MARIOMAKER:
                case QUEUE_TYPE_MARIOMAKER2:
                    if (m_QueueType == QUEUE_TYPE_MARIOMAKER2 && m_config.marioMaker2.useAPI)
                    {
                        marioMakerLevelInfo curLevel = marioMakerAPI.getLevelInfo(aEntry.data);
                        if (curLevel != null)
                        {
                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueSelectMarioMakerAPI"), aEntry.user.Nickname, aEntry.user.inChannel, aEntry.data, aEntry.levelInfo.name, Math.Round(curLevel.clearPercentage * 100, 2), marioMakerAPI.durationString(curLevel.fastestClearTime), string.Join(", ", curLevel.tags_name)));
                        }
                        else
                        {
                            m_BotBrain.sendDefaultChannelMessage(aPrefix + string.Format(m_BotBrain.localizer.getString("queueCurEntryDisplayMarioMaker"), aEntry.user.Nickname, aEntry.user.inChannel, aEntry.data));
                        }
                    }
                    else
                    {
                        m_BotBrain.sendDefaultChannelMessage(aPrefix + string.Format(m_BotBrain.localizer.getString("queueCurEntryDisplayMarioMaker"), aEntry.user.Nickname, aEntry.user.inChannel, aEntry.data));
                    }
                        
                    break;
                case QUEUE_TYPE_GENERIC:
                    m_BotBrain.sendDefaultChannelMessage(aPrefix + string.Format(m_BotBrain.localizer.getString("queueSelectGeneric"), aEntry.user.Nickname, aEntry.data));
                    break;
            }
        }

        public void subNext(userEntry commandUser, string argumentString)
        {
            List<queueData> subList = getEntryList(false, true, true, false);
            queueData nextEntry;
            bool foundEntry = false;

            if (subList.Count > 0)
            {
                nextEntry = subList[0];
                foundEntry = true;

                m_CurEntry = nextEntry;
                m_EntryList.Remove(nextEntry);
                announceSelection(nextEntry, m_BotBrain.localizer.getString("queueSelectNoteSub") + " ");
            }

            if (!foundEntry)
            {
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueNoEntriesSub") +  "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueNoEntriesSub") + "  " + m_BotBrain.localizer.getString("queueClosedOpenToEnter"));
            }

        }

        public void subRandom(userEntry commandUser, string argumentString)
        {
            queueData nextEntry;
            bool foundEntry = false;

            List<queueData> subList = getEntryList(false, true, true, false);

            if (subList.Count > 0)
            {
                int selectID = m_BotBrain.randomizer.Next(0, subList.Count - 1);
                nextEntry = subList[selectID];
                foundEntry = true;

                m_CurEntry = nextEntry;
                m_EntryList.Remove(nextEntry);

                announceSelection(nextEntry, m_BotBrain.localizer.getString("queueSelectNoteSubRandom") + " ");
            }

            if (!foundEntry)
            {
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueNoEntriesSub") + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueNoEntriesSub") + "  " + m_BotBrain.localizer.getString("queueClosedOpenToEnter"));
            }
        }

        public void random(userEntry commandUser, string argumentString)
        {
            List<queueData> entryList = getEntryList();
            int userCount = entryList.Count();

            if (userCount > 0)
            {
                int selectID = m_BotBrain.randomizer.Next(0, userCount - 1);

                queueData nextEntry = entryList[selectID];
                m_CurEntry = nextEntry;
                m_EntryList.Remove(nextEntry);
                announceSelection(nextEntry, m_BotBrain.localizer.getString("queueSelectNoteRandom") + " ");
            }
            else
            {
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueNoEntries") + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueNoEntries") + "  " + m_BotBrain.localizer.getString("queueClosedOpenToEnter"));
            }
        }

        public void permitNoFilter(userEntry commandUser, string argumentString)
        {
            if (m_QueueType == QUEUE_TYPE_MARIOMAKER2)
            {
                userEntry newUser = m_BotBrain.checkCreateUser(argumentString);
                m_PermitList.Add(newUser, DateTime.Now);

                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queuePermitNoFilterDisplay"), argumentString, m_config.permitNoFilterTime));
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

            if (m_BotBrain.checkUpdateIsFollower(aData.user))
                outputWeight *= (m_LoadSuccessful ? m_config.weightedRandom.followModifier : 1.25f);

            if (aData.user.isSubscriber)
                outputWeight *= (m_LoadSuccessful ? m_config.weightedRandom.subModifier : 1.15f);

            return outputWeight;
        }

        public int calculateTotalUserWeight(List<queueData> aEntryList)
        {
            int totalWeight = 0;
            queueData curEntry;

            for (int i = 0; i < aEntryList.Count; i++)
            {
                curEntry = aEntryList[i];
                curEntry.randomWeight = (int)Math.Round(calculateUserWeight(aEntryList[i]));
                totalWeight += curEntry.randomWeight;
            }

            return totalWeight;
        }

        private List<queueData> getEntryList(bool ignoreBrb = false, bool ignoreOffline = true, bool mustSub = false, bool mustFollow = false)
        {
            List<queueData> outList = new List<queueData>();

            foreach (queueData curEnry in m_EntryList)
            {
                if ((ignoreBrb || !curEnry.user.isBrb) && (ignoreOffline || curEnry.user.inChannel) && (!mustSub || curEnry.user.isSubscriber) && (!mustFollow || m_BotBrain.checkUpdateIsFollower(curEnry.user)))
                    outList.Add(curEnry);
            }

            return outList;
        }

        public void weightedRandom(userEntry commandUser, string argumentString)
        {
            List<queueData> entryList = getEntryList();

            int userCount = entryList.Count();

            if (userCount > 0)
            {
                int totalWeight = calculateTotalUserWeight(entryList);
                int targValue = m_BotBrain.randomizer.Next(0, totalWeight);
                int curWeight = 0;

                queueData curEntry = entryList[0];

                for (int i = 0; i < entryList.Count; i++)
                {
                    curEntry = entryList[i];
                    if (i == entryList.Count - 1 || (targValue >= curWeight && targValue < (curWeight + curEntry.randomWeight)))
                    {
                        break;  // Found user
                    }
                    curWeight += curEntry.randomWeight;
                }

                m_CurEntry = curEntry;
                m_EntryList.Remove(curEntry);
                announceSelection(curEntry, m_BotBrain.localizer.getString("queueSelectNoteWeightedRandom"));
            }
            else
            {
                if (isActive)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueNoEntries") + "  " + joinString());
                else
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueNoEntries") + "  " + m_BotBrain.localizer.getString("queueClosedOpenToEnter"));
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
                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("queueAnnounceEntriesSinceUpdate"), usersAddedRecently.Count) + "  " + newJoinString);
                            usersAddedRecently.Clear();
                            userAddedRecently = false;
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(description))
                                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueAnnounceOpen") + "  " + newJoinString);
                            else
                                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("queueAnnounceOpen") + " (" + description + ")  " + newJoinString);
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
                    if (m_config.maxEntries > 0)
                    {
                        m_ListMax = m_config.maxEntries;
                    }
                    m_LoadSuccessful = true;
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
            m_PermitList = new Dictionary<userEntry, DateTime>();
            m_MarioMaker2LevelInfoCache = new Dictionary<string, marioMakerLevelInfo>();

            load();

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
            tempDef.addSubCommand(new chatCommandDef("permit", permitNoFilter, true, false));
            tempDef.addSubCommand(new chatCommandDef("viewlevel", viewLevel, true, false));
            tempDef.addSubCommand(new chatCommandDef("reload", reloadSettings, false, false));
            tempDef.useGlobalCooldown = false;
			m_BotBrain.addChatCommand(tempDef);
		}
	}
}