using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using TwitchLib.Api;
using TwitchLib.Api.Services;
using TwitchLib.Api.Services.Events.LiveStreamMonitor;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using System.Threading.Tasks;

namespace JerpDoesBots
{
    class jerpBot
    {
        ConnectionCredentials m_TwitchCredentials;
        ConnectionCredentials m_TwitchCredentialsJerp;
        botConfig m_CoreConfig;
        TwitchClient m_TwitchClient;
        TwitchClient m_TwitchClientJerp;
        TwitchAPI m_TwitchAPI;
        LiveStreamMonitorService m_StreamMonitor;

        private DateTime m_LiveStartTime;
        private SQLiteConnection m_StorageDB;
        public SQLiteConnection storageDB { get { return m_StorageDB; } }
        private Stopwatch m_ActionTimer;
        private readonly Queue<connectionCommand> actionQueue;
        private bool m_IsDone = false;
        private static uint MESSAGE_VOTE_MAX_LENGTH = 20;
        private bool m_HasJoinedChannel = false;
        private bool m_HasChatConnection = false;
        private string m_DefaultChannel;
        private bool m_IsReadyToClose = false; // Ready to completely end the program

        Random m_Randomizer = new Random();

        public Random randomizer { get { return m_Randomizer; } }

        public bool isReadyToClose
        {
            get { return m_IsReadyToClose; }
            set { m_IsReadyToClose = value; }
        }

        public bool isDone {
            get {
                return m_IsDone;
            }

            set {

                if (!m_IsDone)
                    quit();

                m_IsDone = true;
            }
        }

        private List<botModule> m_Modules;

        public Stopwatch actionTimer { get { return m_ActionTimer; } }

        public static string storagePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JerpBot");

        private logger botLog;
        private long m_UserUpdateLast = 0;
        private long m_UserUpdateThrottle = 5000;

        private long m_SendTimeLast = 0;
        private static long m_SendThrottleMin = 1000;

        private bool m_IsLive = false;
        public bool IsLive { get { return m_IsLive; } set { m_IsLive = value; } }
        private string m_Title = "";
        public string Title { get { return m_Title; } set { m_Title = value; } }
        private int m_ViewersLast = 0;

        private string m_Game = "";
        public string game { get { return m_Game; } }

        private long m_LineCount = 0;   // Total lines
        public long lineCount { get { return m_LineCount; } }

        private List<chatCommandDef> chatCommandList;

        public void setLive(bool newLive)
        {
            m_IsLive = newLive;
        }

        public void addModule(botModule aModule)
        {
            m_Modules.Add(aModule);
        }

        private customCommand customCommandModule;
        public customCommand CustomCommandModule { set { customCommandModule = value; } }

        private gameCommand gameCommandModule;
        public gameCommand GameCommandModule { set { gameCommandModule = value; } }

        private soundCommands soundCommandModule;
        public soundCommands SoundCommandModule { set { soundCommandModule = value; } }

        private commandAlias m_AliasModule;

        public commandAlias AliasModule { set { m_AliasModule = value; } }

        private Dictionary<string, userEntry> m_UserList;
        public Dictionary<string, userEntry> userList { get { return m_UserList; } }

        public static void checkCreateBotStorage()
        {
            if (!Directory.Exists(jerpBot.storagePath))
            {
                Directory.CreateDirectory(jerpBot.storagePath);
            }
        }

        public static void checkCreateBotDatabase()
        {
            string databasePath = System.IO.Path.Combine(jerpBot.storagePath, "jerpBot.sqlite");
            if (!File.Exists(databasePath))
            {
                SQLiteConnection.CreateFile(databasePath);
            }
        }

        private long getCurrentThrottle()
        {
            return m_SendThrottleMin;
        }

        private bool isValidPrivMsg(connectionCommand commandToExecute)
        {
            return (!string.IsNullOrEmpty(commandToExecute.getMessage()) && !string.IsNullOrEmpty(commandToExecute.getTarget()));
        }

