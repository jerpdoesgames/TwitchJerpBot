﻿using System;
using System.Data.SQLite;

namespace JerpDoesBots
{
	class quotes : botModule
	{
		public void add(userEntry commandUser, string argumentString)
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

					m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("quoteAddSuccess"), lastInsertID));
				}
				else
				{
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("quoteAddFail"));
				}
			} else
			{
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("quoteAddMissingData")) ;
			}
		}

		public void display(userEntry commandUser, string argumentString)
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


					m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("quoteDisplay"), quoteID, message, game));
				} else
				{
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("quoteNotFound"));
				}
			} else
			{
				random(commandUser, argumentString);
			}
		}

		public void remove(userEntry commandUser, string argumentString)
		{
			int quoteID;
			if (Int32.TryParse(argumentString, out quoteID))
			{
				string removeQuoteQuery = "DELETE FROM quotes WHERE quoteID=" + quoteID;

				SQLiteCommand removeQuoteCommand = new SQLiteCommand(removeQuoteQuery, m_BotBrain.storageDB);

				if (removeQuoteCommand.ExecuteNonQuery() > 0)
				{
					m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("quoteRemove"), quoteID));
				}
				else
				{
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("quoteNotFound"));
				}
			}
		}

		public void random(userEntry commandUser, string argumentString)
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

				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("quoteDisplay"), quoteID, message, game));
			}
			else
			{
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("quoteNoneFound"));
			}
		}

		public void edit(userEntry commandUser, string argumentString)
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
						m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("quoteUpdated"), quoteID));
					}
					else
					{
						m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("quoteNotFound"));
					}
				}
			}
		}

		public void setGame(userEntry commandUser, string argumentString)
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
						m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("quoteUpdated"), quoteID));
					}
					else
					{
						m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("quoteNotFound"));
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
			// TODO: Add command to return total number of quotes.
			m_BotBrain.addChatCommand(tempDef);
		}
	}
}
