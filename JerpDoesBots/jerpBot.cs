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
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using NAudio.Wave.Asio;

namespace JerpDoesBots
{
    class jerpBot
    {
        private static jerpBot _instance;   // TODO: Less half-assed singleton, update more of the bot to use this

        public static jerpBot instance
        {
            get { return _instance; }
            set
            {
                if (_instance != null)
                {
                    throw new InvalidOperationException("JerpBot instance already set!");
                }
                else
                {
                    _instance = value;
                }
            }
        }

        ConnectionCredentials m_TwitchCredentialsBot;
        ConnectionCredentials m_TwitchCredentialsOwner;
        botConfig m_CoreConfig;
        TwitchClient m_TwitchClientBot;
        TwitchPubSub m_TwitchPubSubBot;
        TwitchClient m_TwitchClientOwner;
        TwitchAPI m_TwitchAPI;
        LiveStreamMonitorService m_StreamMonitor;

        private logger m_LogGeneral;            // Internal housekeeping to keep track of.
        private logger m_LogEvents;             // Subs, raids, follows, etc.
        private logger m_LogChat;               // Chat from Twitch users.
        private logger m_LogWarningsErrors;     // Warnings and errors, of course.
        private logger m_LogConnection;         // General connection output

        public logger logGeneral { get { return m_LogGeneral; } }
        public logger logEvents { get { return m_LogEvents; } }
        public logger logChat { get { return m_LogChat; } }
        public logger logWarningsErrors { get { return m_LogWarningsErrors; } }
        public logger logConnection { get { return m_LogConnection; } }

        public TwitchAPI twitchAPI { get { return m_TwitchAPI; } }

        public string ownerUsername { get { return m_TwitchCredentialsOwner.TwitchUsername; } }
        public string ownerUserID { get { return m_CoreConfig.configData.twitch_api.channel_id.ToString(); } }
        public string botUserID { get { return m_CoreConfig.configData.connections[0].channel_id.ToString(); } }

        private DateTime m_LiveStartTime;
        private SQLiteConnection m_StorageDB;
        public SQLiteConnection storageDB { get { return m_StorageDB; } }
        private Stopwatch m_ActionTimer;
        private readonly Queue<connectionCommand> actionQueue;
        private bool m_IsDone = false;
        private static uint MESSAGE_VOTE_MAX_LENGTH = 20;
        private bool m_HasJoinedChannel = false;
        private bool m_HasChatConnection = false;
        private bool m_IsFullyLoaded = false;
        private bool m_HasExecutedLoadEvent = false;
        private bool m_HasReceivedChannelInfo = false;

        private string m_DefaultChannel;
        private bool m_IsReadyToClose = false; // Ready to completely end the program
        private int m_SubsThisSession = 0;
        private long m_FollowerStaleCheckSeconds = 360;  // Amount of time that must pass before checking to see if someone's following
        private localizer m_Localizer;
        public localizer localizer { get { return m_Localizer; } }

        public int subsThisSession { get { return m_SubsThisSession; } }


        public void setLoadComplete()
        {
            m_IsFullyLoaded = true;
        }

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

        private long m_UserUpdateLast = 0;
        private long m_UserUpdateThrottle = 5000;

        private long m_SendTimeLast = 0;
        private static long m_SendThrottleMin = 1000;

        private bool m_IsLive = false;
        public bool IsLive { get { return m_IsLive; } set { m_IsLive = value; } }
        private string m_Title = "";
        public string Title { get { return m_Title; } }

        private string m_CategoryID = "";
        public string CategoryID { get { return m_CategoryID; } }
        private void setCategoryID(string aCategoryID)
        {
            if (aCategoryID != m_CategoryID)
            {
                m_CategoryID = aCategoryID;

                if (m_HasExecutedLoadEvent && m_HasReceivedChannelInfo) // Probably only need m_HasExecutedLoadEvent but will do both for safety
                {
                    foreach (botModule curModule in m_Modules)
                    {
                        if (isModuleValidForUserAction(curModule))
                            curModule.onCategoryIDChanged();
                    }
                }
            }
        }

        private int m_ViewersLast = 0;

        public int viewersLast { get { return m_ViewersLast; } }

        private string m_Game = "";
        public string game { get { return m_Game; } }

        private string[] m_Tags;
        public string[] tags { get { return m_Tags; } }

        private long m_LineCount = 0;   // Total lines
        public long lineCount { get { return m_LineCount; } }

        private List<chatCommandDef> m_CommandList;

        public void setLive(bool newLive)
        {
            m_IsLive = newLive;
        }

        public void addModule(botModule aModule)
        {
            m_Modules.Add(aModule);
        }

        private customCommand m_CustomCommandModule;
        public customCommand customCommandModule { set { m_CustomCommandModule = value; } }

        private gameCommand m_GameCommandModule;
        public gameCommand gameCommandModule { set { m_GameCommandModule = value; } }

        private soundCommands m_SoundCommandModule;
        public soundCommands soundCommandModule { set { m_SoundCommandModule = value; } }

        private commandAlias m_AliasModule;

        public commandAlias aliasModule { set { m_AliasModule = value; } }

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

