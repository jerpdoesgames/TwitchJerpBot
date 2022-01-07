﻿using System;
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
using System.Linq;

namespace JerpDoesBots
{
    class jerpBot
    {
        ConnectionCredentials m_TwitchCredentialsBot;
        ConnectionCredentials m_TwitchCredentialsOwner;
        botConfig m_CoreConfig;
        TwitchClient m_TwitchClientBot;
        TwitchClient m_TwitchClientOwner;
        TwitchAPI m_TwitchAPI;
        LiveStreamMonitorService m_StreamMonitor;

        public TwitchAPI twitchAPI { get { return m_TwitchAPI; } }

        public string OwnerUsername { get { return m_TwitchCredentialsOwner.TwitchUsername; } }
        public string OwnerID { get { return m_CoreConfig.configData.twitch_api.channel_id.ToString(); } }

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
        private int m_SubsThisSession = 0;
        private localizer m_Localizer;
        public localizer Localizer { get { return m_Localizer; } }

        public int subsThisSession { get { return m_SubsThisSession; } }

        public TimeSpan timeSinceLive {
            get {
                if (IsLive)
                {
                    DateTime curTime = DateTime.Now.ToUniversalTime();
                    return curTime.Subtract(m_LiveStartTime);
                }
                return new TimeSpan(0);
            }
        }

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

        public int viewersLast { get { return m_ViewersLast; } }

        private string m_Game = "";
        public string game { get { return m_Game; } }

        private TwitchLib.Api.Helix.Models.Common.Tag[] m_Tags;
        public TwitchLib.Api.Helix.Models.Common.Tag[] tags { get { return m_Tags; } }

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
        public bool tagInList(string aTag, TwitchLib.Api.Helix.Models.Common.Tag[] aTagList)
        {
            if (aTagList != null)
            {
                for (int i = 0; i < aTagList.Length; i++)
                {
                    TwitchLib.Api.Helix.Models.Common.Tag curTag = aTagList[i];

                    // Rather than require a language to be specified, just check every language for a match.  Probably not a performance concern since this is running locally and Twitch is giving you every language by default.
                    foreach (KeyValuePair<string, string> curLocale in aTagList[i].LocalizationNames)
                    {
                        if (aTag.ToLower() == curLocale.Value.ToLower())
                            return true;
                    }
                }
            }

            return false;
        }

        public string stripPunctuation(string aInput, bool aStripWhitespace = false)
        {
            return new string(aInput.Where(c => (!char.IsPunctuation(c) && (!aStripWhitespace || !char.IsWhiteSpace(c)))).ToArray()); // TODO: Something without linq
        }

