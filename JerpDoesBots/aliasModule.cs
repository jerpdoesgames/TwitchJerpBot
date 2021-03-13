using System;
using System.Data.SQLite;

namespace JerpDoesBots
{
    class commandAlias : commandModule
    {
        public override void initTable()
        {
            insertQuery = "INSERT OR IGNORE INTO command_alias (submitter, modifier, command_name, lastmod, message) values (@param1, @param2, @param3, @param4, @param5)";
            selectQuery = "SELECT * FROM command_alias WHERE command_name = @param1 LIMIT 1";
            createQuery = "CREATE TABLE IF NOT EXISTS command_alias (commandID INTEGER PRIMARY KEY ASC, command_name TEXT unique, submitter TEXT, modifier TEXT, lastmod INTEGER, message TEXT)";
            formatHint = "Expected format: !alias add [name] [commands (separate with |)]";
            removeQuery = "DELETE FROM command_alias WHERE command_name=@param1";

            base.initTable();
        }

        public string[] loadAlias(string aliasName)
        {
            if (!string.IsNullOrEmpty(aliasName))
            {
                SQLiteDataReader getCommandReader = loadCommand(aliasName);

                if (getCommandReader.HasRows && getCommandReader.Read())
                {
                    string message = Convert.ToString(getCommandReader["message"]);
                    return message.Split('|');
                }
            }

            return null;
        }

        public override void add(userEntry commandUser, string argumentString)
        {
            bool usesExistingAlias = false;

            string[] commandList = argumentString.Split('|');
            string curCommandName;

            for (int i=0; i < commandList.Length; i++)
            {
                curCommandName = jerpBot.getCommandName(commandList[i]);
                if (!string.IsNullOrEmpty(curCommandName))
                {
                    // See if this command name exists
                    if (get(curCommandName) != null)
                    {
                        usesExistingAlias = true;
                        break;
                    }
                }
                
            }

            if (!usesExistingAlias)
            {
                string[] argumentList = argumentString.Split(new[] { ' ' }, 2);

                if (
                    argumentList.Length == 2 &&
                    !string.IsNullOrEmpty(argumentList[0]) &&
                    !string.IsNullOrEmpty(argumentList[1])
                )
                {
                    string commandName = argumentList[0];

                    SQLiteDataReader getCommandReader = loadCommand(commandName);

                    if (getCommandReader.HasRows)
                    {
                        m_BotBrain.sendDefaultChannelMessage("Unable to add alias '" + commandName + "' - already exists.");
                    }
                    else
                    {
                        string addCommandQuery = insertQuery;

                        SQLiteCommand addCommandCommand = new SQLiteCommand(addCommandQuery, m_BotBrain.BotData);

                        addCommandCommand.Parameters.Add(new SQLiteParameter("@param1", commandUser.Nickname));     // Submitter
                        addCommandCommand.Parameters.Add(new SQLiteParameter("@param2", commandUser.Nickname));     // Modifier (same)
                        addCommandCommand.Parameters.Add(new SQLiteParameter("@param3", commandName));          // Command Name
                        addCommandCommand.Parameters.Add(new SQLiteParameter("@param4", 423432434));                // Last Modified (timestamp)
                        addCommandCommand.Parameters.Add(new SQLiteParameter("@param5", argumentList[1]));          // Message

                        if (addCommandCommand.ExecuteNonQuery() > 0)
                            m_BotBrain.sendDefaultChannelMessage("Alias '" + argumentList[0] + "' added!");
                        else
                            m_BotBrain.sendDefaultChannelMessage("Unable to add Alias '" + argumentList[0] + "'");
                    }
                }
                else
                    m_BotBrain.sendDefaultChannelMessage(formatHint);

            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage("New alias makes reference to existing aliases (prohibited to avoid circular references).");
            }
            
        }

        public commandAlias(jerpBot botGeneral) : base(botGeneral)
        {
            chatCommandDef tempDef = new chatCommandDef("alias", null, false, false);
            tempDef.addSubCommand(new chatCommandDef("add", add, false, false));
            tempDef.addSubCommand(new chatCommandDef("remove", remove, false, false));
            m_BotBrain.addChatCommand(tempDef);

        }
    }
}