        // TODO: Move to some generic location and provide a better name.
        /// <summary>
        /// Generic "string in string, but convert to lowercase" comparison.
        /// </summary>
        /// <param name="aTag">Tag to search for.</param>
        /// <param name="aTagList">String array to search through.</param>
        /// <returns>Whether the tag is in the array.</returns>
        public bool tagInList(string aTag, String[] aTagList)
        {
            if (aTagList != null)
            {
                for (int i = 0; i < aTagList.Length; i++)
                {
                    string curTag = aTagList[i];

                    if (aTag.ToLower() == curTag.ToLower())
                        return true;
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
                        m_LogGeneral.writeAndLog($"Channel Message | {commandToExecute.getTarget()} | {commandToExecute.getMessage()}");
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
                        m_LogGeneral.writeAndLog($"Private Message | {commandToExecute.getTarget()} | {commandToExecute.getMessage()}");
                        m_TwitchClientBot.SendWhisper(commandToExecute.getTarget(), commandToExecute.getMessage());
                    }
                    break;

                case connectionCommand.types.quit:
                    m_TwitchClientBot.Disconnect();
                    m_TwitchClientOwner.Disconnect();
                    m_IsReadyToClose = true;
                    isDone = true;
                    break;

                case connectionCommand.types.channelAnnouncement:
                    m_LogGeneral.writeAndLog($"Channel Announcement | {commandToExecute.getTarget()} | {commandToExecute.getMessage()}");
                    Task announceTask = Task.Run(() => m_TwitchAPI.Helix.Chat.SendChatAnnouncementAsync(commandToExecute.getTarget(), botUserID, commandToExecute.getMessage(), TwitchLib.Api.Helix.Models.Chat.AnnouncementColors.Blue, m_TwitchCredentialsBot.TwitchOAuth.Substring(6)));
                    announceTask.Wait();
                    break;

                default:
                    m_LogWarningsErrors.writeAndLog("Unknown command type sent to executeAndLog");
                    break;
            }
        }