        public void executeAndLog(connectionCommand commandToExecute)
        {
            switch (commandToExecute.getCommandType())
            {
                case connectionCommand.types.channelMessage:
                    if (isValidPrivMsg(commandToExecute))
                    {
                        m_TwitchClient.SendMessage(commandToExecute.getTarget(), commandToExecute.getMessage());
                    }
                    break;

                case connectionCommand.types.joinChannel:
                    if (!String.IsNullOrEmpty(commandToExecute.getTarget()))
                    {
                        m_TwitchClient.JoinChannel(commandToExecute.getTarget());
                    }
                    break;

                case connectionCommand.types.partAllChannels:
                    for (int i = 0; i < m_TwitchClient.JoinedChannels.Count; i++)
                    {
                        m_TwitchClient.LeaveChannel(m_TwitchClient.JoinedChannels[i]);
                    }
                    break;

                case connectionCommand.types.partChannel:
                    if (!String.IsNullOrEmpty(commandToExecute.getTarget()))
                    {
                        m_TwitchClient.LeaveChannel(commandToExecute.getTarget());
                    }
                    break;

                case connectionCommand.types.privateMessage:
                    if (isValidPrivMsg(commandToExecute))
                    {
                        m_TwitchClient.SendWhisper(commandToExecute.getTarget(), commandToExecute.getMessage());
                    }
                    break;

                case connectionCommand.types.quit:
                    m_TwitchClient.Disconnect();
                    m_TwitchClientJerp.Disconnect();
                    m_IsReadyToClose = true;
                    isDone = true;
                    break;

                default:
                    Console.WriteLine("Unknown command type sent to ircConnection sendAndLog");
                    break;
            }
        }

        private void processActionQueue()
        {
            if (actionQueue.Count > 0)
            {
                if (m_ActionTimer.ElapsedMilliseconds > m_SendTimeLast + getCurrentThrottle())
                {
                    connectionCommand commandToExecute = actionQueue.Dequeue();
                    executeAndLog(commandToExecute);
                    m_SendTimeLast = m_ActionTimer.ElapsedMilliseconds;
                }
            }
        }

        /// <summary>
        /// Finds the name of the command assuming it begins with !.  Returns the name of the command (no !), and null if it can't be confirmed to be a command.
        /// </summary>
        public static string getCommandName(string aInput)
        {
            if (aInput.Length > 1 && aInput[0] == '!')
            {
                return aInput.Substring(1).Split(' ')[0];   // TODO: Find a space in a non-stupid way
            }

            return null;
        }

        private void queueAction(connectionCommand actionToExecute)
        {
            actionQueue.Enqueue(actionToExecute);
        }

        public void sendDefaultChannelMessage(string messageToSend, bool doQueue = true)
        {
            sendChannelMessage(m_DefaultChannel, messageToSend, doQueue);
        }

        public void sendChannelMessage(string targetChannel, string messageToSend, bool doQueue = true)
        {
            connectionCommand newCommand = new connectionCommand(connectionCommand.types.channelMessage);
            newCommand.setTarget(targetChannel);
            newCommand.setMessage(messageToSend);

            if (doQueue)
                queueAction(newCommand);
            else
                m_TwitchClient.SendMessage(m_DefaultChannel, messageToSend);
        }

        public void processUserUpdates(bool forceUpdate = false)    // For any users who needs anything written to DB
        {
            bool userWasUpdated = false;
            if (forceUpdate || m_ActionTimer.ElapsedMilliseconds > m_UserUpdateLast + m_UserUpdateThrottle)
            {
                if (m_UserList.Count > 0)
                {
                    foreach (userEntry user in m_UserList.Values)
                    {
                        if (user.needsUpdate)
                        {
                            user.doUpdate(m_ActionTimer.ElapsedMilliseconds);
                            userWasUpdated = true;
                        }
                    }
                    if (userWasUpdated)
                    {
                        m_UserUpdateLast = m_ActionTimer.ElapsedMilliseconds;
                    }
                }
            }

        }

        public void quit()
        {
            connectionCommand commandQuit = new connectionCommand(connectionCommand.types.quit);
            queueAction(commandQuit);
        }

