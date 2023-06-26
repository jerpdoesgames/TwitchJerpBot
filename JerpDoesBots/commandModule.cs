using System;
using System.Data.SQLite;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Net;
using System.Globalization;

namespace JerpDoesBots
{
	class commandModule : botModule
	{
		protected string insertQuery = "INSERT OR IGNORE INTO commands_custom (submitter, modifier, command_name, lastmod, allow_normal, message) values (@param1, @param2, @param3, @param4, @param5, @param6)";
		protected string selectQuery = "SELECT * FROM commands_custom WHERE command_name = @param1 LIMIT 1";
		protected string createQuery = "CREATE TABLE IF NOT EXISTS commands_custom (commandID INTEGER PRIMARY KEY ASC, command_name TEXT UNIQUE, submitter TEXT, modifier TEXT, lastmod INTEGER, allow_normal INTEGER, message TEXT)";
		protected string formatHint; // Filled out via localizer in constructor
		protected string removeQuery = "DELETE FROM commands_custom WHERE command_name=@param1";
		protected string selectAllQuery = "SELECT * FROM commands_custom ORDER BY command_name ASC";
		protected string useGame = null;
		protected string outputListFilename = "jerpdoesbots_commands_custom.json";
		protected string outputListMessageSuccess;

		private string getGameString()
		{
			if (!string.IsNullOrEmpty(useGame))
				return useGame;
			else
				return m_BotBrain.game;
		}