        private void processActionQueue()
        {
            if (m_IsFullyLoaded && m_HasJoinedChannel && actionQueue.Count > 0)
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
            if (aInput[0] == '@' && aInput[1] == '!')
            {
                aInput = aInput.Substring(1);   // Strip silent mode character
            }

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

        public void sendDefaultChannelAnnounce(string messageToSend, bool doQueue = true)
        {
            sendChannelAnnouncement(ownerUserID, messageToSend, doQueue);
        }

        public void sendChannelAnnouncement(string targetChannel, string messageToSend, bool doQueue = true)
        {
            connectionCommand newCommand = new connectionCommand(connectionCommand.types.channelAnnouncement);
            newCommand.setTarget(targetChannel);
            newCommand.setMessage(messageToSend);

            if (doQueue)
            {
                queueAction(newCommand);
            }
            else
            {
                Task announceTask = Task.Run(() => m_TwitchAPI.Helix.Chat.SendChatAnnouncementAsync(targetChannel, targetChannel, messageToSend));
                announceTask.Wait();
            }
        }

        public void sendChannelMessage(string targetChannel, string messageToSend, bool doQueue = true)
        {
            connectionCommand newCommand = new connectionCommand(connectionCommand.types.channelMessage);
            newCommand.setTarget(targetChannel);
            newCommand.setMessage(messageToSend);

            if (!m_IsFullyLoaded || !m_HasJoinedChannel || doQueue)
                queueAction(newCommand);
            else
                executeAndLog(newCommand);
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

            if (!string.IsNullOrEmpty(checkCommandString) && currentCommand.name == checkCommandString)
            {
                for (int i = 0; i < currentCommand.subCommands.Count; i++)
                {
                    if (input.Length >= checkCommandString.Length + 1)
                    {
                        checkSubString = getFirstTokenString(input.Substring(checkCommandString.Length + 1));

                        if (!string.IsNullOrEmpty(checkSubString))
                        {
                            checkSub = findCommand(currentCommand.subCommands[i], checkSubString, checkCommandString.Length + 1, ref commandLength);
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

        public void getGameCommand(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            sendChannelMessage(m_DefaultChannel, string.Format(m_Localizer.getString("infoCurrentGame"), m_Game));
        }

        public void updateChannelInfo(TwitchLib.Api.Helix.Models.Channels.ModifyChannelInformation.ModifyChannelInformationRequest newChannelInfo, List<string> newTags = null, bool aSilentMode = false)
        {
            try
            {
                Task modifyChannelInfoTask = Task.Run(() => m_TwitchAPI.Helix.Channels.ModifyChannelInformationAsync(m_CoreConfig.configData.twitch_api.channel_id.ToString(), newChannelInfo));
                modifyChannelInfoTask.Wait();

                if (!string.IsNullOrEmpty(newChannelInfo.GameId))
                {
                    requestChannelInfo();   // TODO: Swap to a request for just the game name from ID
                }
                else
                {
                    if (!string.IsNullOrEmpty(newChannelInfo.Title))
                        m_Title = newChannelInfo.Title;

                    if (!string.IsNullOrEmpty(newChannelInfo.GameId))
                        setCategoryID(newChannelInfo.GameId);

                    if (newChannelInfo.Tags != null)
                        m_Tags = newChannelInfo.Tags;
                }

                if (!aSilentMode)
                    sendDefaultChannelMessage(m_Localizer.getString("modifyChannelInfoSuccess"));
            }
            catch (Exception e)
            {
                m_LogWarningsErrors.writeAndLog("Failed to update channel info/tags: " + e.Message);
                sendDefaultChannelMessage(m_Localizer.getString("modifyChannelInfoFailRequestFail"));
            }
        }

        public void getStreamTitle(userEntry commandUser, string argumentString, bool aSilent = false)
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
                    m_LogWarningsErrors.writeAndLog("Failed to update stream title: " + e.Message);
                    sendDefaultChannelMessage(m_Localizer.getString("modifyChannelInfoTitleFail"));
                }
            }
            else
            {
                sendChannelMessage(m_DefaultChannel, string.Format(m_Localizer.getString("infoStreamTitle"), m_Title));
            }
        }

        public void getViewCount(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (m_IsLive)
                sendChannelMessage(m_DefaultChannel, string.Format(m_Localizer.getString("infoViewCountLive"), m_ViewersLast));
            else
                sendChannelMessage(m_DefaultChannel, m_Localizer.getString("infoViewCountOffline"));
        }

        public void getNewSubCount(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (m_SubsThisSession > 0)
                sendChannelMessage(m_DefaultChannel, string.Format(m_Localizer.getString("infoNewSubCount"), m_SubsThisSession.ToString()));
            else
                sendChannelMessage(m_DefaultChannel, m_Localizer.getString("infoNewSubCountNone"));
        }

        public void setUserBrb(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (!aSilent)
                sendChannelMessage(m_DefaultChannel, string.Format(m_Localizer.getString("brbSetAway"), commandUser.Nickname));

            commandUser.isBrb = true;
        }

        public void setUserBack(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (!aSilent)
                sendChannelMessage(m_DefaultChannel, string.Format(m_Localizer.getString("brbSetBack"), commandUser.Nickname));

            commandUser.isBrb = false;
        }

        public void announceChatterFollowingCount(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            int chattersTotal;
            int chattersFollowing = getNumChattersFollowing(out chattersTotal);
            sendChannelMessage(m_DefaultChannel, string.Format(m_Localizer.getString("infoChattersFollowing"), chattersFollowing.ToString(), chattersTotal.ToString()));
        }

        public void genericSerializeToFile(object aInput, string aFilename)
        {
            JsonSerializerOptions jsonOptions = new JsonSerializerOptions();
            jsonOptions.WriteIndented = true;
            string outputString = JsonSerializer.Serialize(aInput, jsonOptions);
            string outputDirectory = System.IO.Path.Combine(jerpBot.storagePath, "output");
            string outputPath = System.IO.Path.Combine(outputDirectory, aFilename);
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllText(outputPath, outputString);
        }

        public void outputCommandList(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            genericSerializeToFile(m_CommandList, "jerpdoesbots_commands.json");

            if (!aSilent)
                sendDefaultChannelMessage("Successfully wrote command json to output directory.");
        }

        // This is crude and could be replaced with something better
        public string simpleDurationString(TimeSpan aDuration)
        {
            List<string> outputList = new List<string>();

            double daysPassed = aDuration.Days;
            double yearsPassed = Math.Floor(daysPassed / 365.25);
            daysPassed -= yearsPassed * 365.25;
            double monthsPassed = Math.Floor(daysPassed / 30.436875);
            daysPassed = Math.Floor(daysPassed - (monthsPassed * 30.436875));

            if (yearsPassed > 0)
                outputList.Add(string.Format(localizer.getString("durationStringYears"), yearsPassed));

            if (monthsPassed > 0)
                outputList.Add(string.Format(localizer.getString("durationStringMonths"), monthsPassed));

            if (daysPassed > 0)
                outputList.Add(string.Format(localizer.getString("durationStringDays"), daysPassed));

            if (aDuration.Hours > 0)
                outputList.Add(string.Format(localizer.getString("durationStringHours"), aDuration.Hours));

            if (aDuration.Minutes > 0)
                outputList.Add(string.Format(localizer.getString("durationStringMinutes"), aDuration.Minutes));

            return string.Join(", ", outputList);
        }

        public void marker(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (m_IsLive)
            {
                TwitchLib.Api.Helix.Models.Streams.CreateStreamMarker.CreateStreamMarkerRequest newMarkerRequest = new TwitchLib.Api.Helix.Models.Streams.CreateStreamMarker.CreateStreamMarkerRequest();
                newMarkerRequest.UserId = ownerUserID;
                if (!string.IsNullOrEmpty(argumentString))
                    newMarkerRequest.Description = argumentString;

                try
                {
                    Task<TwitchLib.Api.Helix.Models.Streams.CreateStreamMarker.CreateStreamMarkerResponse> createMarkerTask = m_TwitchAPI.Helix.Streams.CreateStreamMarkerAsync(newMarkerRequest);
                    createMarkerTask.Wait();

                    if (createMarkerTask.Result != null)
                    {
                        TimeSpan markerPos = TimeSpan.FromSeconds(createMarkerTask.Result.Data[0].PositionSeconds);

                        if (!aSilent)
                            sendDefaultChannelMessage(string.Format(localizer.getString("markerCreateSuccess"), simpleDurationString(markerPos)));
                    }
                    else
                    {
                        sendDefaultChannelMessage(localizer.getString("markerCreateFail"));
                    }
                }
                catch (Exception e)
                {
                    m_LogWarningsErrors.writeAndLog("Failed to create stream marker - " + e.Message);
                    sendDefaultChannelMessage(localizer.getString("markerCreateFail"));
                }
            }
            else
            {
                sendDefaultChannelMessage(localizer.getString("markerCreateFailNotLive"));
            }
        }

        public void followage(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (commandUser.isBroadcaster)
            {
                sendDefaultChannelMessage(string.Format(localizer.getString("followageIsBroadcaster"), commandUser.Nickname));
            }
            else
            {
                TwitchLib.Api.Helix.Models.Users.GetUserFollows.GetUsersFollowsResponse getFollowsResponse = getUserFollowsResult(commandUser);
                if (getFollowsResponse != null && getFollowsResponse.Follows.Length > 0)
                {
                    commandUser.isFollower = (getFollowsResponse.TotalFollows > 0);
                    commandUser.lastFollowCheckTime = DateTime.Now;

                    if (commandUser.isFollower)
                    {
                        string followDurationString = simpleDurationString(DateTime.Now.Subtract(getFollowsResponse.Follows[0].FollowedAt));
                        sendDefaultChannelMessage(string.Format(localizer.getString("followageDisplayTime"), commandUser.Nickname, followDurationString));
                    }
                    else
                    {
                        sendDefaultChannelMessage(string.Format(localizer.getString("followageNotFollowing"), commandUser.Nickname));
                    }
                }
                else
                {
                    sendDefaultChannelMessage(string.Format(localizer.getString("followageNotFollowing"), commandUser.Nickname));
                }
            }
        }

        public void getHelpString(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            sendChannelMessage(m_DefaultChannel, m_Localizer.getString("helpText"));
        }

        public void quitCommand(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (!aSilent)
            {
                if (actionQueue.Count > 0)
                {
                    m_TwitchClientBot.SendMessage(m_DefaultChannel, m_Localizer.getString("announceQuitMessagesQueued"), false);
                }
                else
                {
                    m_TwitchClientBot.SendMessage(m_DefaultChannel, m_Localizer.getString("announceQuit"), false);
                }
            }

            isDone = true;
            quit();
        }

        public bool processUserCommand(userEntry aCommandUser, string aMessage)
        {
            bool silentMode = aMessage[0] == '@';
            if (silentMode)
            {
                aMessage = aMessage.Substring(1);
            }

            if (!m_IsFullyLoaded || m_IsDone)
            {
                return false;
            }
            int commandEnd = aMessage.IndexOf(' ');
            string command;

            string argumentString = "";

            if (commandEnd > 0)
            {
                command = aMessage.Substring(1, commandEnd);
                argumentString = aMessage.Substring(commandEnd + 1);
            }
            else
            {
                command = aMessage.Substring(1);
            }

            command = command.ToLower().TrimEnd();

            int commandLength = 0;
            chatCommandDef commandDef = null;
            for (int i = 0; i < m_CommandList.Count; i++)
            {
                commandDef = findCommand(m_CommandList[i], aMessage.Substring(1), 0, ref commandLength);

                if (commandDef != null)
                    break;
            }

            if (commandDef == null)
                commandDef = m_GameCommandModule.get(command);

            if (commandDef == null)
                commandDef = m_CustomCommandModule.get(command);

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
                        return processUserCommand(aCommandUser, foundAlias[i] + argumentString); // TODO: Exploit checking on argumentString
                    }
                    return true;    // TODO: Return actual command output
                }
            }

            if (commandDef != null && commandDef.Run != null && commandDef.canUse(aCommandUser, m_ActionTimer.ElapsedMilliseconds))
            {
                argumentString = aMessage.Substring(Math.Min(aMessage.Length, commandLength + 1));
                commandDef.timeLast = m_ActionTimer.ElapsedMilliseconds;
                commandDef.Run(aCommandUser, argumentString, silentMode);
                return true;    // TODO: Return actual command output
            }

            if (m_SoundCommandModule.soundExists(command))
            {
                string silentPrefix = silentMode ? "@" : "";
                return processUserCommand(aCommandUser, silentPrefix + "!sound " + command);
            }
                

            return true;    // TODO: Return actual command output

        }