        public string[] processCommandArguments(string argumentString)  // TODO: this will be replaced by something to grab x number of arguments
        {
            string[] argumentList = new string[0];

            if (!string.IsNullOrEmpty(argumentString))
            {
                argumentList = argumentString.Split(' ');
            }

            return argumentList;
        }

        public string getFirstTokenString(string inputString)   // Should be static
        {
            int subEnd = inputString.IndexOf(' ');
            if (subEnd > 0)
            {
                return inputString.Substring(0, subEnd);
            }

            return inputString;
        }

        public chatCommandDef findCommand(chatCommandDef currentCommand, string input, int commandLengthCheck, ref int commandLength)
        {
            string checkCommandString = getFirstTokenString(input);
            string checkSubString;
            chatCommandDef checkSub;

            if (!string.IsNullOrEmpty(checkCommandString) && currentCommand.Name == checkCommandString)
            {
                for (int i = 0; i < currentCommand.SubCommands.Count; i++)
                {
                    if (input.Length >= checkCommandString.Length + 1)
                    {
                        checkSubString = getFirstTokenString(input.Substring(checkCommandString.Length + 1));

                        if (!string.IsNullOrEmpty(checkSubString))
                        {
                            checkSub = findCommand(currentCommand.SubCommands[i], checkSubString, checkCommandString.Length + 1, ref commandLength);
                            if (checkSub != null)
                            {
                                return checkSub;
                            }
                        }
                    }

                }

                commandLength = commandLengthCheck + checkCommandString.Length + 1;
                return currentCommand;
            }

            return null;
        }

        public void getGameCommand(userEntry commandUser, string argumentString)
        {
            sendChannelMessage(m_DefaultChannel, "Current game is " + m_Game);
        }

        public void getStreamTitle(userEntry commandUser, string argumentString)
        {
            sendChannelMessage(m_DefaultChannel, "Stream title is \"" + m_Title + "\"");
        }

        public void getViewCount(userEntry commandUser, string argumentString)
        {
            if (m_IsLive)
                sendChannelMessage(m_DefaultChannel, "Current view count: " + m_ViewersLast);
            else
                sendChannelMessage(m_DefaultChannel, "Stream isn't live - check back later.");
        }

        public void getHelpString(userEntry commandUser, string argumentString)
        {
            if (!string.IsNullOrEmpty(m_CoreConfig.configData.helpText))
            {
                sendChannelMessage(m_DefaultChannel, m_CoreConfig.configData.helpText);
            }
        }

        public void quitCommand(userEntry commandUser, string argumentString)
        {
            if (actionQueue.Count > 0)
            {
                m_TwitchClient.SendMessage(m_DefaultChannel, "JerpBot is quitting, no new commands will be accepted, current queue will be processed...", false);
            }
            else
            {
                m_TwitchClient.SendMessage(m_DefaultChannel, "JerpBot is quitting...", false);
            }

            isDone = true;
            quit();
        }

        public void processUserCommand(userEntry commandUser, string message)
        {
            if (m_IsDone)
            {
                return;
            }
            int commandEnd = message.IndexOf(' ');
            string command;

            string argumentString = "";

            if (commandEnd > 0)
            {
                command = message.Substring(1, commandEnd);
                argumentString = message.Substring(commandEnd + 1);
            }
            else
            {
                command = message.Substring(1);
            }

            command = command.ToLower().TrimEnd();

            int commandLength = 0;
            chatCommandDef commandDef = null;
            for (int i = 0; i < chatCommandList.Count; i++)
            {
                commandDef = findCommand(chatCommandList[i], message.Substring(1), 0, ref commandLength);

                if (commandDef != null)
                    break;
            }

            if (commandDef == null)
                commandDef = gameCommandModule.get(command);

            if (commandDef == null)
                commandDef = customCommandModule.get(command);

            if (commandDef == null)
            {
                string[] foundAlias = m_AliasModule.loadAlias(command);
                if (foundAlias != null)
                {
                    for (int i=0; i < foundAlias.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(argumentString))
                        {
                            argumentString = " " + argumentString;
                        }
                        processUserCommand(commandUser, foundAlias[i] + argumentString); // TODO: Exploit checking on argumentString
                    }
                    return;
                }
            }

