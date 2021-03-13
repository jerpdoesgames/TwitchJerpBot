using System.Collections.Generic;

namespace JerpDoesBots
{
    class chatCommandDef
	{
		private string name;
		private List<chatCommandDef> subCommands;
		public List<chatCommandDef> SubCommands { get { return subCommands; } }

		public delegate void commandActionDelegate(userEntry commandUser, string argumentString);
		private commandActionDelegate commandAction;

		public commandActionDelegate Run { get { return commandAction; } }

		private chatCommandDef parentCommand;
		public chatCommandDef ParentCommand {
			get { return parentCommand; }
			set { parentCommand = value; }
		}

		private bool allowModerator		= false;
		private bool allowNormal		= false;

		private long globalCooldown		= 5000;
        private long userCooldown       = 15000;
		private bool useGlobalCooldown	= true;
		private long timeLast			= 0;    // For global cooldowns

        private Dictionary<string, long> lastUsedTimes;

        public bool UseGlobalCooldown {
			get { return useGlobalCooldown; }
			set { useGlobalCooldown = value; }
		}

		public long GlobalCooldown
		{
			get { return globalCooldown; }
			set { globalCooldown = value; }
		}

        public long UserCooldown
        {
            get { return userCooldown; }
            set { userCooldown = value; }
        }

        private bool useUserCooldown = true;

        public bool UseUserCooldown
        {
            get { return useUserCooldown; }
            set { useUserCooldown = value; }
        }

        public void addSubCommand(chatCommandDef newSub)
		{
			newSub.parentCommand = this;
			subCommands.Add(newSub);
		}

		public string Name { get { return name; } }

		public bool isOnCooldown(long timeNow, userEntry checkUser)
		{
            if (useGlobalCooldown && !(timeNow > timeLast + globalCooldown))
                return true;
            else if (getLastUsed(checkUser) + userCooldown > timeNow)
                return true;
            else
                return false;
		}

		public long TimeLast {
			get { return timeLast; }
			set { timeLast = value; }
		}

        private long getLastUsed(userEntry checkUser)
        {
            if (lastUsedTimes.ContainsKey(checkUser.Nickname))
                return lastUsedTimes[checkUser.Nickname];
            else
                lastUsedTimes[checkUser.Nickname] = -1;

            return lastUsedTimes[checkUser.Nickname];
        }

		public bool canUse(userEntry commandUser, long timeNow)
		{
			if (commandUser.IsBroadcaster)
				return true;

			if (isOnCooldown(timeNow, commandUser))
				return false;

			if (allowModerator && commandUser.IsModerator)
				return true;
			else if (allowNormal)
				return true;
			else
				return false;
		}

		public chatCommandDef(string newName, commandActionDelegate newCommandAction, bool newAllowModerator = true, bool newAllowNormal = false)
		{
			name			= newName;
			allowModerator	= newAllowModerator;
			allowNormal		= newAllowNormal;
			commandAction	= newCommandAction;

			subCommands = new List<chatCommandDef>();
            lastUsedTimes = new Dictionary<string, long>();
		}
	}
}