        public userEntry checkCreateUser(string aUsername, bool aCanCreate = true)
        {
            userEntry userEntry;
            string keyName = aUsername.ToLower();
            if (m_UserList.ContainsKey(keyName) && m_UserList[keyName] != null)
            {
                userEntry = m_UserList[keyName];
            }
            else if (aCanCreate)
            {
                userEntry = new userEntry(aUsername, m_StorageDB);
                lock(m_UserList)
                {
                    m_UserList[keyName] = userEntry;
                }
            }
            else
            {
                return null;
            }

            return userEntry;
        }

        private TwitchLib.Api.Helix.Models.Users.GetUserFollows.GetUsersFollowsResponse getUserFollowsResult(userEntry aUser)
        {
            Task<TwitchLib.Api.Helix.Models.Users.GetUserFollows.GetUsersFollowsResponse> userFollowsTask = m_TwitchAPI.Helix.Users.GetUsersFollowsAsync(null, null, 1, aUser.twitchUserID, ownerUserID);
            userFollowsTask.Wait();
            return userFollowsTask.Result;
        }

        public bool checkUpdateIsFollower(userEntry aUser)
        {
            if (!aUser.isBroadcaster)
            {
                TimeSpan timeSinceFollowCheck = DateTime.Now.Subtract(aUser.lastFollowCheckTime);

                if (timeSinceFollowCheck.TotalSeconds > m_FollowerStaleCheckSeconds && !string.IsNullOrEmpty(aUser.twitchUserID))
                {
                    try
                    {
                        TwitchLib.Api.Helix.Models.Users.GetUserFollows.GetUsersFollowsResponse userFollowsResponse = getUserFollowsResult(aUser);

                        if (userFollowsResponse != null)
                        {
                            aUser.isFollower = (userFollowsResponse.TotalFollows > 0);
                            aUser.lastFollowCheckTime = DateTime.Now;
                        }
                    }
                    catch (Exception e)
                    {
                        m_LogWarningsErrors.writeAndLog("Failed to check following status for: " + aUser.Nickname + "| Error: " + e.Message);
                    }
                }
            }
            else
            {
                aUser.isFollower = true;
            }

            return aUser.isFollower;
        }