            if (commandDef != null && commandDef.Run != null && commandDef.canUse(commandUser, m_ActionTimer.ElapsedMilliseconds))
            {
                argumentString = message.Substring(Math.Min(message.Length, commandLength + 1));
                commandDef.TimeLast = m_ActionTimer.ElapsedMilliseconds;
                commandDef.Run(commandUser, argumentString);
                return;
            }

            if (soundCommandModule.soundExists(command))
                processUserCommand(commandUser, "!sound " + command);

        }

        public userEntry checkCreateUser(string username, bool canCreate = true)
        {
            userEntry userEntry;
            if (m_UserList.ContainsKey(username) && m_UserList[username] != null)
            {
                userEntry = m_UserList[username];
            }
            else if (canCreate)
            {
                userEntry = new userEntry(username, m_StorageDB);
                m_UserList[username] = userEntry;
            }
            else
            {
                return null;
            }

            return userEntry;
        }

        public void processJoinPart(string nickname, bool hasJoined)
        {
            userEntry messageUser = checkCreateUser(nickname);
            messageUser.inChannel = hasJoined;
        }

        public void processUserMessage(string nickname, string message)
        {
            userEntry messageUser = checkCreateUser(nickname);

            m_LineCount++;

            if (message[0] == '!')  // Try to assume this is a command
            {
                messageUser.incrementCommandCount();
                processUserCommand(messageUser, message);
            }
            else
            {
                botModule tempModule;
                for (int i = 0; i < m_Modules.Count; i++)
                {
                    tempModule = m_Modules[i];

                    if (moduleValidForAction(tempModule))
                        tempModule.onUserMessage(messageUser, message);
                }

                messageUser.incrementMessageCount();
                if (message.Length <= MESSAGE_VOTE_MAX_LENGTH && message.IndexOf(" ") == -1)
                    processUserCommand(messageUser, "!vote " + message);
                        
            }
        }

        private bool moduleValidForAction(botModule aModule)
        {
            if (
                (!aModule.requiresConnection || m_HasChatConnection) &&
                (!aModule.requiresChannel || m_HasJoinedChannel) &&
                (!aModule.requiresPM || true)    // TODO: Eventually actually check the PM connection!
            )
            {
                return true;
            }
            return false;
        }

        public void frame()
        {
            if (m_TwitchClient.IsConnected)
            {

                botModule tempModule;
                for (int i = 0; i < m_Modules.Count; i++)
                {
                    tempModule = m_Modules[i];

                    if (moduleValidForAction(tempModule))
                        tempModule.frame();
                }

                processActionQueue();
            }
        }

        public TwitchLib.Api.V5.Models.Channels.Channel getSingleChannelInfoByName(string aChannelName)
        {

            Task<TwitchLib.Api.Helix.Models.Users.GetUsers.GetUsersResponse> userInfoTask = Task.Run(() => m_TwitchAPI.Helix.Users.GetUsersAsync(null, new List<string>() { aChannelName }));
            userInfoTask.Wait();

            if (userInfoTask.Result != null && userInfoTask.Result.Users.Length >= 1)
            {
                string userID = userInfoTask.Result.Users[0].Id;

                // TODO: Switch to Helix
                Task<TwitchLib.Api.V5.Models.Channels.Channel> channelInfoTask = Task.Run(() => m_TwitchAPI.V5.Channels.GetChannelByIDAsync(userID));
                channelInfoTask.Wait();

                if (channelInfoTask.Result != null)
                {
                    return channelInfoTask.Result;
                }
            }

            return null;
        }

