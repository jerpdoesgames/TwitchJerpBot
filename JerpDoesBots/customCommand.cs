namespace JerpDoesBots
{
	class customCommand : commandModule
	{

		public customCommand(jerpBot aJerpBot) : base(aJerpBot)
		{
			chatCommandDef tempDef = new chatCommandDef("command", null, false, false);
			tempDef.addSubCommand(new chatCommandDef("add", add, true, false));
			tempDef.addSubCommand(new chatCommandDef("remove", remove, true, false));

			m_BotBrain.addChatCommand(tempDef);
		}
	}
}
