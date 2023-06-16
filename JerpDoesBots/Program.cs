namespace JerpDoesBots
{
	class Program
	{
		static void Main(string[] args)
		{
			jerpBot.checkCreateBotStorage();
			jerpBot.checkCreateBotDatabase();

			botConfig tempConfig = new botConfig();
			botConnection connConfig;

			if (tempConfig.loaded && tempConfig.configData.connections.Count > 0)
				connConfig = tempConfig.configData.connections[0];
			else
				return;

			jerpBot botGeneral					= new jerpBot(tempConfig);
			jerpBot.instance = botGeneral;

			raffle raffleModule						  = new raffle(botGeneral);
			quotes quoteModule						  = new quotes(botGeneral);
			customCommand customCommandModule		  = new customCommand(botGeneral);
			gameCommand gameCommandModule			  = new gameCommand(botGeneral);
			counter counterModule					  = new counter(botGeneral);
			queueSystem queueModule					  = new queueSystem(botGeneral);
			autoShoutout shoutoutModule				  = new autoShoutout(botGeneral);
            lurkShoutout lurkShoutModule			  = new lurkShoutout(botGeneral);
            messageRoll rollModule					  = new messageRoll(botGeneral);
			pollManager pollModule					  = new pollManager(botGeneral);
            soundCommands soundManager				  = new soundCommands(botGeneral);
            commandAlias aliasManager				  = new commandAlias(botGeneral);
            trivia triviaManager					  = new trivia(botGeneral);
            hydrateReminder hydrateManager			  = new hydrateReminder(botGeneral);
            delaySender delaySendManager			  = new delaySender(botGeneral);
			hostMessages hostMessageModule			  = new hostMessages(botGeneral);
			streamProfiles streamProfileManager		  = new streamProfiles(botGeneral);
			predictionManager streamPredictionManager = new predictionManager(botGeneral);
			mediaPlayerMonitor mediaMonitor           = new mediaPlayerMonitor(botGeneral);
			dataLookup dataLookupManager              = new dataLookup(botGeneral);
			adManager adManagerModule                 = new adManager(botGeneral);

			customCommandModule.initTable();
			gameCommandModule.initTable();
            aliasManager.initTable();

			botGeneral.customCommandModule = customCommandModule;
			botGeneral.gameCommandModule = gameCommandModule;
            botGeneral.soundCommandModule = soundManager;
            botGeneral.aliasModule = aliasManager;

			botGeneral.setLoadComplete();

            while (!botGeneral.isReadyToClose)
            {
                botGeneral.frame();
            }

		}
	}
}
