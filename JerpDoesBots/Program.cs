using Microsoft.Extensions.Hosting;
using System.IO;
using Microsoft.Extensions.Configuration;
using TwitchLib.EventSub.Websockets.Extensions;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace JerpDoesBots
{
	class Program
	{

        // ========================= REMOVE THIS SECTION EVENTUALLY ==========================
        private static void addSettingsDelegate(HostBuilderContext aContext, IConfigurationBuilder aBuilder)
        {
            // TODO: Figure out what we're doing here.
            // aBuilder.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));
        }

        private static void addServicesDelegate(HostBuilderContext hostContext, IServiceCollection services)
        {
            services.AddLogging();
            services.AddTwitchLibEventSubWebsockets();

            services.AddHostedService<TwitchWSHostedService>();
        }

        private static IHostBuilder CreateHostBuilder()
        {
            IHostBuilder newHostBuilder = Host.CreateDefaultBuilder();
            newHostBuilder = newHostBuilder.ConfigureAppConfiguration(addSettingsDelegate);

            newHostBuilder.ConfigureServices(addServicesDelegate);

            return newHostBuilder;
        }

        // ========================= REMOVE THIS SECTION EVENTUALLY ==========================





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
            announceManager announceModule = new announceManager();
            // TwitchEventSubHandler eventSubManager     = new TwitchEventSubHandler();

            customCommandModule.initTable();
			gameCommandModule.initTable();
            aliasManager.initTable();

			botGeneral.customCommandModule = customCommandModule;
			botGeneral.gameCommandModule = gameCommandModule;
            botGeneral.soundCommandModule = soundManager;
            botGeneral.aliasModule = aliasManager;
            // botGeneral.eventSubModule = eventSubManager;

            botGeneral.setLoadComplete();

            botGeneral.initiateSubscriptions();

            IHost TwitchEventHost = CreateHostBuilder().Build();
            botGeneral.TwitchEventHost = TwitchEventHost;
            TwitchEventHost.RunAsync();

            // ==========================================================

            while (!jerpBot.instance.isReadyToClose)
            {
                jerpBot.instance.onFrame();
            }

            TwitchEventHost.StopAsync();
        }
    }
}
