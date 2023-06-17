namespace JerpDoesBots
{
	class customCommand : commandModule
	{

		public override void initTable()
		{
			outputListMessageSuccess = m_BotBrain.localizer.getString("commandOutputListSuccess");

			base.initTable();
		}

		public customCommand(jerpBot aJerpBot) : base(aJerpBot)
		{
			chatCommandDef tempDef = new chatCommandDef("command", null, false, false);
			tempDef.addSubCommand(new chatCommandDef("add", add, true, false));
			tempDef.addSubCommand(new chatCommandDef("remove", remove, true, false));
			tempDef.addSubCommand(new chatCommandDef("outputlist", outputList, false, false));

			m_BotBrain.addChatCommand(tempDef);
		}
	}
}
