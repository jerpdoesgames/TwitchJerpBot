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

			pointRewardManager pointRewardsModule     = new pointRewardManager(); // Keep this early as other modules will be dependent on the fist rewards list update.
			raffle raffleModule						  = new raffle();
			quotes quoteModule						  = new quotes();
			customCommand customCommandModule		  = new customCommand();
			gameCommand gameCommandModule			  = new gameCommand();
			counter counterModule					  = new counter();
			queueSystem queueModule					  = new queueSystem();
			autoShoutout shoutoutModule				  = new autoShoutout();
            lurkShoutout lurkShoutModule			  = new lurkShoutout();
            messageRoll rollModule					  = new messageRoll();
			pollManager pollModule					  = new pollManager();
            soundCommands soundManager				  = new soundCommands();
            commandAlias aliasManager				  = new commandAlias();
            trivia triviaManager					  = new trivia();
            hydrateReminder hydrateManager			  = new hydrateReminder();
            delaySender delaySendManager			  = new delaySender();
			hostMessages hostMessageModule			  = new hostMessages();
			streamProfiles streamProfileManager		  = new streamProfiles();
			predictionManager streamPredictionManager = new predictionManager();
			mediaPlayerMonitor mediaMonitor           = new mediaPlayerMonitor();
			dataLookup dataLookupManager              = new dataLookup();
			adManager adManagerModule                 = new adManager();
            autoExec autoExecModule                   = new autoExec();

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
                botGeneral.onFrame();
            }

		}
	}
}