        public void getUptime(userEntry commandUser, string argumentString)
        {
            if (m_IsLive)
            {
                DateTime curTime = DateTime.Now;
                TimeSpan timeSinceLive = curTime.Subtract(m_LiveStartTime);
                sendDefaultChannelMessage(string.Format("Stream has been live for {0} hours, {1 minutes}.", timeSinceLive.Hours, timeSinceLive.Minutes));
            }
            else
            {
                sendDefaultChannelMessage("Stream isn't live - check back later.");
            }
        }

        public void shoutout(userEntry commandUser, string argumentString)
        {
            string nickname = getFirstTokenString(argumentString);
            if (!string.IsNullOrEmpty(nickname))
            {
                string lastGame = "";

                TwitchLib.Api.V5.Models.Channels.Channel channelInfo = getSingleChannelInfoByName(nickname);

                if (channelInfo != null && !string.IsNullOrEmpty(channelInfo.Game))
                    lastGame = "  They were last playing " + channelInfo.Game;

                sendDefaultChannelMessage("Check out " + channelInfo.DisplayName + " and give 'em a follow!  ( twitch.tv/" + channelInfo.DisplayName.ToLower() + " )" + lastGame);
            }
        }

        public void addChatCommand(chatCommandDef aNewCommand)
        {
            chatCommandList.Add(aNewCommand);
        }

        public void checkSub(userEntry commandUser, string argumentString)
        {
            if (!string.IsNullOrEmpty(argumentString))
            {
                userEntry checkUser = checkCreateUser(argumentString, false);

                if (checkUser != null)
                {
                    if (checkUser.isSubscriber)
                        sendDefaultChannelMessage(checkUser.Nickname + " is a sub.");
                    else
                        sendDefaultChannelMessage(checkUser.Nickname + " is NOT a sub.");
                }
            }
        }

        public void checkFollower(userEntry commandUser, string argumentString)
        {
            if (!string.IsNullOrEmpty(argumentString))
            {
                userEntry checkUser = checkCreateUser(argumentString, false);

                if (checkUser != null)
                {
                    if (checkUser.isFollower)
                        sendDefaultChannelMessage(checkUser.Nickname + " is a follower.");
                    else
                        sendDefaultChannelMessage(checkUser.Nickname + " is NOT a follower.");
                }
            }
        }

        public void checkBroadcaster(userEntry commandUser, string argumentString)
        {
            if (!string.IsNullOrEmpty(argumentString))
            {
                userEntry checkUser = checkCreateUser(argumentString, false);

                if (checkUser != null)
                {
                    if (checkUser.isBroadcaster)
                        sendDefaultChannelMessage(checkUser.Nickname + " is the broadcaster.");
                    else
                        sendDefaultChannelMessage(checkUser.Nickname + " is NOT the broadcaster.");
                }
            }
        }

        public void checkModerator(userEntry commandUser, string argumentString)
        {
            if (!string.IsNullOrEmpty(argumentString))
            {
                userEntry checkUser = checkCreateUser(argumentString, false);

                if (checkUser != null)
                {
                    if (checkUser.isModerator)
                        sendDefaultChannelMessage(checkUser.Nickname + " is a moderator.");
                    else
                        sendDefaultChannelMessage(checkUser.Nickname + " is NOT a moderator.");
                }
            }
        }

