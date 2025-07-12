using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.EventSub.Websockets;

namespace JerpDoesBots
{
    internal class TwitchEventSubHandler : botModule
    {
        private ClientWebSocket m_Connection;
        static int m_WSKeepaliveSeconds = 30;
        private Uri m_EventSubURI = new Uri("wss://eventsub.wss.twitch.tv/ws?keepalive_timeout_seconds=" + m_WSKeepaliveSeconds.ToString());
        private string m_SessionID = "";
        public bool waitingForMessage = false;


        private async void attemptSubscription(string aTopic, Dictionary<string, string> aConditions, string aVersion = "1")
        {
            if (m_Connection.State == WebSocketState.Open)
            {
                try
                {
                    CreateEventSubSubscriptionResponse subResponse = await jerpBot.instance.twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(aTopic, aVersion, aConditions, TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket, m_SessionID);
                }
                catch (Exception subException)
                {
                    jerpBot.instance.logConnection.writeAndLog($"EventSub subscription failed for topic \"{aTopic}\" - {subException.Message}");
                }
            }
            else
            {
                jerpBot.instance.logConnection.writeAndLog($"Websocket connection closed - skipping EventSub subscription for topic \"{aTopic}\"");
            }
        }

        private void processReceivedMessage(TwitchEventSubMessage aMessage, string aRawMessage)
        {
            switch (aMessage.metadata.message_type)
            {
                case "session_welcome":
                    m_SessionID = aMessage.payload.session.id;
                    Dictionary<string, string> conditions = new Dictionary<string, string>()
                                        {
                                            { "broadcaster_user_id", jerpBot.instance.ownerUserID },
                                            { "user_id", jerpBot.instance.ownerUserID },
                                            { "moderator_user_id", jerpBot.instance.ownerUserID }
                                        };
                    attemptSubscription("channel.chat.message", conditions);
                    attemptSubscription("channel.subscribe", conditions);
                    attemptSubscription("channel.follow", conditions, "2");
                    attemptSubscription("channel.subscription.gift", conditions);
                    attemptSubscription("stream.online", conditions);
                    attemptSubscription("stream.offline", conditions);
                    attemptSubscription("channel.ad_break.begin", conditions);
                    attemptSubscription("channel.channel_points_custom_reward_redemption.add", conditions);
                    break;
                case "session_keepalive":
                    // KeepAlive message - not gonna bother with it for now.
                    // Would probably DateTime.Parse the timestamp and check against it here and there
                    break;
                case "notification":
                    string username;
                    string userID;
                    switch (aMessage.metadata.subscription_type)
                    {
                        case "channel.chat.message":
                            username = aMessage.payload.eventData.chatter_user_name;
                            userID = aMessage.payload.eventData.chatter_user_id;
                            string messageText = aMessage.payload.eventData.message.text;
                            string broadcasterID = aMessage.payload.eventData.broadcaster_user_id;
                            bool isModerator = false;
                            bool isSubscriber = false;

                            if (aMessage.payload.eventData.badges != null)
                            {
                                foreach (TwitchEventSubMessageBadge curBadge in aMessage.payload.eventData.badges)
                                {
                                    switch (curBadge.setID)
                                    {
                                        case "moderator":
                                            isModerator = true;
                                            break;
                                        case "subscriber":
                                            isSubscriber = true;
                                            break;
                                        case "sub-gifter":
                                            // TODO: Sub gifter stuff?
                                            break;
                                    }

                                }
                            }

                            jerpBot.instance.receiveEventChatMessage(broadcasterID, username, userID, messageText, false, isModerator, isSubscriber);

                            break;
                        case "channel.subscribe":
                            jerpBot.instance.receiveEventUserSubscribe(aMessage.payload.eventData.user_name);
                            break;
                        case "channel.follow":
                            username = aMessage.payload.eventData.chatter_user_name;
                            userID = aMessage.payload.eventData.chatter_user_id;
                            jerpBot.instance.receiveEventChannelFollow(userID, username);
                            break;
                        case "channel.subscription.gift":
                            username = aMessage.payload.eventData.chatter_user_name;
                            bool isAnonymousGift = aMessage.payload.eventData.is_anonymous.HasValue ? aMessage.payload.eventData.is_anonymous.Value : false;
                            jerpBot.instance.receiveEventSubGift(username, isAnonymousGift, aMessage.payload.eventData.total, aMessage.payload.eventData.cumulative_total);
                            break;
                        case "stream.online":
                            jerpBot.instance.receiveEventStreamOnline(DateTime.Parse(aMessage.payload.eventData.started_at));
                            break;
                        case "stream.offline":
                            jerpBot.instance.receiveEventStreamOffline();
                            break;
                        case "channel.ad_break.begin":
                            jerpBot.instance.receiveEventAdBreakBegin(int.Parse(aMessage.payload.eventData.duration_seconds));
                            break;
                        case "channel.channel_points_custom_reward_redemption.add":
                            username = aMessage.payload.eventData.user_name;
                            TwitchEventSubMessageReward rewardEntry = aMessage.payload.eventData.reward;
                            jerpBot.instance.receiveEventChannelPointRewardRedemption(username, rewardEntry.title, rewardEntry.cost, aMessage.payload.eventData.user_input, rewardEntry.id, aMessage.payload.eventData.id);
                            break;
                        default:
                            jerpBot.instance.logWarningsErrors.writeAndLog("Unknown EventSub notification subscription_type " + aMessage.metadata.subscription_type);
                            jerpBot.instance.logWarningsErrors.writeAndLog(aRawMessage);
                            break;
                    }
                    break;
                default:
                    jerpBot.instance.logWarningsErrors.writeAndLog("Unknown EventSub message_type " + aMessage.metadata.message_type);
                    jerpBot.instance.logWarningsErrors.writeAndLog(aRawMessage);
                    break;
            }
        }