        public void executeAndLog(connectionCommand commandToExecute)
        {
            switch (commandToExecute.getCommandType())
            {
                case connectionCommand.types.channelMessage:
                    if (isValidPrivMsg(commandToExecute))
                    {
                        m_TwitchClientBot.SendMessage(commandToExecute.getTarget(), commandToExecute.getMessage());
                    }
                    break;

                case connectionCommand.types.joinChannel:
                    if (!String.IsNullOrEmpty(commandToExecute.getTarget()))
                    {
                        m_TwitchClientBot.JoinChannel(commandToExecute.getTarget());
                    }
                    break;

                case connectionCommand.types.partAllChannels:
                    for (int i = 0; i < m_TwitchClientBot.JoinedChannels.Count; i++)
                    {
                        m_TwitchClientBot.LeaveChannel(m_TwitchClientBot.JoinedChannels[i]);
                    }
                    break;

                case connectionCommand.types.partChannel:
                    if (!String.IsNullOrEmpty(commandToExecute.getTarget()))
                    {
                        m_TwitchClientBot.LeaveChannel(commandToExecute.getTarget());
                    }
                    break;

                case connectionCommand.types.privateMessage:
                    if (isValidPrivMsg(commandToExecute))
                    {
                        m_TwitchClientBot.SendWhisper(commandToExecute.getTarget(), commandToExecute.getMessage());
                    }
                    break;

                case connectionCommand.types.quit:
                    m_TwitchClientBot.Disconnect();
                    m_TwitchClientOwner.Disconnect();
                    m_IsReadyToClose = true;
                    isDone = true;
                    break;

                default:
                    Console.WriteLine("Unknown command type sent to executeAndLog");
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
                m_TwitchClientBot.SendMessage(m_DefaultChannel, messageToSend);
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

        public static string getFirstTokenString(string inputString)
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
            sendChannelMessage(m_DefaultChannel, string.Format(m_Localizer.getString("infoCurrentGame"), m_Game));
        }

        public void updateChannelInfo(TwitchLib.Api.Helix.Models.Channels.ModifyChannelInformation.ModifyChannelInformationRequest newChannelInfo, List<string> newTags = null)
        {
            try
            {
                Task modifyChannelInfoTask = Task.Run(() => m_TwitchAPI.Helix.Channels.ModifyChannelInformationAsync(m_CoreConfig.configData.twitch_api.channel_id.ToString(), newChannelInfo));
                modifyChannelInfoTask.Wait();

                requestChannelInfo();   // TODO: I mean, this is kind of the lazy way to do it

                if (newTags != null)
                {
                    Task replaceTagsTask = Task.Run(() => m_TwitchAPI.Helix.Streams.ReplaceStreamTagsAsync(m_CoreConfig.configData.twitch_api.channel_id.ToString(), newTags));
                    replaceTagsTask.Wait();
                }

                sendDefaultChannelMessage(m_Localizer.getString("modifyChannelInfoSuccess"));
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to update channel info/tags: " + e.Message);
                sendDefaultChannelMessage(m_Localizer.getString("modifyChannelInfoFailRequestFail"));
            }
        }

        public void getStreamTitle(userEntry commandUser, string argumentString)
        {
            if ((commandUser.isBroadcaster || commandUser.isModerator) && !string.IsNullOrEmpty(argumentString))
            {
                try
                {
                    TwitchLib.Api.Helix.Models.Channels.ModifyChannelInformation.ModifyChannelInformationRequest newChannelInfoRequest = new TwitchLib.Api.Helix.Models.Channels.ModifyChannelInformation.ModifyChannelInformationRequest() { Title = argumentString };
                    Task modifyChannelInfoTask = Task.Run(() => m_TwitchAPI.Helix.Channels.ModifyChannelInformationAsync(m_CoreConfig.configData.twitch_api.channel_id.ToString(), newChannelInfoRequest));
                    modifyChannelInfoTask.Wait();

                    m_Title = argumentString;

                    sendDefaultChannelMessage(string.Format(m_Localizer.getString("modifyChannelInfoTitleSuccess"), m_Title));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to update stream title: " + e.Message);   // TODO: I'm now realizing I totally trashed logging a while back and never really updated it.
                    sendDefaultChannelMessage(m_Localizer.getString("modifyChannelInfoTitleFail"));
                }
            }
            else
            {
                sendChannelMessage(m_DefaultChannel, string.Format(m_Localizer.getString("infoStreamTitle"), m_Title));
            }
        }

        public void getViewCount(userEntry commandUser, string argumentString)
        {
            if (m_IsLive)
                sendChannelMessage(m_DefaultChannel, string.Format(m_Localizer.getString("infoViewCountLive"), m_ViewersLast));
            else
                sendChannelMessage(m_DefaultChannel, m_Localizer.getString("infoViewCountOffline"));
        }

        public void getNewSubCount(userEntry commandUser, string argumentString)
        {
            if (m_SubsThisSession > 0)
                sendChannelMessage(m_DefaultChannel, string.Format(m_Localizer.getString("infoNewSubCount"), m_SubsThisSession.ToString()));
            else
                sendChannelMessage(m_DefaultChannel, m_Localizer.getString("infoNewSubCountNone"));
        }

        public void getHelpString(userEntry commandUser, string argumentString)
        {
            sendChannelMessage(m_DefaultChannel, m_Localizer.getString("helpText"));
        }

        public void quitCommand(userEntry commandUser, string argumentString)
        {
            if (actionQueue.Count > 0)
            {
                m_TwitchClientBot.SendMessage(m_DefaultChannel, m_Localizer.getString("announceQuitMessagesQueued"), false);
            }
            else
            {
                m_TwitchClientBot.SendMessage(m_DefaultChannel, m_Localizer.getString("announceQuit"), false);
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
            if (m_TwitchClientBot.IsConnected)
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

        public TwitchLib.Api.Helix.Models.Channels.GetChannelInformation.ChannelInformation getSingleChannelInfoByName(string aChannelName)
        {

            Task<TwitchLib.Api.Helix.Models.Users.GetUsers.GetUsersResponse> userInfoTask = Task.Run(() => m_TwitchAPI.Helix.Users.GetUsersAsync(null, new List<string>() { aChannelName }));
            userInfoTask.Wait();

            if (userInfoTask.Result != null && userInfoTask.Result.Users.Length >= 1)
            {
                string userID = userInfoTask.Result.Users[0].Id;

                Task<TwitchLib.Api.Helix.Models.Channels.GetChannelInformation.GetChannelInformationResponse> channelInfoTask = Task.Run(() => m_TwitchAPI.Helix.Channels.GetChannelInformationAsync(userID));

                channelInfoTask.Wait();

                if (channelInfoTask.Result != null)
                {
                    return channelInfoTask.Result.Data[0];
                }
            }

            return null;
        }

        public void getUptime(userEntry commandUser, string argumentString)
        {
            if (m_IsLive)
            {
                TimeSpan tempTimeSinceLive = timeSinceLive;
                sendDefaultChannelMessage(string.Format(m_Localizer.getString("infoLiveTime"), tempTimeSinceLive.Hours, tempTimeSinceLive.Minutes));
            }
            else
            {
                sendDefaultChannelMessage(m_Localizer.getString("infoLiveTimeOffline"));
            }
        }

        public void shoutout(userEntry commandUser, string argumentString)
        {
            string nickname = getFirstTokenString(argumentString);
            if (!string.IsNullOrEmpty(nickname))
            {
                string lastGame = "";

                TwitchLib.Api.Helix.Models.Channels.GetChannelInformation.ChannelInformation channelInfo = getSingleChannelInfoByName(nickname);

                if (channelInfo != null && !string.IsNullOrEmpty(channelInfo.GameName))
                    lastGame = "  " + string.Format(m_Localizer.getString("shoutoutLastPlaying"), channelInfo.GameName);

                sendDefaultChannelMessage(string.Format(m_Localizer.getString("shoutoutMessage"), channelInfo.BroadcasterName, channelInfo.BroadcasterName.ToLower()) + lastGame);
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
                        sendDefaultChannelMessage(string.Format(m_Localizer.getString("infoUserSubCheckPass"), checkUser.Nickname));
                    else
                        sendDefaultChannelMessage(string.Format(m_Localizer.getString("infoUserSubCheckFail"), checkUser.Nickname));
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
                        sendDefaultChannelMessage(string.Format(m_Localizer.getString("infoUserFollowCheckPass"), checkUser.Nickname));
                    else
                        sendDefaultChannelMessage(string.Format(m_Localizer.getString("infoUserFollowCheckFail"), checkUser.Nickname));
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
                        sendDefaultChannelMessage(string.Format(m_Localizer.getString("infoUserBroadcasterCheckPass"), checkUser.Nickname));
                    else
                        sendDefaultChannelMessage(string.Format(m_Localizer.getString("infoUserBroadcasterCheckFail"), checkUser.Nickname));
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
                        sendDefaultChannelMessage(string.Format(m_Localizer.getString("infoUserModCheckPass"), checkUser.Nickname));
                    else
                        sendDefaultChannelMessage(string.Format(m_Localizer.getString("infoUserModCheckFail"), checkUser.Nickname));
                }
            }
        }

        public int getNumChattersFollowing(out int numChattersTotal)
        {
            numChattersTotal = 0;
            int totalFollowers = 0;
            foreach (string curKey in userList.Keys)
            {
                if (userList[curKey].inChannel)
                {
                    numChattersTotal++;
                    if (userList[curKey].isFollower)
                        totalFollowers++;
                }
            }
            return totalFollowers;
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

            sendDefaultChannelMessage(string.Format(m_Localizer.getString("randomizerGeneral"), randomizer.Next(randMin, randMax), randMin, randMax));
        }

        // ==========================================================

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            m_HasJoinedChannel = true;
            m_TwitchClientBot.SendMessage(e.Channel, m_Localizer.getString("announceChannelJoin"));
        }

        private void Client_OnJoinedChannelJerp(object sender, OnJoinedChannelArgs e)
        {
            // blah
        }

        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            m_HasChatConnection = true;
            Console.WriteLine($"Connected to {e.AutoJoinChannel}");
            requestChannelInfo();
            m_TwitchClientBot.JoinChannel(m_DefaultChannel);
            
        }

