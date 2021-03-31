using System;
using System.Data.SQLite;

namespace JerpDoesBots
{
	class commandModule : botModule
	{
		protected string insertQuery = "INSERT OR IGNORE INTO commands_custom (submitter, modifier, command_name, lastmod, allow_normal, message) values (@param1, @param2, @param3, @param4, @param5, @param6)";
		protected string selectQuery = "SELECT * FROM commands_custom WHERE command_name = @param1 LIMIT 1";
		protected string createQuery = "CREATE TABLE IF NOT EXISTS commands_custom (commandID INTEGER PRIMARY KEY ASC, command_name TEXT UNIQUE, submitter TEXT, modifier TEXT, lastmod INTEGER, allow_normal INTEGER, message TEXT)";
		protected string formatHint = "Expected format: !command add [name] [allowNormalUsers (0/1)] [message]";
		protected string removeQuery = "DELETE FROM commands_custom WHERE command_name=@param1";
		protected string useGame = null;

		private string getGameString()
		{
			if (!string.IsNullOrEmpty(useGame))
				return useGame;
			else
				return m_BotBrain.game;
		}

		public void setGame(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				useGame = argumentString;
				m_BotBrain.sendDefaultChannelMessage("Now using \"" + argumentString + "\" for game-specific commands.");
			}
		}

		public void clearGame(userEntry commandUser, string argumentString)
		{
			useGame = null;
			m_BotBrain.sendDefaultChannelMessage("Auto-select mode resumed for game-specific commands. ("+m_BotBrain.game+")");
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

		public chatCommandDef get(string commandName) // TODO: Add a loader so this can be mostly reused but not return a chatCommandDef
		{
			if (!string.IsNullOrEmpty(commandName))
			{
				SQLiteDataReader getCommandReader = loadCommand(commandName);

				if (getCommandReader.HasRows && getCommandReader.Read())
				{
					string message = Convert.ToString(getCommandReader["message"]);
					bool allowNormal = Convert.ToBoolean(int.Parse(Convert.ToString(getCommandReader["allow_normal"])));    // Don't look at me, I'm HIDEOUS!

					// TODO: Something that allows tokens within the string (?)
					// TODO: Something to allow other commands to be executed via template-like behavior?

					chatCommandDef.commandActionDelegate customCommandDelegate = delegate (userEntry commandUser, string argumentString) { m_BotBrain.sendDefaultChannelMessage(message); };

					return new chatCommandDef(commandName, customCommandDelegate, true, allowNormal);
				}
			}

			return null;
		}

		public virtual void add(userEntry commandUser, string argumentString)
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
					m_BotBrain.sendDefaultChannelMessage("Unable to add command '" + argumentList[0] + "' - already exists.");
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
						m_BotBrain.sendDefaultChannelMessage("Command '" + argumentList[0] + "' added!");
					else
						m_BotBrain.sendDefaultChannelMessage("Unable to add command '" + argumentList[0] + "'");
				}
			}
			else
				m_BotBrain.sendDefaultChannelMessage(formatHint);
		}

		public void edit(userEntry commandUser, string argumentString)
		{
			// TODO: Add edit for custom commands
		}

		public void remove(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				string removeCommandQuery = removeQuery;

				SQLiteCommand removeCommandCommand = new SQLiteCommand(removeCommandQuery, m_BotBrain.storageDB);

				removeCommandCommand.Parameters.Add(new SQLiteParameter("@param1", argumentString));
				removeCommandCommand.Parameters.Add(new SQLiteParameter("@param2", getGameString()));            // Current Game (unused in base)

				if (removeCommandCommand.ExecuteNonQuery() > 0)
				{
					m_BotBrain.sendDefaultChannelMessage("Command '" + argumentString + "' removed");
				}
				else
				{
					m_BotBrain.sendDefaultChannelMessage("Command not found!");
				}
			}
		}

		public virtual void initTable()
		{
			string createCommandTableQuery = createQuery;
			SQLiteCommand createCommandTableCommand = new SQLiteCommand(createCommandTableQuery, m_BotBrain.storageDB);
			createCommandTableCommand.ExecuteNonQuery();
		}

		public commandModule(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			// TODO: Add ability to list out commands.
		}
	}
}