        public async Task waitForIncomingMessage()
        {
            byte[] bytesReceived = new byte[1024];

            WebSocketReceiveResult receiveResult = await m_Connection.ReceiveAsync(new ArraySegment<byte>(bytesReceived), CancellationToken.None);

            // Task<WebSocketReceiveResult> receiveTask = Task.Run(() => );
            // receiveTask.Wait();

            if (receiveResult != null)
            {
                using (MemoryStream incomingMessageStream = new MemoryStream())
                {
                    incomingMessageStream.Write(bytesReceived, 0, receiveResult.Count);
                    while (!receiveResult.EndOfMessage)
                    {
                        // receiveTask = Task.Run(() => m_Connection.ReceiveAsync(new ArraySegment<byte>(bytesReceived), CancellationToken.None));
                        // receiveTask.Wait();
                        receiveResult = await m_Connection.ReceiveAsync(new ArraySegment<byte>(bytesReceived), CancellationToken.None);

                        if (receiveResult != null)
                        {
                            incomingMessageStream.Write(bytesReceived, 0, receiveResult.Count);
                        }
                    }

                    incomingMessageStream.Seek(0, SeekOrigin.Begin);
                    using (StreamReader messageReader = new StreamReader(incomingMessageStream, Encoding.UTF8))
                    {
                        string parsedMessage = messageReader.ReadToEnd();
                        if (!string.IsNullOrEmpty(parsedMessage))
                        {
                            TwitchEventSubMessage messageObject = JsonSerializer.Deserialize<TwitchEventSubMessage>(parsedMessage);
                            processReceivedMessage(messageObject, parsedMessage);
                        }
                    }
                }
            }

            waitingForMessage = false;
        }
        public override void onFrame()
        {
            if (m_Connection.State == WebSocketState.Open)
            {
                if (!waitingForMessage)
                {
                    waitForIncomingMessage();
                    waitingForMessage = true;
                }
            }
            else if (m_Connection.State == WebSocketState.CloseReceived)
            {
                closeConnection();
            }
        }

        public void closeConnection()
        {
            // Task closeTask = Task.Run(() => m_Connection.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal close event", CancellationToken.None));  // TODO: Figure out what closeDescription should actually be
            // closeTask.Wait();
            // TODO: How do we close this websocket connection?
            Console.WriteLine("EventSub WebSocket would be closed here, once I figure out what to do.");
        }

        public TwitchEventSubHandler() : base(false, false, false)
        {
            m_Connection = new ClientWebSocket();
            Task connectionTask = m_Connection.ConnectAsync(m_EventSubURI, CancellationToken.None);
            connectionTask.Wait();
        }
    }
}
