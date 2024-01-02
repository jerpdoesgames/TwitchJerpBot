using System.Data.SQLite;
using System;

namespace JerpDoesBots
{
	class gameCommand : commandModule
	{
		public override void initTable()
		{
			insertQuery = "INSERT OR IGNORE INTO commands_game (submitter, modifier, command_name, lastmod, allow_normal, message, game) values (@param1, @param2, @param3, @param4, @param5, @param6, @param7)";
			selectQuery = "SELECT * FROM commands_game WHERE command_name = @param1 AND game = @param2 LIMIT 1";
			createQuery = "CREATE TABLE IF NOT EXISTS commands_game (commandID INTEGER PRIMARY KEY ASC, command_name TEXT, submitter TEXT, modifier TEXT, lastmod INTEGER, allow_normal INTEGER, game TEXT, message TEXT, UNIQUE(command_name, game))";
			removeQuery = "DELETE FROM commands_game WHERE command_name=@param1 AND game=@param2";
			selectAllQuery = "SELECT * FROM commands_game ORDER BY game ASC, command_name ASC";
			outputListFilename = "jerpdoesbots_commands_game.json";
			outputListMessageSuccess = jerpBot.instance.localizer.getString("commandGameOutputListSuccess");

			base.initTable();
		}

		public override object getCurEntryJsonObject(SQLiteDataReader aEntryReader)
		{
			return new { name = Convert.ToString(aEntryReader["command_name"]), message = Convert.ToString(aEntryReader["message"]), game = Convert.ToString(aEntryReader["game"]) };
		}

		// TODO: variant that applies properties before creating table
		public gameCommand() : base()
		{
			formatHint = jerpBot.instance.localizer.getString("commandGameFormatHint");
			chatCommandDef tempDef = new chatCommandDef("gamecommand", null, false, false);
			tempDef.addSubCommand(new chatCommandDef("add", add, true, false));
			tempDef.addSubCommand(new chatCommandDef("remove", remove, true, false));
			tempDef.addSubCommand(new chatCommandDef("setgame", setGame, true, false));
			tempDef.addSubCommand(new chatCommandDef("cleargame", clearGame, true, false));
			tempDef.addSubCommand(new chatCommandDef("outputlist", outputList, false, false));
			jerpBot.instance.addChatCommand(tempDef);

		}
	}
}
