﻿using System;
using System.Data.SQLite;
using System.Collections.Generic;

namespace JerpDoesBots
{
	class counterEntry
	{
		jerpBot botBrain;

		private string m_Name;
		private string description;
		private string m_Game;
		private string owner;
		private int count = 0;
		private bool initialized = false;
		public bool Initialized { get { return initialized; } }

		public int Count { get { return count; } }
		public string Name { get { return m_Name; } }
		public string Game { get { return m_Game; } }

		public string Description { get { return description; } }

		private bool create(string newName)
		{
			string createViewerRowQuery = "INSERT INTO counters (name, count, game) values (@param1, @param2, @param3)";

			SQLiteCommand createViewerRowCommand = new SQLiteCommand(createViewerRowQuery, botBrain.storageDB);

			createViewerRowCommand.Parameters.Add(new SQLiteParameter("@param1", newName));
			createViewerRowCommand.Parameters.Add(new SQLiteParameter("@param2", (object)0));
			createViewerRowCommand.Parameters.Add(new SQLiteParameter("@param3", m_Game));

			if (createViewerRowCommand.ExecuteNonQuery() > 0)
			{
				count = 0;
				return true;
			}
			return false;
		}

		public string Owner
		{
			get { return owner; }
			set { owner = value; }
		}

		public bool load(string counterName)
		{
			string getViewerRowQuery = "SELECT * FROM counters WHERE name = @param1 AND game=@param2 LIMIT 1";
			SQLiteCommand getRowCommand = new SQLiteCommand(getViewerRowQuery, botBrain.storageDB);
			getRowCommand.Parameters.Add(new SQLiteParameter("@param1", counterName));
			getRowCommand.Parameters.Add(new SQLiteParameter("@param2", m_Game));
			SQLiteDataReader rowReader = getRowCommand.ExecuteReader();

			if (rowReader.HasRows && rowReader.Read())
			{
				description = Convert.ToString(rowReader["description"]);
				count = Convert.ToInt32(rowReader["count"]);
				return true;
			}

			return false;
		}

		public bool add(int amount)
		{
			if (initialized)
			{
				count = count + amount;
				string updateQuery = "UPDATE counters SET count=@param1 where name=@param2 AND game=@param3";

				SQLiteCommand updateCommand = new SQLiteCommand(updateQuery, botBrain.storageDB);

				updateCommand.Parameters.Add(new SQLiteParameter("@param1", amount));
				updateCommand.Parameters.Add(new SQLiteParameter("@param2", m_Name));
				updateCommand.Parameters.Add(new SQLiteParameter("@param3", m_Game));

				if (updateCommand.ExecuteNonQuery() > 0)
				{
					return true;
				}
			}
			return false;
		}

		public bool remove(int amount)
		{
			return add((amount * -1));
		}

		public bool describe(string newDescription)
		{
			if (initialized)
			{
				description = newDescription;
				string updateQuery = "UPDATE counters SET description=@param1 WHERE name=@param2 AND game=@param3";

				SQLiteCommand updateCommand = new SQLiteCommand(updateQuery, botBrain.storageDB);

				updateCommand.Parameters.Add(new SQLiteParameter("@param1", description));
				updateCommand.Parameters.Add(new SQLiteParameter("@param2", m_Name));
				updateCommand.Parameters.Add(new SQLiteParameter("@param3", m_Game));

				if (updateCommand.ExecuteNonQuery() > 0)
				{
					return true;
				}
			}
			return false;
		}

		public bool set(int amount)
		{
			if (initialized)
			{
				count = amount;
				string updateQuery = "UPDATE counters SET count=@param1 WHERE name=@param2 AND game=@param3";

				SQLiteCommand updateCommand = new SQLiteCommand(updateQuery, botBrain.storageDB);

				updateCommand.Parameters.Add(new SQLiteParameter("@param1", amount));
				updateCommand.Parameters.Add(new SQLiteParameter("@param2", m_Name));
				updateCommand.Parameters.Add(new SQLiteParameter("@param3", m_Game));

				if (updateCommand.ExecuteNonQuery() > 0)
				{
					return true;
				}
			}

			return false;
		}

