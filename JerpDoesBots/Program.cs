namespace JerpDoesBots
{
	class Program
	{
		static void Main(string[] args)
		{
			jerpBot.checkCreateBotStorage();
			jerpBot.checkCreateBotDatabase();

			logger logGeneral		= new logger("log_general.txt");

			botConfig tempConfig = new botConfig();
			botConnection connConfig;

			if (tempConfig.loaded && tempConfig.configData.connections.Count > 0)
				connConfig = tempConfig.configData.connections[0];
			else
				return;

			jerpBot botGeneral					= new jerpBot(logGeneral, tempConfig);

			raffle raffleModule					= new raffle(botGeneral);
			quotes quoteModule					= new quotes(botGeneral);
			customCommand customCommandModule	= new customCommand(botGeneral);
			gameCommand gameCommandModule		= new gameCommand(botGeneral);
			counter counterModule				= new counter(botGeneral);
			queueSystem queueModule				= new queueSystem(botGeneral);
			autoShoutout shoutoutModule			= new autoShoutout(botGeneral);
            lurkShoutout lurkShoutModule        = new lurkShoutout(botGeneral);
            messageRoll rollModule				= new messageRoll(botGeneral);
			pollManager pollModule				= new pollManager(botGeneral);
            soundCommands soundManager	        = new soundCommands(botGeneral);
            commandAlias aliasManager           = new commandAlias(botGeneral);
            trivia triviaManager                = new trivia(botGeneral);
            hydrateReminder hydrateManager      = new hydrateReminder(botGeneral);
            delaySender delaySendManager        = new delaySender(botGeneral);
			hostMessages hostMessageModule		= new hostMessages(botGeneral);

			customCommandModule.initTable();
			gameCommandModule.initTable();
            aliasManager.initTable();

			botGeneral.CustomCommandModule = customCommandModule;
			botGeneral.GameCommandModule = gameCommandModule;
            botGeneral.SoundCommandModule = soundManager;
            botGeneral.AliasModule = aliasManager;

            while (!botGeneral.isReadyToClose)
            {
                botGeneral.frame();
            }

		}
	}
}
