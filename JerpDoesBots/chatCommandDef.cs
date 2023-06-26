using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JerpDoesBots
{
    class chatCommandDef
	{
		private string m_Name;
		public string name { get { return m_Name; } }

		public delegate void commandActionDelegate(userEntry aUser, string aArgumentString, bool aSilent = false);
		private commandActionDelegate m_CommandAction;

		[JsonIgnore]
		public commandActionDelegate Run { get { return m_CommandAction; } }

		private chatCommandDef m_ParentCommand;
		[JsonIgnore]
		public chatCommandDef parentCommand {
			get { return m_ParentCommand; }
			set { m_ParentCommand = value; }
		}

		public bool allowModerator { get { return m_AllowModerator; } }
		private bool m_AllowModerator		= false;
		public bool allowNormal { get { return m_AllowNormal; } }
		private bool m_AllowNormal		= false;

		private long m_GlobalCooldown		= 5000;
        private long m_UserCooldown       = 15000;
		private bool m_UseCooldown	= true;
		private long m_TimeLast			= 0;    // For global cooldowns

        private Dictionary<string, long> lastUsedTimes;

        public bool useGlobalCooldown {
			get { return m_UseCooldown; }
			set { m_UseCooldown = value; }
		}

		public long globalCooldown
		{
			get { return m_GlobalCooldown; }
			set { m_GlobalCooldown = value; }
		}

        public long userCooldown
        {
            get { return m_UserCooldown; }
            set { m_UserCooldown = value; }
        }

        private bool m_useUserCooldown = true;

        public bool useUserCooldown
        {
            get { return m_useUserCooldown; }
            set { m_useUserCooldown = value; }
        }

		private List<chatCommandDef> m_SubCommands;
		public List<chatCommandDef> subCommands { get { return m_SubCommands; } }

		public void addSubCommand(chatCommandDef newSub)
		{
			newSub.m_ParentCommand = this;
			m_SubCommands.Add(newSub);
		}


		public bool isOnCooldown(long aTimeNow, userEntry aUser)
		{
            if (m_UseCooldown && !(aTimeNow > m_TimeLast + m_GlobalCooldown))
                return true;
            else if (getLastUsed(aUser) + m_UserCooldown > aTimeNow)
                return true;
            else
                return false;
		}
		[JsonIgnore]
		public long timeLast {
			get { return m_TimeLast; }
			set { m_TimeLast = value; }
		}

        private long getLastUsed(userEntry aUser)
        {
            if (lastUsedTimes.ContainsKey(aUser.Nickname))
                return lastUsedTimes[aUser.Nickname];
            else
                lastUsedTimes[aUser.Nickname] = -1;

            return lastUsedTimes[aUser.Nickname];
        }

		public bool canUse(userEntry aUser, long aTimeNow)
		{
			if (aUser.isBroadcaster)
				return true;

			if (isOnCooldown(aTimeNow, aUser))
				return false;

			if (m_AllowModerator && aUser.isModerator)
				return true;
			else if (m_AllowNormal)
				return true;
			else
				return false;
		}

		public chatCommandDef(string aName, commandActionDelegate aCommandAction, bool aAllowModerator = true, bool aAllowNormal = false)
		{
			m_Name			= aName;
			m_AllowModerator	= aAllowModerator;
			m_AllowNormal		= aAllowNormal;
			m_CommandAction	= aCommandAction;

			m_SubCommands = new List<chatCommandDef>();
            lastUsedTimes = new Dictionary<string, long>();
		}
	}
}