        private void Client_OnConnectedOwner(object sender, OnConnectedArgs e)
        {
            Console.WriteLine($"jerpBot owner account connected to {e.AutoJoinChannel}");
            m_TwitchClientOwner.JoinChannel(m_DefaultChannel);
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

        private void Client_OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
            m_SubsThisSession++;
        }

        private void Client_OnCommunitySubscription(object sender, OnCommunitySubscriptionArgs e)
        {
            m_SubsThisSession++;
        }

        private void Client_OnReSubscribe(object sender, OnReSubscriberArgs e)
        {
            m_SubsThisSession++;
        }

        private void Client_OnGiftedSubscription(object sender, OnGiftedSubscriptionArgs e)
        {
            m_SubsThisSession++;
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

        private void requestChannelInfo()
        {
            TwitchLib.Api.Helix.Models.Channels.GetChannelInformation.ChannelInformation channelInfo = getSingleChannelInfoByName(m_TwitchCredentialsOwner.TwitchUsername);
            m_Game = channelInfo.GameName;
            m_Title = channelInfo.Title;

            Task<TwitchLib.Api.Helix.Models.Streams.GetStreamTags.GetStreamTagsResponse> getStreamTagsTask = Task.Run(() => m_TwitchAPI.Helix.Streams.GetStreamTagsAsync(channelInfo.BroadcasterId));
            getStreamTagsTask.Wait();

            m_Tags = getStreamTagsTask.Result.Data;
        }

        private void ParseStreamData(TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream aStream)
        {
            requestChannelInfo();
            if (aStream != null)
            {
                m_IsLive = true;
                // m_Game = aStream.GameName;
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
            m_Localizer = new localizer(this);

            m_DefaultChannel = m_CoreConfig.configData.connections[0].channels[0];

            m_TwitchCredentialsBot = new ConnectionCredentials(m_CoreConfig.configData.connections[0].nickname, m_CoreConfig.configData.connections[0].oauth);
            m_TwitchCredentialsOwner = new ConnectionCredentials(m_CoreConfig.configData.connections[1].nickname, m_CoreConfig.configData.connections[1].oauth);

            m_TwitchClientBot = new TwitchClient(protocol: TwitchLib.Client.Enums.ClientProtocol.TCP);
            m_TwitchClientOwner = new TwitchClient(protocol: TwitchLib.Client.Enums.ClientProtocol.TCP);

            m_TwitchClientBot.Initialize(m_TwitchCredentialsBot);
            m_TwitchClientOwner.Initialize(m_TwitchCredentialsOwner);

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

            m_TwitchClientOwner.OnJoinedChannel += Client_OnJoinedChannelJerp;
            m_TwitchClientOwner.OnConnected += Client_OnConnectedOwner;
            m_TwitchClientOwner.OnBeingHosted += Client_OnBeingHosted;

            m_TwitchClientBot.OnJoinedChannel += Client_OnJoinedChannel;

            m_TwitchClientBot.OnLog += Client_OnLog;
            m_TwitchClientBot.OnConnected += Client_OnConnected;
            m_TwitchClientBot.OnConnectionError += Client_OnConnectionError;
            m_TwitchClientBot.OnMessageReceived += Client_OnMessageReceived;
            m_TwitchClientBot.OnUserJoined += Client_OnUserJoined;
            m_TwitchClientBot.OnUserLeft += Client_OnUserLeft;
            m_TwitchClientBot.OnNewSubscriber += Client_OnNewSubscriber;
            m_TwitchClientBot.OnCommunitySubscription += Client_OnCommunitySubscription;
            m_TwitchClientBot.OnReSubscriber += Client_OnReSubscribe;
            m_TwitchClientBot.OnGiftedSubscription += Client_OnGiftedSubscription;

            m_TwitchClientBot.Connect();
            m_TwitchClientOwner.Connect();

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
            chatCommandList.Add(new chatCommandDef("subcount", getNewSubCount, true, true));

            string databasePath = System.IO.Path.Combine(storagePath, "jerpbot.sqlite");
			m_StorageDB = new SQLiteConnection("Data Source=" + databasePath + ";Version=3;");
			m_StorageDB.Open();

			string createViewerTableQuery = "CREATE TABLE IF NOT EXISTS viewers (viewerID INTEGER PRIMARY KEY ASC, name varchar(25) UNIQUE, loyalty INTEGER, points INTEGER)";
			SQLiteCommand createViewerTableCommand = new SQLiteCommand(createViewerTableQuery, m_StorageDB);
			createViewerTableCommand.ExecuteNonQuery();
		}
	}
}