        public void processJoinPart(string aNickname, bool aHasJoined)
        {
            userEntry messageUser = checkCreateUser(aNickname);
            messageUser.inChannel = aHasJoined;
        }

        public bool isValidCommandFormat(string aMessage)
        {
            return ((aMessage[0] == '@' && aMessage[1] == '!') || aMessage[0] == '!');
        }

        public bool processUserMessage(string aNickname, string aMessage)
        {
            userEntry messageUser = checkCreateUser(aNickname);

            m_LineCount++;

            m_LogChat.writeAndLog("Chat | " + aNickname + " | " + aMessage);

            if (isValidCommandFormat(aMessage))
            {
                messageUser.incrementCommandCount();
                return processUserCommand(messageUser, aMessage);
            }
            else
            {
                botModule tempModule;
                for (int i = 0; i < m_Modules.Count; i++)
                {
                    tempModule = m_Modules[i];

                    if (isModuleValidForUserAction(tempModule))
                        tempModule.onUserMessage(messageUser, aMessage);
                }

                messageUser.incrementMessageCount();
                if (aMessage.Length <= MESSAGE_VOTE_MAX_LENGTH && aMessage.IndexOf(" ") == -1)
                    processUserCommand(messageUser, "!vote " + aMessage);
                return true;
            }
        }

        public void processChannelPointRedemption(string aNickname, string aRewardTitle, int aRewardCost, string aRewardUserInput, string aRewardID, string aRedemptionID)
        {
            userEntry messageUser = checkCreateUser(aNickname);

            botModule tempModule;
            for (int i = 0; i < m_Modules.Count; i++)
            {
                tempModule = m_Modules[i];

                if (isModuleValidForUserAction(tempModule))
                    tempModule.onChannelPointRedemption(messageUser, aRewardTitle, aRewardCost, aRewardUserInput, aRewardID, aRedemptionID);
            }
        }