		public bool delete()
		{
			if (initialized)
			{
				string updateQuery = "DELETE FROM counters WHERE name=@param1 AND game=@param2";

				SQLiteCommand updateCommand = new SQLiteCommand(updateQuery, botBrain.storageDB);

				updateCommand.Parameters.Add(new SQLiteParameter("@param1", m_Name));
				updateCommand.Parameters.Add(new SQLiteParameter("@param2", m_Game));

				if (updateCommand.ExecuteNonQuery() > 0)
				{
					return true;
				}
			}

			return false;
		}

		public counterEntry(string newName, string newGame, jerpBot botGeneral, bool autoCreate = true, bool tryLoad = true)
		{
			if (!String.IsNullOrEmpty(newName))
			{
				m_Name = newName;
				m_Game = newGame;
				botBrain = botGeneral;

                if (tryLoad && load(newName))
                    initialized = true;
                else if (autoCreate && create(newName))
                    initialized = true;
			}
		}
	}
	class counter : botModule
	{
        public const string GAME_NOGAME = "nogame";
		private string m_Game;
		private Dictionary<string, Dictionary<string, counterEntry>> m_Entries; // Game, <counterName, counterObject>

        private string getGameString()
        {
            if (!string.IsNullOrEmpty(m_Game))
                return m_Game;

            if (!string.IsNullOrEmpty(m_BotBrain.game))
                return m_BotBrain.game;

            return GAME_NOGAME;
        }

        private counterEntry tryLoadEntry(string name, string specificGame = null)
        {
            string gameString = (specificGame != null) ? specificGame : getGameString();

            if (m_Entries[gameString].ContainsKey(name))
                return m_Entries[gameString][name];
            if (gameString != GAME_NOGAME && m_Entries[GAME_NOGAME].ContainsKey(name))
                return m_Entries[GAME_NOGAME][name];

            counterEntry loadedEntry = new counterEntry(name, gameString, m_BotBrain, false);

            if (!loadedEntry.Initialized && gameString != GAME_NOGAME)
                loadedEntry = new counterEntry(name, GAME_NOGAME, m_BotBrain, false);

            if (loadedEntry.Initialized)
            {
                m_Entries[loadedEntry.Game][loadedEntry.Name] = loadedEntry;
                return loadedEntry;
            }

            return null;
        }

        private void checkInitializeList(string game)
        {
            if (!m_Entries.ContainsKey(game))
                m_Entries[game] = new Dictionary<string, counterEntry>(); // Create list for the game we're checking, if it doesn't already exist
        }

        public counterEntry checkCreateEntry(string name, bool doCreate = true, string specificGame = null)
        {
            string gameString = (specificGame != null) ? specificGame : getGameString();

            if (gameString != GAME_NOGAME)
                checkInitializeList(gameString);

            counterEntry newCounter = tryLoadEntry(name);

            if (newCounter != null)
                return newCounter;
            else if (doCreate)
            {
                newCounter = new counterEntry(name, gameString, m_BotBrain, true, false);
                if (newCounter.Initialized)
                {
                    m_Entries[gameString][name] = newCounter;
                    return newCounter;
                }
            }

            return null;
        }

        private uint getSimpleArgs(string aInput, out string aCounterName, out string aRemainder)
        {
            string[] argArray = aInput.Split(new[] { ' ' }, 2);

            aCounterName = "";
            aRemainder = "";

            if (argArray.Length >= 1)
            {
                aCounterName = argArray[0];

                if (argArray.Length == 2)
                {
                    aRemainder = argArray[1];
                    return 2;
                }
                else
                {
                    return 1;
                }
            }

            return 0;
        }

        private uint getModifyArgs(string aInput, out string aCounterName, out int aValue)
        {
            string[] argArray = aInput.Split(new[] { ' ' }, 2);

            aCounterName = "";
            aValue = 0;

            if (argArray.Length >= 1)
            {
                aCounterName = argArray[0];

                if (argArray.Length == 2)
                {
                    if (Int32.TryParse(argArray[1], out aValue))
                        return 2;
                }

                return 1;
            }

            return 0;
        }

		public void forceGame(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				m_Game = argumentString;
                if (!aSilent)
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterGameSet"), argumentString));
            }
		}

