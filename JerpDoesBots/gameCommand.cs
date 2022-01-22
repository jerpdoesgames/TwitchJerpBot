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

			base.initTable();
		}

		// TODO: variant that applies properties before creating table
		public gameCommand(jerpBot botGeneral) : base(botGeneral)
		{
			formatHint = m_BotBrain.localizer.getString("commandGameFormatHint");
			chatCommandDef tempDef = new chatCommandDef("gamecommand", null, false, false);
			tempDef.addSubCommand(new chatCommandDef("add", add, true, false));
			tempDef.addSubCommand(new chatCommandDef("remove", remove, true, false));
			tempDef.addSubCommand(new chatCommandDef("setgame", setGame, true, false));
			tempDef.addSubCommand(new chatCommandDef("cleargame", clearGame, true, false));
			m_BotBrain.addChatCommand(tempDef);

		}
	}
}