        private bool isModuleValidForUserAction(botModule aModule)
        {
            if (
                m_IsFullyLoaded &&
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
                if (m_IsFullyLoaded && m_HasReceivedChannelInfo && !m_HasExecutedLoadEvent)
                {
                    foreach(botModule curModule in m_Modules)
                    {
                        if (isModuleValidForUserAction(curModule))
                            curModule.onBotFullyLoaded();
                    }
                    m_HasExecutedLoadEvent = true;
                }

                foreach (botModule curModule in m_Modules)
                {
                    if (isModuleValidForUserAction(curModule))
                        curModule.frame();
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

        public void getUptime(userEntry commandUser, string argumentString, bool aSilent = false)
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

        public void addChatCommand(chatCommandDef aNewCommand)
        {
            m_CommandList.Add(aNewCommand);
        }

        public void checkSub(userEntry commandUser, string argumentString, bool aSilent = false)
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

        public void checkFollower(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (!string.IsNullOrEmpty(argumentString))
            {
                userEntry checkUser = checkCreateUser(argumentString, false);

                if (checkUser != null)
                {
                    if (checkUpdateIsFollower(checkUser))
                        sendDefaultChannelMessage(string.Format(m_Localizer.getString("infoUserFollowCheckPass"), checkUser.Nickname));
                    else
                        sendDefaultChannelMessage(string.Format(m_Localizer.getString("infoUserFollowCheckFail"), checkUser.Nickname));
                }
            }
        }

        public void checkBroadcaster(userEntry commandUser, string argumentString, bool aSilent = false)
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

        public void checkModerator(userEntry commandUser, string argumentString, bool aSilent = false)
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

        public int getNumChattersSubscribed(out int numChattersTotal)
        {
            numChattersTotal = 0;
            int totalSubs = 0;

            lock (userList)
            {
                lock (userList.Keys)
                {
                    foreach (string curKey in userList.Keys)
                    {
                        if (
                            userList[curKey].Nickname.ToLower() != m_CoreConfig.configData.connections[0].nickname.ToLower() && // Skip bot
                            userList[curKey].Nickname.ToLower() != m_CoreConfig.configData.connections[1].nickname.ToLower() && // Skip owner
                            userList[curKey].inChannel)
                        {
                            numChattersTotal++;
                            if (userList[curKey].isSubscriber)
                                totalSubs++;
                        }
                    }
                    return totalSubs;
                }
            }
        }

        public int getNumChattersFollowing(out int numChattersTotal)
        {
            numChattersTotal = 0;
            int totalFollowers = 0;

            lock (userList)
            {
                lock (userList.Keys)
                {
                    foreach (string curKey in userList.Keys)
                    {
                        if (
                            userList[curKey].Nickname.ToLower() != m_CoreConfig.configData.connections[0].nickname.ToLower() && // Skip bot
                            userList[curKey].Nickname.ToLower() != m_CoreConfig.configData.connections[1].nickname.ToLower() && // Skip owner
                            userList[curKey].inChannel)
                        {
                            numChattersTotal++;
                            if (checkUpdateIsFollower(userList[curKey]))
                                totalFollowers++;
                        }
                    }
                    return totalFollowers;
                }
            }


        }

        public void announce(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            sendDefaultChannelAnnounce(argumentString);
        }

        public bool messageOrCommand(string aInput)
        {
            if (!String.IsNullOrEmpty(aInput))
            {
                if (isValidCommandFormat(aInput))
                {
                    userEntry ownerUser = checkCreateUser(ownerUsername);
                    return processUserCommand(ownerUser, aInput);
                }
                else
                {
                    sendDefaultChannelMessage(aInput);
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        public void randomNumber(userEntry commandUser, string argumentString, bool aSilent = false)
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
            sendChannelMessage(e.Channel, m_Localizer.getString("announceChannelJoin"));
            m_LogConnection.writeAndLog($"Bot account joined channel {e.Channel}");
        }

        private void Client_OnJoinedChannelJerp(object sender, OnJoinedChannelArgs e)
        {
            m_LogConnection.writeAndLog($"Owner account joined channel {e.Channel}");
        }

        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            m_HasChatConnection = true;
            m_LogConnection.writeAndLog($"Connected to {e.AutoJoinChannel}");
            m_TwitchClientBot.JoinChannel(m_DefaultChannel);
        }

        private void Client_OnConnectedOwner(object sender, OnConnectedArgs e)
        {
            m_LogConnection.writeAndLog($"jerpBot owner account connected to {e.AutoJoinChannel}");
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
                messageUser.inChannel = true;
                messageUser.twitchUserID = e.ChatMessage.UserId;

                processUserMessage(e.ChatMessage.Username, e.ChatMessage.Message);
            }
        }

        private void Client_OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
            m_SubsThisSession++;
            m_LogEvents.writeAndLog("User Subscribed - " + e.Subscriber.DisplayName);
        }

        private void Client_OnCommunitySubscription(object sender, OnCommunitySubscriptionArgs e)
        {
            m_SubsThisSession++;
            // Need to figure out what these params actually are
            m_LogEvents.writeAndLog("Community Subscription - " + e.GiftedSubscription.DisplayName + " | MsgParamSenderCount: " + e.GiftedSubscription.MsgParamSenderCount + " | MsgParamMassGiftCount: " + e.GiftedSubscription.MsgParamMassGiftCount);
        }

        private void Client_OnReSubscribe(object sender, OnReSubscriberArgs e)
        {
            m_SubsThisSession++;
            m_LogEvents.writeAndLog("User Resubscribed - " + e.ReSubscriber.DisplayName);
        }

        private void Client_OnGiftedSubscription(object sender, OnGiftedSubscriptionArgs e)
        {
            m_SubsThisSession++;
            m_LogEvents.writeAndLog("User Gifted a Subscription - " + e.GiftedSubscription.DisplayName + " gifted to " + e.GiftedSubscription.MsgParamRecipientDisplayName);
        }

        private void Client_OnLog(object sender, TwitchLib.Client.Events.OnLogArgs e)
        {
            m_LogConnection.writeAndLog($"{e.BotUsername} - {e.Data}");
        }

        private void Client_OnConnectionError(object sender, OnConnectionErrorArgs e)
        {
            m_LogWarningsErrors.writeAndLog($"{e.BotUsername} - {e.Error}");
        }

        private void Client_OnUserJoined(object sender, OnUserJoinedArgs e)
        {
            userEntry joinedUser = checkCreateUser(e.Username);
            joinedUser.inChannel = true;

            // This is a massive rate limit risk, disabled by default
            if (m_CoreConfig.configData.updateTwitchIDsOnUserJoins)
            {
                List<string> userList = new List<string>();
                userList.Add(e.Username.ToLower());

                try
                {
                    Task<TwitchLib.Api.Helix.Models.Users.GetUsers.GetUsersResponse> getUserIDTask = m_TwitchAPI.Helix.Users.GetUsersAsync(null, userList);
                    getUserIDTask.Wait();

                    if (getUserIDTask.Result != null && getUserIDTask.Result.Users.Length >= 1)
                    {
                        joinedUser.twitchUserID = getUserIDTask.Result.Users[0].Id;
                    }
                }
                catch (Exception exceptionInfo)
                {
                    m_LogWarningsErrors.writeAndLog("Client_OnUserJoined - Could not grab user ID for user " + e.Username + ": " + exceptionInfo.Message);
                }
            }

            botModule tempModule;
            for (int i = 0; i < m_Modules.Count; i++)
            {
                tempModule = m_Modules[i];

                if (isModuleValidForUserAction(tempModule))
                    tempModule.onUserJoin(joinedUser);
            }
        }

        private void Client_OnUserLeft(object sender, OnUserLeftArgs e)
        {
            userEntry leftUser = checkCreateUser(e.Username);
            leftUser.inChannel = false;
        }

        private void Client_OnRaidNotification(object sender, OnRaidNotificationArgs e)
        {
            m_LogEvents.writeAndLog("Raid from " + e.RaidNotification.MsgParamDisplayName + " with " + e.RaidNotification.MsgParamViewerCount + " viewers.");

            botModule tempModule;
            for (int i = 0; i < m_Modules.Count; i++)
            {
                tempModule = m_Modules[i];

                if (isModuleValidForUserAction(tempModule))
                    tempModule.onRaid(e.RaidNotification.MsgParamDisplayName, Int32.Parse(e.RaidNotification.MsgParamViewerCount));
            }
        }

        // ==========================================================

        private void requestChannelInfo()
        {
            TwitchLib.Api.Helix.Models.Channels.GetChannelInformation.ChannelInformation channelInfo = getSingleChannelInfoByName(m_TwitchCredentialsOwner.TwitchUsername);
            m_Game = channelInfo.GameName;
            m_Title = channelInfo.Title;
            m_Tags = channelInfo.Tags;
            setCategoryID(channelInfo.GameId);
            m_HasReceivedChannelInfo = true;
        }

        private void ParseStreamData(TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream aStream)
        {
            if (aStream != null)
            {
                m_IsLive = true;
                m_ViewersLast = aStream.ViewerCount;
                m_LiveStartTime = aStream.StartedAt;
                m_Game = aStream.GameName;
                m_Title = aStream.Title;
                m_Tags = aStream.Tags;
                setCategoryID(aStream.GameId);
                m_HasReceivedChannelInfo = true;
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

        private void PubSub_OnChannelPointsRewardRedeemed(object sender, OnChannelPointsRewardRedeemedArgs e)
        {
            processChannelPointRedemption(e.RewardRedeemed.Redemption.User.DisplayName, e.RewardRedeemed.Redemption.Reward.Title, e.RewardRedeemed.Redemption.Reward.Cost, e.RewardRedeemed.Redemption.UserInput, e.RewardRedeemed.Redemption.Reward.Id, e.RewardRedeemed.Redemption.Id);
        }

        private void PubSub_OnServiceConnected(object sender, EventArgs e)
        {
            m_LogConnection.writeAndLog("Connected to PubSub service, sending topics...");
            m_TwitchPubSubBot.SendTopics(m_CoreConfig.configData.pubsub.oauth);
        }

        private void PubSub_OnListenResponse(object sender, OnListenResponseArgs e)
        {
            if (!e.Successful)
                throw new Exception($"Failed to listen! Response: {e.Response}");
        }

        private void PubSub_OnFollowResponse(object sender, OnFollowArgs e)
        {
            userEntry messageUser = checkCreateUser(e.DisplayName);
            messageUser.isFollower = true;
            messageUser.twitchUserID = e.UserId;
            messageUser.lastFollowCheckTime = DateTime.Now;
            if (m_CoreConfig.configData.announceFollowEvents)
            {
                sendDefaultChannelMessage(string.Format(m_Localizer.getString("announceFollowEvent"), e.DisplayName));
            }
        }

        private void PubSub_OnCommercialResponse(object sender, OnCommercialArgs e)
        {
            m_LogEvents.writeAndLog("Commercial Started with Length:" + e.Length + " seconds.");

            botModule tempModule;
            for (int i = 0; i < m_Modules.Count; i++)
            {
                tempModule = m_Modules[i];

                if (isModuleValidForUserAction(tempModule))
                    tempModule.onCommercialStart(e);
            }
        }

        // ==========================================================

        public jerpBot(botConfig aConfig)
		{
            OperatingSystem osInfo = Environment.OSVersion;
            Version win8version = new Version(6, 2, 9200, 0);
            bool webSocketsSupported = (osInfo.Platform == PlatformID.Win32NT && osInfo.Version >= win8version); // Websockets requires Win8+

            m_CoreConfig = aConfig;
            m_Tags = new string[0];

            m_UserList = new Dictionary<string, userEntry>();
            m_Modules = new List<botModule>();
			actionQueue = new Queue<connectionCommand>();

            string databasePath = System.IO.Path.Combine(storagePath, "jerpbot.sqlite");
            m_StorageDB = new SQLiteConnection("Data Source=" + databasePath + ";Version=3;");
            m_StorageDB.Open();

            string createViewerTableQuery = "CREATE TABLE IF NOT EXISTS viewers (viewerID INTEGER PRIMARY KEY ASC, name varchar(25) UNIQUE, loyalty INTEGER, points INTEGER)";
            SQLiteCommand createViewerTableCommand = new SQLiteCommand(createViewerTableQuery, m_StorageDB);
            createViewerTableCommand.ExecuteNonQuery();

            m_LogGeneral = new logger("log_general");
            m_LogEvents = new logger("log_events");
            m_LogChat = new logger("log_chat");
            m_LogWarningsErrors = new logger("log_warnings_errors");
            m_LogConnection = new logger("log_connection");

            m_Localizer = new localizer(this);

            m_FollowerStaleCheckSeconds = m_CoreConfig.configData.followerStaleCheckSeconds;

            m_DefaultChannel = m_CoreConfig.configData.connections[0].channels[0];

            m_TwitchCredentialsBot = new ConnectionCredentials(m_CoreConfig.configData.connections[0].nickname, m_CoreConfig.configData.connections[0].oauth);
            m_TwitchCredentialsOwner = new ConnectionCredentials(m_CoreConfig.configData.connections[1].nickname, m_CoreConfig.configData.connections[1].oauth);

            TwitchLib.Client.Enums.ClientProtocol useClientProtocol = webSocketsSupported ? TwitchLib.Client.Enums.ClientProtocol.WebSocket : TwitchLib.Client.Enums.ClientProtocol.TCP;

            m_TwitchClientBot = new TwitchClient(protocol: useClientProtocol);
            m_TwitchClientOwner = new TwitchClient(protocol: useClientProtocol);

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
            m_TwitchClientOwner.OnRaidNotification += Client_OnRaidNotification;

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

            m_TwitchPubSubBot = new TwitchPubSub();

            m_TwitchPubSubBot.OnChannelPointsRewardRedeemed += PubSub_OnChannelPointsRewardRedeemed;
            m_TwitchPubSubBot.OnPubSubServiceConnected += PubSub_OnServiceConnected;
            m_TwitchPubSubBot.OnListenResponse += PubSub_OnListenResponse;
            m_TwitchPubSubBot.OnFollow += PubSub_OnFollowResponse;
            m_TwitchPubSubBot.OnCommercial += PubSub_OnCommercialResponse;

            m_TwitchPubSubBot.ListenToChannelPoints(m_CoreConfig.configData.twitch_api.channel_id.ToString());
            m_TwitchPubSubBot.ListenToFollows(m_CoreConfig.configData.twitch_api.channel_id.ToString());
            m_TwitchPubSubBot.ListenToVideoPlayback(m_CoreConfig.configData.twitch_api.channel_id.ToString());

            if (webSocketsSupported)
            {
                m_TwitchPubSubBot.Connect();
            }
            else
            {
                m_LogWarningsErrors.writeAndLog("Unable to check for followers/channel point redemptions, etc. via websockets -- requires Win8+");
            }            

            m_ActionTimer = Stopwatch.StartNew();

			m_CommandList = new List<chatCommandDef>();
			m_CommandList.Add(new chatCommandDef("botquit", quitCommand, false, false));
			m_CommandList.Add(new chatCommandDef("title", getStreamTitle, true, true));
			m_CommandList.Add(new chatCommandDef("game", getGameCommand, true, true));
			m_CommandList.Add(new chatCommandDef("viewers", getViewCount, true, true));
			m_CommandList.Add(new chatCommandDef("help", getHelpString, true, true));
            m_CommandList.Add(new chatCommandDef("random", randomNumber, true, true));
            m_CommandList.Add(new chatCommandDef("follower", checkFollower, true, true));
            m_CommandList.Add(new chatCommandDef("moderator", checkModerator, true, true));
            m_CommandList.Add(new chatCommandDef("subscriber", checkSub, true, true));
            m_CommandList.Add(new chatCommandDef("broadcaster", checkBroadcaster, true, true));
            m_CommandList.Add(new chatCommandDef("uptime", getUptime, true, true));
            m_CommandList.Add(new chatCommandDef("subcount", getNewSubCount, true, false));
            m_CommandList.Add(new chatCommandDef("brb", setUserBrb, true, true));
            m_CommandList.Add(new chatCommandDef("back", setUserBack, true, true));
            m_CommandList.Add(new chatCommandDef("followcount", announceChatterFollowingCount, false, false));
            m_CommandList.Add(new chatCommandDef("outputcommandlist", outputCommandList, false, false));
            m_CommandList.Add(new chatCommandDef("followage", followage, true, true));
            m_CommandList.Add(new chatCommandDef("marker", marker, true, false));
            m_CommandList.Add(new chatCommandDef("announce", announce, true, false));

            requestChannelInfo();
        }
	}
}
