namespace JerpDoesBots
{
	class customCommand : commandModule
	{

		public override void initTable()
		{
			outputListMessageSuccess = jerpBot.instance.localizer.getString("commandOutputListSuccess");

			base.initTable();
		}

		public customCommand() : base()
		{
			chatCommandDef tempDef = new chatCommandDef("command", null, false, false);
			tempDef.addSubCommand(new chatCommandDef("add", add, true, false));
			tempDef.addSubCommand(new chatCommandDef("remove", remove, true, false));
			tempDef.addSubCommand(new chatCommandDef("outputlist", outputList, false, false));

			jerpBot.instance.addChatCommand(tempDef);
		}
	}
}