		public void clearGame(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			if (!string.IsNullOrEmpty(m_Game))
			{
				m_Game = null;
                if (!aSilent)
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterGameClear"), getGameString()));
            }
		}

        private void setValue(userEntry commandUser, string argumentString, string commandName = "set", bool aSilent = false)
        {
            string countName;
            int countValue;
            uint argCount = getModifyArgs(argumentString, out countName, out countValue);

            if (argCount >= 1)
            {
                if (argCount == 1)
                {
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("counterValueSetEmpty"));
                }

                counterEntry curCounter = checkCreateEntry(countName);

                if (curCounter.Owner == null || curCounter.Owner.ToLower() == commandUser.Nickname.ToLower())
                {
                    if (curCounter.set(countValue))
                    {
                        if (!aSilent)
                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterValueSet"), curCounter.Name, curCounter.Count, curCounter.Game));
                    }
                        
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterValueSetFailOwner"), commandName, curCounter.Owner, curCounter.Name));
                }
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("counterNotSpecified"));
            }
        }

		public void set(userEntry commandUser, string argumentString, bool aSilent = false)
		{
            setValue(commandUser, argumentString, "set", aSilent);
		}

		public void add(userEntry commandUser, string argumentString, bool aSilent = false)
		{
            string countName;
            int countValue;
            uint argCount = getModifyArgs(argumentString, out countName, out countValue);

            if (argCount >= 1)
            {
                counterEntry curCounter = checkCreateEntry(countName);

                if (argCount == 1)
                    countValue = curCounter.Count + 1;
                else
                    countValue = curCounter.Count + countValue;

                setValue(commandUser, countName + " " + countValue, "add", aSilent);
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("counterNotSpecified"));
            }
        }

		public void remove(userEntry commandUser, string argumentString, bool aSilent = false)
		{
            string countName;
            int countValue;
            uint argCount = getModifyArgs(argumentString, out countName, out countValue);

            if (argCount >= 1)
            {
                counterEntry curCounter = checkCreateEntry(countName);

                if (argCount == 1)
                    countValue = curCounter.Count - 1;
                else
                    countValue = curCounter.Count - countValue;

                setValue(commandUser, countName + " " + countValue, "remove", aSilent);
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("counterNotSpecified"));
            }
		}

		public void display(userEntry commandUser, string argumentString, bool aSilent = false)
		{
            string counterName;
            string argument;

            if (getSimpleArgs(argumentString, out counterName, out argument) >= 1)
            {
                counterEntry curCount = checkCreateEntry(counterName, false);

                if (curCount != null)
                {
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterDisplay"), curCount.Name, curCount.Count, curCount.Game));
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterFailNotFound"), argumentString));
                }
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("counterNotSpecified"));
            }
		}

		public void delete(userEntry commandUser, string argumentString, bool aSilent = false)
		{
            string countName;
            int countValue;
            uint argCount = getModifyArgs(argumentString, out countName, out countValue);

            if (argCount == 1)
            {
                counterEntry curCounter = checkCreateEntry(countName, false);
                string useGame = getGameString();

                if (curCounter == null && useGame != GAME_NOGAME)
                {
                    useGame = GAME_NOGAME;
                    curCounter = checkCreateEntry(countName, false, useGame);
                }

                if (curCounter != null)
                {
                    string name = curCounter.Name;
                    string game = curCounter.Game;

                    if (curCounter.delete())
                    {
                        m_Entries[game].Remove(name);
                        if (!aSilent)
                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterRemoveSuccess"), name, game));
                    }
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterFailNotFound"), countName));
                }
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("counterNotSpecified"));
            }
		}

		public void describe(userEntry commandUser, string argumentString, bool aSilent = false)
		{
            string counterName;
            string argument;
            uint argCount = getSimpleArgs(argumentString, out counterName, out argument);

            if (argCount >= 1)
            {
                if (argCount == 2)
                {
                    counterEntry curCount = checkCreateEntry(counterName);

                    if (curCount.describe(argument))
                    {
                        if (aSilent)
                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterDescriptionUpdateSuccess"), curCount.Name));
                    }
                    else
                    {
                        // TODO: failure message?
                    }
                        
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("counterDescriptionUpdateFailEmpty"));
                }
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("counterNotSpecified"));
            }
		}

		public void about(userEntry commandUser, string argumentString, bool aSilent = false)
		{
            string counterName;
            string argument;

            if (getSimpleArgs(argumentString, out counterName, out argument) >= 1)
            {
                counterEntry curCount = checkCreateEntry(counterName, false);

                if (curCount != null)
                {
                    if (!string.IsNullOrEmpty(curCount.Description))
                        m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterDescriptionDisplay"), curCount.Name, curCount.Description));
                    else
                        m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterDescriptionUpdateFailEmpty"), curCount.Name));
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterFailNotFound"), counterName));
                }
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("counterNotSpecified"));
            }
		}

		public void setOwner(userEntry commandUser, string argumentString, bool aSilent = false)
		{
            string counterName;
            string argument;
            uint argCount = getSimpleArgs(argumentString, out counterName, out argument);

            if (argCount >= 1)
            {
                if (argCount == 2)
                {
                    counterEntry curCount = checkCreateEntry(counterName);

                    curCount.Owner = argument;
                    if (!aSilent)
                        m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterOwnerSetSuccess"), curCount.Name, argument));
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("counterOwnerSetFailEmpty"));
                }
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("counterNotSpecified"));
            }
		}

		public void clearOwner(userEntry commandUser, string argumentString, bool aSilent = false)
		{
            string counterName;
            string argument;
            uint argCount = getSimpleArgs(argumentString, out counterName, out argument);

            if (!string.IsNullOrEmpty(argumentString))
            {
                counterEntry curCount = checkCreateEntry(argumentString, false);

                curCount.Owner = null;
                if (!aSilent)
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("counterOwnerClear"), curCount.Name));
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("counterNotSpecified"));
            }
		}

        public void outputList(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            string getCountersQuery = "SELECT * FROM counters ORDER BY game ASC, name ASC";

            SQLiteCommand getCountersCommand = new SQLiteCommand(getCountersQuery, m_BotBrain.storageDB);
            SQLiteDataReader getCountersReader = getCountersCommand.ExecuteReader();

            List<object> rowData = new List<object>();

            if (getCountersReader.HasRows)
            {
                while (getCountersReader.Read())
                {
                    rowData.Add(new { game = Convert.ToString(getCountersReader["game"]), name = Convert.ToString(getCountersReader["name"]), count = Convert.ToString(getCountersReader["count"]) });
                }
            }

            m_BotBrain.genericSerializeToFile(rowData, "jerpdoesbots_counters.json");

            if (!aSilent)
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("counterOutputListSuccess"));
        }

        public counter(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			string createCounterTableQuery = "CREATE TABLE IF NOT EXISTS counters (counterID INTEGER PRIMARY KEY ASC, name TEXT, game TEXT, description TEXT, count INTEGER, UNIQUE(name, game))";
			SQLiteCommand createCounterTableCommand = new SQLiteCommand(createCounterTableQuery, m_BotBrain.storageDB);
			createCounterTableCommand.ExecuteNonQuery();

			m_Entries = new Dictionary<string, Dictionary<string, counterEntry>>();
            checkInitializeList(GAME_NOGAME);

            chatCommandDef tempDef;

			tempDef = new chatCommandDef("count", display, true, true);
			tempDef.addSubCommand(new chatCommandDef("add", add, true, false));
			tempDef.addSubCommand(new chatCommandDef("remove", remove, true, false));
			tempDef.addSubCommand(new chatCommandDef("delete", delete, true, false));
			tempDef.addSubCommand(new chatCommandDef("describe", describe, true, false));
			tempDef.addSubCommand(new chatCommandDef("set", set, true, false));
			tempDef.addSubCommand(new chatCommandDef("forcegame", forceGame, true, false));
			tempDef.addSubCommand(new chatCommandDef("cleargame", clearGame, true, false));
			tempDef.addSubCommand(new chatCommandDef("setowner", setOwner, false, false));
			tempDef.addSubCommand(new chatCommandDef("clearowner", clearOwner, false, false));
			tempDef.addSubCommand(new chatCommandDef("about", about, true, true));
            tempDef.addSubCommand(new chatCommandDef("outputlist", outputList, false, false));
            m_BotBrain.addChatCommand(tempDef);
		}
	}
}