		public void setGame(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				useGame = argumentString;
				if (!aSilent)
					m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("commandGameForced"), argumentString));
			}
		}

		public void clearGame(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			useGame = null;
			if (!aSilent)
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("commandGameAuto"), m_BotBrain.game));
		}

		public SQLiteDataReader loadCommand(string commandName)
		{
			string getCommandQuery = selectQuery;

			SQLiteCommand getCommandCommand = new SQLiteCommand(getCommandQuery, m_BotBrain.storageDB);

			getCommandCommand.Parameters.Add(new SQLiteParameter("@param1", commandName));
			getCommandCommand.Parameters.Add(new SQLiteParameter("@param2", getGameString()));            // Current Game (unused in base)
			SQLiteDataReader getCommandReader = getCommandCommand.ExecuteReader();

			return getCommandReader;
		}

		const int CUSTOM_ARG_COUNT_MAX = 9;

		private struct customCommandArg
		{
			public bool isUrlEncoded { get; set; }
			public bool isUpperCase { get; set; }
			public bool isLowerCase { get; set; }
			public bool isTitleCase { get; set; }
			public string matchString { get; set; }
			public int index { get; set; }

			public customCommandArg(string aMatchString, int aIndex, bool aUrlEncoded, bool aIsTitleCase, bool aIsUpperCase, bool aIsLowerCase)
			{
				isUrlEncoded = aUrlEncoded;
				index = aIndex;
				matchString = aMatchString;
				isTitleCase = aIsTitleCase;
				isUpperCase = aIsUpperCase;
				isLowerCase = aIsLowerCase;
			}
		}

		public string processCustomCommandArgs(string aCommandMessage, string aArgumentString)	// Process {0}, {1e}, etc. in custom commands.  'e' denotes url encoding.
        {
			List<customCommandArg> customArgList = new List<customCommandArg>();
			
			string argPattern = @"\{[0-9][eut]*\}";
			string numPattern = @"\d+";
			int highestArgNum = -1;

			TextInfo tempTextInfo = new CultureInfo("en-US", false).TextInfo;	// TODO: Move somewhere a bit more global

			foreach (Match match in Regex.Matches(aCommandMessage, argPattern))
            {
				Match numMatch = Regex.Match(match.Value, numPattern);
				int numValue;
					
				if (numMatch.Success && Int32.TryParse(numMatch.Value, out numValue))
                {
					highestArgNum = Math.Min(numValue, CUSTOM_ARG_COUNT_MAX);

					bool isUrlEncoded = match.Value.IndexOf("e") >= 0;
					bool isTitleCase = match.Value.IndexOf("t") >= 0;
					bool isUpperCase = match.Value.IndexOf("u") >= 0;
					bool isLowerCase = match.Value.IndexOf("l") >= 0;

					customArgList.Add(new customCommandArg(match.Value, numValue, isUrlEncoded, isTitleCase, isUpperCase, isLowerCase));
				}
            }

			if (highestArgNum >= 0)
            {
				string[] messageArgs = aArgumentString.Split(new char[] { ' ' }, highestArgNum + 2, StringSplitOptions.RemoveEmptyEntries);
				List<string> messageArgList = new List<string>(messageArgs);
				messageArgList.RemoveAt(0);	// Remove command name

				foreach(customCommandArg customArg in customArgList)
                {
					if (customArg.index < messageArgList.Count)
                    {
						string messageArg = messageArgList[customArg.index];
						messageArg = customArg.isTitleCase? tempTextInfo.ToTitleCase(messageArg) : messageArg;
						messageArg = customArg.isUpperCase ? messageArg.ToUpper() : messageArg;
						messageArg = customArg.isLowerCase ? messageArg.ToLower() : messageArg;
						messageArg = customArg.isUrlEncoded ? WebUtility.UrlEncode(messageArg) : messageArg;

						aCommandMessage = aCommandMessage.Replace(customArg.matchString, messageArg);
					}
				}
			}

			return aCommandMessage;
        }

		public chatCommandDef get(string commandName) // TODO: Add a loader so this can be mostly reused but not return a chatCommandDef
		{
			if (!string.IsNullOrEmpty(commandName))
			{
				SQLiteDataReader getCommandReader = loadCommand(commandName);

				if (getCommandReader.HasRows && getCommandReader.Read())
				{
					string message = Convert.ToString(getCommandReader["message"]);
					bool allowNormal = Convert.ToBoolean(int.Parse(Convert.ToString(getCommandReader["allow_normal"])));    // Don't look at me, I'm HIDEOUS!

					chatCommandDef.commandActionDelegate customCommandDelegate = delegate (userEntry commandUser, string argumentString, bool aSilent) {
						string processedMessage = processCustomCommandArgs(message, argumentString);

						m_BotBrain.sendDefaultChannelMessage(processedMessage);
					};

					return new chatCommandDef(commandName, customCommandDelegate, true, allowNormal);
				}
			}

			return null;
		}

		public virtual void add(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			string[] argumentList = argumentString.Split(new[] { ' ' }, 3);

			if (
				argumentList.Length == 3 &&
				!string.IsNullOrEmpty(argumentList[0]) &&
				!string.IsNullOrEmpty(argumentList[1]) && (argumentList[1] == "0" || argumentList[1] == "1") &&
				!string.IsNullOrEmpty(argumentList[2])
			)
			{
				string commandName = argumentList[0];

				SQLiteDataReader getCommandReader = loadCommand(commandName);

				if (getCommandReader.HasRows)
				{
					m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("commandAddFailExists"), argumentList[0]));
				}
				else
				{
					uint allowNormalUsers = (argumentList[2] == "0") ? 0u : 1u;

					string addCommandQuery = insertQuery;

					SQLiteCommand addCommandCommand = new SQLiteCommand(addCommandQuery, m_BotBrain.storageDB);

					addCommandCommand.Parameters.Add(new SQLiteParameter("@param1", commandUser.Nickname));     // Submitter
					addCommandCommand.Parameters.Add(new SQLiteParameter("@param2", commandUser.Nickname));     // Modifier (same)
					addCommandCommand.Parameters.Add(new SQLiteParameter("@param3", argumentList[0]));          // Command Name
					addCommandCommand.Parameters.Add(new SQLiteParameter("@param4", 423432434));                // TODO: Actual timestamp
					addCommandCommand.Parameters.Add(new SQLiteParameter("@param5", allowNormalUsers));         // 0/1
					addCommandCommand.Parameters.Add(new SQLiteParameter("@param6", argumentList[2]));          // Message
					addCommandCommand.Parameters.Add(new SQLiteParameter("@param7", getGameString()));          // Current Game (unused in base)

					if (addCommandCommand.ExecuteNonQuery() > 0)
					{
						if (!aSilent)
							m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("commandAddSuccess"), argumentList[0]));
                    }
					else
						m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("commandAddFail"), argumentList[0]));
				}
			}
			else
				m_BotBrain.sendDefaultChannelMessage(formatHint);
		}

		public void remove(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				string removeCommandQuery = removeQuery;

				SQLiteCommand removeCommandCommand = new SQLiteCommand(removeCommandQuery, m_BotBrain.storageDB);

				removeCommandCommand.Parameters.Add(new SQLiteParameter("@param1", argumentString));
				removeCommandCommand.Parameters.Add(new SQLiteParameter("@param2", getGameString()));            // Current Game (unused in base)

				if (removeCommandCommand.ExecuteNonQuery() > 0)
				{
					if (!aSilent)
						m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("commandRemoveSuccess"), argumentString));
				}
				else
				{
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("commandRemoveFailNotFound"));
				}
			}
		}

		public virtual void initTable()
		{
			string createCommandTableQuery = createQuery;
			SQLiteCommand createCommandTableCommand = new SQLiteCommand(createCommandTableQuery, m_BotBrain.storageDB);
			createCommandTableCommand.ExecuteNonQuery();
		}

		public virtual object getCurEntryJsonObject(SQLiteDataReader aEntryReader)
        {
			return new { name = Convert.ToString(aEntryReader["command_name"]), message = Convert.ToString(aEntryReader["message"]) };
		}

		public virtual void outputList(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			SQLiteCommand getEntriesCommand = new SQLiteCommand(selectAllQuery, m_BotBrain.storageDB);
			SQLiteDataReader getEntriesReader = getEntriesCommand.ExecuteReader();

			List<object> rowData = new List<object>();

			if (getEntriesReader.HasRows)
			{
				while (getEntriesReader.Read())
				{
					rowData.Add(getCurEntryJsonObject(getEntriesReader));
				}
			}

			m_BotBrain.genericSerializeToFile(rowData, outputListFilename);

			if (!aSilent)
				m_BotBrain.sendDefaultChannelMessage(outputListMessageSuccess);
		}

		public commandModule(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			formatHint = m_BotBrain.localizer.getString("commandFormatHint");
		}
	}
}
