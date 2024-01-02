﻿using System;
using System.Data.SQLite;
using System.Collections.Generic;

namespace JerpDoesBots
{
	class quotes : botModule
	{
		/// <summary>
		/// Add a note quote.
		/// </summary>
		/// <param name="commandUser">User attempting to add a quote.</param>
		/// <param name="argumentString">Text of the quote to add.</param>
		/// <param name="aSilent">Whether to output a message on success/failure.</param>
		public void add(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			if (argumentString.Length > 0)
			{
				string addQuoteQuery = "INSERT INTO quotes (submitter, message, game) values (@param1, @param2, @param3)";

				SQLiteCommand addQuoteCommand = new SQLiteCommand(addQuoteQuery, jerpBot.instance.storageDB);

				string gameString;

				if (jerpBot.instance.game.Length > 0)
					gameString = jerpBot.instance.game;
				else
					gameString = "No Game";

				addQuoteCommand.Parameters.Add(new SQLiteParameter("@param1", commandUser.Nickname));
				addQuoteCommand.Parameters.Add(new SQLiteParameter("@param2", argumentString));
				addQuoteCommand.Parameters.Add(new SQLiteParameter("@param3", gameString));

				if (addQuoteCommand.ExecuteNonQuery() > 0)
				{
					long lastInsertID = jerpBot.instance.storageDB.LastInsertRowId;

					if (!aSilent)
						jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("quoteAddSuccess"), lastInsertID));
				}
				else
				{
					jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("quoteAddFail"));
				}
			} else
			{
				jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("quoteAddMissingData")) ;
			}
		}

		/// <summary>
		/// Displays a quote.  Called when passing no arguments or passing a numeric argument.
		/// </summary>
		/// <param name="commandUser">User attempting to display a quote.</param>
		/// <param name="argumentString">Optional - integer ID for a specific quote to display.</param>
		/// <param name="aSilent">Whether to output on success/failure.  Will always display a succesfully retrieved quote.  False by default.</param>
		public void display(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			int quoteID;

			if (Int32.TryParse(argumentString, out quoteID))
			{
				string getQuoteQuery = "SELECT * FROM quotes WHERE quoteID=" + quoteID + " LIMIT 1";

				SQLiteCommand getQuoteCommand = new SQLiteCommand(getQuoteQuery, jerpBot.instance.storageDB);
				SQLiteDataReader getQuoteReader = getQuoteCommand.ExecuteReader();

				if (getQuoteReader.HasRows && getQuoteReader.Read())
				{
					string submitter = Convert.ToString(getQuoteReader["submitter"]);
					string message = Convert.ToString(getQuoteReader["message"]);
					string game = Convert.ToString(getQuoteReader["game"]);


					jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("quoteDisplay"), quoteID, message, game));
				} else
				{
					jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("quoteNotFound"));
				}
			} else
			{
				random(commandUser, argumentString);
			}
		}

		/// <summary>
		/// Remove a quote from the database.
		/// </summary>
		/// <param name="commandUser">Unused.  User attempting to remove a quote.</param>
		/// <param name="argumentString">ID of the quote to remove.</param>
		/// <param name="aSilent">Whether to output a message on success.</param>
		public void remove(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			int quoteID;
			if (Int32.TryParse(argumentString, out quoteID))
			{
				string removeQuoteQuery = "DELETE FROM quotes WHERE quoteID=" + quoteID;

				SQLiteCommand removeQuoteCommand = new SQLiteCommand(removeQuoteQuery, jerpBot.instance.storageDB);

				if (removeQuoteCommand.ExecuteNonQuery() > 0)
				{
					if (!aSilent)
						jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("quoteRemove"), quoteID));
				}
				else
				{
					jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("quoteNotFound"));
				}
			}
		}

		/// <summary>
		/// Output a random quote.
		/// </summary>
		/// <param name="commandUser">Unused.  User attempting to display a quote.</param>
		/// <param name="argumentString">Unused</param>
		/// <param name="aSilent">Whether to display a message on failure.</param>
		public void random(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			string getQuoteQuery = "SELECT * FROM quotes ORDER BY RANDOM() LIMIT 1";

			SQLiteCommand getQuoteCommand = new SQLiteCommand(getQuoteQuery, jerpBot.instance.storageDB);
			SQLiteDataReader getQuoteReader = getQuoteCommand.ExecuteReader();

			if (getQuoteReader.HasRows && getQuoteReader.Read())
			{
				string submitter = Convert.ToString(getQuoteReader["submitter"]);
				string message = Convert.ToString(getQuoteReader["message"]);
				string game = Convert.ToString(getQuoteReader["game"]);
				string quoteID = Convert.ToString(getQuoteReader["quoteID"]);

				jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("quoteDisplay"), quoteID, message, game));
			}
			else if (!aSilent)
			{
				jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("quoteNoneFound"));
			}
		}

        public override void onOutputDataRequest()
        {
			outputListInternal();
        }

        public void outputListInternal()
		{
            string getQuotesQuery = "SELECT * FROM quotes";

            SQLiteCommand getQuotesCommand = new SQLiteCommand(getQuotesQuery, jerpBot.instance.storageDB);
            SQLiteDataReader getQuotesReader = getQuotesCommand.ExecuteReader();

            List<object> rowData = new List<object>();

            if (getQuotesReader.HasRows)
            {
                while (getQuotesReader.Read())
                {
                    rowData.Add(new { id = Convert.ToString(getQuotesReader["quoteID"]), message = Convert.ToString(getQuotesReader["message"]), game = Convert.ToString(getQuotesReader["game"]) });
                }
            }

            jerpBot.instance.genericSerializeToFile(rowData, "jerpdoesbots_quotes.json");
        }

        public void outputList(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			outputListInternal();

			if (!aSilent)
				jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("quoteOutputListSuccess"));
		}

        /// <summary>
        /// Output a localized string (quoteList) which includes a link to a list of quotes.
        /// </summary>
        /// <param name="commandUser">Unused.  User attempting to output a link to a list of quotes.</param>
        /// <param name="argumentString">Unused.</param>
        /// <param name="aSilent">Unused.</param>
        public void quoteListCommand(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("quoteList"));
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
					SQLiteCommand editQuoteCommand = new SQLiteCommand(editQuoteQuery, jerpBot.instance.storageDB);
					editQuoteCommand.Parameters.Add(new SQLiteParameter("@param1", argumentList[1]));
					editQuoteCommand.Parameters.Add(new SQLiteParameter("@param2", quoteID));

					if (editQuoteCommand.ExecuteNonQuery() > 0)
					{
						if (!aSilent)
							jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("quoteUpdated"), quoteID));
					}
					else
					{
						jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("quoteNotFound"));
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
					SQLiteCommand editQuoteCommand = new SQLiteCommand(editQuoteQuery, jerpBot.instance.storageDB);
					editQuoteCommand.Parameters.Add(new SQLiteParameter("@param1", argumentList[1]));
					editQuoteCommand.Parameters.Add(new SQLiteParameter("@param2", quoteID));

					if (editQuoteCommand.ExecuteNonQuery() > 0)
					{
						if (!aSilent)
							jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("quoteUpdated"), quoteID));
					}
					else
					{
						jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("quoteNotFound"));
					}
				}
			}
		}

		public quotes() : base(true, true, false)
		{
			string createQuoteTableQuery = "CREATE TABLE IF NOT EXISTS quotes (quoteID INTEGER PRIMARY KEY ASC, submitter TEXT, message TEXT, game TEXT)";
			SQLiteCommand createQuoteTableCommand = new SQLiteCommand(createQuoteTableQuery, jerpBot.instance.storageDB);
			createQuoteTableCommand.ExecuteNonQuery();

			chatCommandDef tempDef = new chatCommandDef("quote", display, true, true);
			tempDef.addSubCommand(new chatCommandDef("add", add, true, false));
			tempDef.addSubCommand(new chatCommandDef("remove", remove, true, false));
			tempDef.addSubCommand(new chatCommandDef("edit", edit, true, false));
			tempDef.addSubCommand(new chatCommandDef("setgame", setGame, true, false));
			tempDef.addSubCommand(new chatCommandDef("outputlist", outputList, false, false));
            tempDef.addSubCommand(new chatCommandDef("list", quoteListCommand, true, true));
            // TODO: Add command to return total number of quotes.
            jerpBot.instance.addChatCommand(tempDef);
		}
	}
}
