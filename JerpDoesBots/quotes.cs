using System;
using System.Data.SQLite;
using System.Collections.Generic;

namespace JerpDoesBots
{
	class quotes : botModule
	{
		public void add(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			if (argumentString.Length > 0)
			{
				string addQuoteQuery = "INSERT INTO quotes (submitter, message, game) values (@param1, @param2, @param3)";

				SQLiteCommand addQuoteCommand = new SQLiteCommand(addQuoteQuery, m_BotBrain.storageDB);

				string gameString;

				if (m_BotBrain.game.Length > 0)
					gameString = m_BotBrain.game;
				else
					gameString = "No Game";

				addQuoteCommand.Parameters.Add(new SQLiteParameter("@param1", commandUser.Nickname));
				addQuoteCommand.Parameters.Add(new SQLiteParameter("@param2", argumentString));
				addQuoteCommand.Parameters.Add(new SQLiteParameter("@param3", gameString));

				if (addQuoteCommand.ExecuteNonQuery() > 0)
				{
					long lastInsertID = m_BotBrain.storageDB.LastInsertRowId;

					if (!aSilent)
						m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("quoteAddSuccess"), lastInsertID));
				}
				else
				{
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("quoteAddFail"));
				}
			} else
			{
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("quoteAddMissingData")) ;
			}
		}

		public void display(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			int quoteID;

			if (Int32.TryParse(argumentString, out quoteID))
			{
				string getQuoteQuery = "SELECT * FROM quotes WHERE quoteID=" + quoteID + " LIMIT 1";

				SQLiteCommand getQuoteCommand = new SQLiteCommand(getQuoteQuery, m_BotBrain.storageDB);
				SQLiteDataReader getQuoteReader = getQuoteCommand.ExecuteReader();

				if (getQuoteReader.HasRows && getQuoteReader.Read())
				{
					string submitter = Convert.ToString(getQuoteReader["submitter"]);
					string message = Convert.ToString(getQuoteReader["message"]);
					string game = Convert.ToString(getQuoteReader["game"]);


					m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("quoteDisplay"), quoteID, message, game));
				} else
				{
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("quoteNotFound"));
				}
			} else
			{
				random(commandUser, argumentString);
			}
		}

		public void remove(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			int quoteID;
			if (Int32.TryParse(argumentString, out quoteID))
			{
				string removeQuoteQuery = "DELETE FROM quotes WHERE quoteID=" + quoteID;

				SQLiteCommand removeQuoteCommand = new SQLiteCommand(removeQuoteQuery, m_BotBrain.storageDB);

				if (removeQuoteCommand.ExecuteNonQuery() > 0)
				{
					if (!aSilent)
						m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("quoteRemove"), quoteID));
				}
				else
				{
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("quoteNotFound"));
				}
			}
		}

		public void random(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			string getQuoteQuery = "SELECT * FROM quotes ORDER BY RANDOM() LIMIT 1";

			SQLiteCommand getQuoteCommand = new SQLiteCommand(getQuoteQuery, m_BotBrain.storageDB);
			SQLiteDataReader getQuoteReader = getQuoteCommand.ExecuteReader();

			if (getQuoteReader.HasRows && getQuoteReader.Read())
			{
				string submitter = Convert.ToString(getQuoteReader["submitter"]);
				string message = Convert.ToString(getQuoteReader["message"]);
				string game = Convert.ToString(getQuoteReader["game"]);
				string quoteID = Convert.ToString(getQuoteReader["quoteID"]);

				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("quoteDisplay"), quoteID, message, game));
			}
			else
			{
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("quoteNoneFound"));
			}
		}

        public override void onOutputDataRequest()
        {
			outputListInternal();
        }

        public void outputListInternal()
		{
            string getQuotesQuery = "SELECT * FROM quotes";

            SQLiteCommand getQuotesCommand = new SQLiteCommand(getQuotesQuery, m_BotBrain.storageDB);
            SQLiteDataReader getQuotesReader = getQuotesCommand.ExecuteReader();

            List<object> rowData = new List<object>();

            if (getQuotesReader.HasRows)
            {
                while (getQuotesReader.Read())
                {
                    rowData.Add(new { id = Convert.ToString(getQuotesReader["quoteID"]), message = Convert.ToString(getQuotesReader["message"]), game = Convert.ToString(getQuotesReader["game"]) });
                }
            }

            m_BotBrain.genericSerializeToFile(rowData, "jerpdoesbots_quotes.json");
        }

		public void outputList(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			outputListInternal();

			if (!aSilent)
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("quoteOutputListSuccess"));
		}

        public void quoteListCommand(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("quoteList"));
        }

        public void edit(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			string[] argumentList = argumentString.Split(new[] { ' ' }, 2);
			if (argumentList.Length == 2)
			{
				int quoteID;
				if (Int32.TryParse(argumentList[0], out quoteID))
				{
					string editQuoteQuery = "UPDATE quotes SET message=@param1 WHERE quoteID=@param2";
					SQLiteCommand editQuoteCommand = new SQLiteCommand(editQuoteQuery, m_BotBrain.storageDB);
					editQuoteCommand.Parameters.Add(new SQLiteParameter("@param1", argumentList[1]));
					editQuoteCommand.Parameters.Add(new SQLiteParameter("@param2", quoteID));

					if (editQuoteCommand.ExecuteNonQuery() > 0)
					{
						if (!aSilent)
							m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("quoteUpdated"), quoteID));
					}
					else
					{
						m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("quoteNotFound"));
					}
				}
			}
		}

		public void setGame(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			string[] argumentList = argumentString.Split(new[] { ' ' }, 2);
			if (argumentList.Length == 2)
			{
				int quoteID;
				if (Int32.TryParse(argumentList[0], out quoteID))
				{
					string editQuoteQuery = "UPDATE quotes SET game=@param1 WHERE quoteID=@param2";
					SQLiteCommand editQuoteCommand = new SQLiteCommand(editQuoteQuery, m_BotBrain.storageDB);
					editQuoteCommand.Parameters.Add(new SQLiteParameter("@param1", argumentList[1]));
					editQuoteCommand.Parameters.Add(new SQLiteParameter("@param2", quoteID));

					if (editQuoteCommand.ExecuteNonQuery() > 0)
					{
						if (!aSilent)
							m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("quoteUpdated"), quoteID));
					}
					else
					{
						m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("quoteNotFound"));
					}
				}
			}
		}

		public quotes(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			string createQuoteTableQuery = "CREATE TABLE IF NOT EXISTS quotes (quoteID INTEGER PRIMARY KEY ASC, submitter TEXT, message TEXT, game TEXT)";
			SQLiteCommand createQuoteTableCommand = new SQLiteCommand(createQuoteTableQuery, m_BotBrain.storageDB);
			createQuoteTableCommand.ExecuteNonQuery();

			chatCommandDef tempDef = new chatCommandDef("quote", display, true, true);
			tempDef.addSubCommand(new chatCommandDef("add", add, true, false));
			tempDef.addSubCommand(new chatCommandDef("remove", remove, true, false));
			tempDef.addSubCommand(new chatCommandDef("edit", edit, true, false));
			tempDef.addSubCommand(new chatCommandDef("setgame", setGame, true, false));
			tempDef.addSubCommand(new chatCommandDef("outputlist", outputList, false, false));
            tempDef.addSubCommand(new chatCommandDef("list", quoteListCommand, true, true));
            // TODO: Add command to return total number of quotes.
            m_BotBrain.addChatCommand(tempDef);
		}
	}
}