        public void randomNumber(userEntry commandUser, string argumentString)
        {
            int randMin = 1;
            int randMax = 1000;

            int newRandMin;
            int newRandMax;

            if (argumentString.Length > 0)
            {
                
                if (argumentString.IndexOf(' ') > -1)
                {
                    string[] argumentList = argumentString.Split(new[] { ' ' }, 2);
                    if (argumentList.Length == 2)
                    {
                        if (int.TryParse(argumentList[0], out newRandMin) && int.TryParse(argumentList[1], out newRandMax))
                        {
                            randMin = newRandMin;
                            randMax = newRandMax;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }

                }
                else
                {
                    if (int.TryParse(argumentString, out newRandMax))
                    {
                        randMax = newRandMax;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            sendDefaultChannelMessage("Random number is "+randomizer.Next(randMin, randMax)+" ("+randMin+"-"+randMax+")");
        }

        // ==========================================================

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            m_HasJoinedChannel = true;
            m_TwitchClient.SendMessage(e.Channel, "jerpBot in da house!!!");
        }

        private void Client_OnJoinedChannelJerp(object sender, OnJoinedChannelArgs e)
        {
            // blah
        }

        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            m_HasChatConnection = true;
            Console.WriteLine($"Connected to {e.AutoJoinChannel}");
            m_TwitchClient.JoinChannel(m_DefaultChannel);
        }

        private void Client_OnConnectedJerp(object sender, OnConnectedArgs e)
        {
            Console.WriteLine($"Jerp Connected to {e.AutoJoinChannel}");
            m_TwitchClientJerp.JoinChannel(m_DefaultChannel);
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            if (e.ChatMessage.Channel == m_DefaultChannel)
            {
                userEntry messageUser = checkCreateUser(e.ChatMessage.Username);

                messageUser.isBroadcaster = e.ChatMessage.IsBroadcaster;
                messageUser.isModerator = e.ChatMessage.IsModerator;
                messageUser.isSubscriber = e.ChatMessage.IsSubscriber;
                messageUser.isVIP = e.ChatMessage.IsVip;
                messageUser.isPartner = e.ChatMessage.IsPartner;

                processUserMessage(e.ChatMessage.Username, e.ChatMessage.Message);
            }
        }

        private void Client_OnLog(object sender, OnLogArgs e)
        {
            Console.WriteLine($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
        }

        private void Client_OnConnectionError(object sender, OnConnectionErrorArgs e)
        {
            Console.WriteLine($"{e.BotUsername} - {e.Error}");
        }

        private void Client_OnUserJoined(object sender, OnUserJoinedArgs e)
        {
            userEntry joinedUser = checkCreateUser(e.Username);
            joinedUser.inChannel = true;


            botModule tempModule;
            for (int i = 0; i < m_Modules.Count; i++)
            {
                tempModule = m_Modules[i];

                if (moduleValidForAction(tempModule))
                    tempModule.onUserJoin(joinedUser);
            }
        }

        private void Client_OnUserLeft(object sender, OnUserLeftArgs e)
        {
            userEntry leftUser = checkCreateUser(e.Username);
            leftUser.inChannel = false;
        }

        private void Client_OnBeingHosted(object sender, OnBeingHostedArgs e)
        {
            botModule tempModule;
            for (int i = 0; i < m_Modules.Count; i++)
            {
                tempModule = m_Modules[i];

                if (moduleValidForAction(tempModule))
                    tempModule.onHost(e.BeingHostedNotification.HostedByChannel, e.BeingHostedNotification.Viewers);
            }
        }

        // ==========================================================

        private void ParseStreamData(TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream aStream)
        {
            if (aStream != null)
            {
                m_IsLive = true;
                m_Game = aStream.GameName;
                m_ViewersLast = aStream.ViewerCount;
                m_LiveStartTime = aStream.StartedAt;
            }
        }

        private void Monitor_OnStreamOnline(object sender, OnStreamOnlineArgs e)
        {
            if (e.Stream != null)
            {
                ParseStreamData(e.Stream);
            }
        }

        private void Monitor_OnStreamOffline(object sender, OnStreamOfflineArgs e)
        {
            m_IsLive = false;
        }

        private void Monitor_OnStreamUpdate(object sender, OnStreamUpdateArgs e)
        {
            ParseStreamData(e.Stream);
        }

        // ==========================================================

        public jerpBot(logger useLog, botConfig aConfig)
		{
            m_UserList = new Dictionary<string, userEntry>();
            m_Modules = new List<botModule>();
			botLog		= useLog;
			actionQueue = new Queue<connectionCommand>();
            m_CoreConfig = aConfig;

            m_DefaultChannel = m_CoreConfig.configData.connections[0].channels[0];

            m_TwitchCredentials = new ConnectionCredentials(m_CoreConfig.configData.connections[0].nickname, m_CoreConfig.configData.connections[0].oauth);
            m_TwitchCredentialsJerp = new ConnectionCredentials(m_CoreConfig.configData.connections[1].nickname, m_CoreConfig.configData.connections[1].oauth);

            m_TwitchClient = new TwitchClient(protocol: TwitchLib.Client.Enums.ClientProtocol.TCP);
            m_TwitchClientJerp = new TwitchClient(protocol: TwitchLib.Client.Enums.ClientProtocol.TCP);

            m_TwitchClient.Initialize(m_TwitchCredentials);
            m_TwitchClientJerp.Initialize(m_TwitchCredentialsJerp);

            m_TwitchAPI = new TwitchAPI();
            m_TwitchAPI.Settings.AccessToken = m_CoreConfig.configData.twitch_api.oauth;
            m_TwitchAPI.Settings.ClientId = m_CoreConfig.configData.twitch_api.client_id;

            m_StreamMonitor = new LiveStreamMonitorService(m_TwitchAPI, 60);

            List<string> apiChannelList = new List<string> { m_CoreConfig.configData.twitch_api.channel_id.ToString() };
            m_StreamMonitor.SetChannelsById(apiChannelList);

            m_StreamMonitor.OnStreamOnline += Monitor_OnStreamOnline;
            m_StreamMonitor.OnStreamOffline += Monitor_OnStreamOffline;
            m_StreamMonitor.OnStreamUpdate += Monitor_OnStreamUpdate;

            m_StreamMonitor.Start();

            m_TwitchClientJerp.OnJoinedChannel += Client_OnJoinedChannelJerp;
            m_TwitchClientJerp.OnConnected += Client_OnConnectedJerp;
            m_TwitchClientJerp.OnBeingHosted += Client_OnBeingHosted;

            m_TwitchClient.OnJoinedChannel += Client_OnJoinedChannel;

            m_TwitchClient.OnLog += Client_OnLog;
            m_TwitchClient.OnConnected += Client_OnConnected;
            m_TwitchClient.OnConnectionError += Client_OnConnectionError;
            m_TwitchClient.OnMessageReceived += Client_OnMessageReceived;
            m_TwitchClient.OnUserJoined += Client_OnUserJoined;
            m_TwitchClient.OnUserLeft += Client_OnUserLeft;

            m_TwitchClient.Connect();
            m_TwitchClientJerp.Connect();

			m_ActionTimer = Stopwatch.StartNew();

			chatCommandList = new List<chatCommandDef>();
			chatCommandList.Add(new chatCommandDef("botquit", this.quitCommand, false, false));
			chatCommandList.Add(new chatCommandDef("title", getStreamTitle, true, true));
			chatCommandList.Add(new chatCommandDef("game", getGameCommand, true, true));
			chatCommandList.Add(new chatCommandDef("viewers", getViewCount, true, true));
			chatCommandList.Add(new chatCommandDef("help", getHelpString, true, true));
            chatCommandList.Add(new chatCommandDef("random", randomNumber, true, true));
            chatCommandList.Add(new chatCommandDef("follower", checkFollower, true, true));
            chatCommandList.Add(new chatCommandDef("moderator", checkModerator, true, true));
            chatCommandList.Add(new chatCommandDef("subscriber", checkSub, true, true));
            chatCommandList.Add(new chatCommandDef("broadcaster", checkBroadcaster, true, true));
            chatCommandList.Add(new chatCommandDef("shoutout", shoutout, true, false));
            chatCommandList.Add(new chatCommandDef("uptime", getUptime, true, true));

            string databasePath = System.IO.Path.Combine(storagePath, "jerpbot.sqlite");
			m_StorageDB = new SQLiteConnection("Data Source=" + databasePath + ";Version=3;");
			m_StorageDB.Open();

			string createViewerTableQuery = "CREATE TABLE IF NOT EXISTS viewers (viewerID INTEGER PRIMARY KEY ASC, name varchar(25) UNIQUE, loyalty INTEGER, points INTEGER)";
			SQLiteCommand createViewerTableCommand = new SQLiteCommand(createViewerTableQuery, m_StorageDB);
			createViewerTableCommand.ExecuteNonQuery();
		}
	}
}
