using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;

namespace JerpDoesBots
{
    class TwitchWSHostedService : IHostedService
    {
        private readonly ILogger<TwitchWSHostedService> _logger;
        private readonly EventSubWebsocketClient _eventSubWebsocketClient;

        public EventSubWebsocketClient EventSubClient { get { return _eventSubWebsocketClient; } }

        public TwitchWSHostedService(ILogger<TwitchWSHostedService> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _eventSubWebsocketClient = new EventSubWebsocketClient(loggerFactory);

            _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
            _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
            _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
            _eventSubWebsocketClient.ErrorOccurred += jerpBot.instance.Twitch_OnErrorOccurred;

            // _eventSubWebsocketClient.ChannelRaid += jerpBot.instance.Twitch_ChannelRaid; // TODO: This is apparently when a raid is outgoing, rather than an incoming raid (so this can be like "generate a raid message/etc.")

            _eventSubWebsocketClient.ChannelChatMessage += jerpBot.instance.Twitch_OnChannelChatMessage;
            _eventSubWebsocketClient.ChannelSubscribe += jerpBot.instance.Twitch_ChannelSubscribe;
            _eventSubWebsocketClient.ChannelFollow += jerpBot.instance.Twitch_OnChannelFollow;
            _eventSubWebsocketClient.ChannelSubscriptionGift += jerpBot.instance.Twitch_ChannelSubscriptionGift;
            _eventSubWebsocketClient.StreamOnline += jerpBot.instance.Twitch_StreamOnline;
            _eventSubWebsocketClient.StreamOffline += jerpBot.instance.Twitch_StreamOffline;
            _eventSubWebsocketClient.ChannelAdBreakBegin += jerpBot.instance.Twitch_ChannelAdBreakBegin;
            _eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += jerpBot.instance.Twitch_ChannelPointsCustomRewardRedemptionAdd;
        }

        // TODO: Move elsewhere

        private async void AttemptSubscription(string aTopic, Dictionary<string, string> aConditions, string aVersion="1")
        {
            try
            {
                CreateEventSubSubscriptionResponse subResponse = await jerpBot.instance.twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(aTopic, aVersion, aConditions, TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket, _eventSubWebsocketClient.SessionId);
            }
            catch (Exception subException)
            {
                jerpBot.instance.logConnection.writeAndLog($"EventSub subscription failed for topic \"{aTopic}\" - {subException.Message}");
            }
        }

        public async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
        {
            jerpBot.instance.logConnection.writeAndLog($"Websocket connected with ID: {_eventSubWebsocketClient.SessionId}");

            if (!e.IsRequestedReconnect)
            {
                // subscribe to topics
                Dictionary<string, string> conditions = new Dictionary<string, string>()
                {
                    { "broadcaster_user_id", jerpBot.instance.ownerUserID },
                    { "user_id", jerpBot.instance.ownerUserID },
                    { "moderator_user_id", jerpBot.instance.ownerUserID }
                };

                // AttemptSubscription("channel.raid", conditions);

                AttemptSubscription("channel.chat.message", conditions);
                AttemptSubscription("channel.subscribe", conditions);
                AttemptSubscription("channel.follow", conditions, "2");
                AttemptSubscription("channel.subscription.gift", conditions);
                AttemptSubscription("stream.online", conditions);
                AttemptSubscription("stream.offline", conditions);
                AttemptSubscription("channel.ad_break.begin", conditions);
                AttemptSubscription("channel.channel_points_custom_reward_redemption.add", conditions);
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _eventSubWebsocketClient.ConnectAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _eventSubWebsocketClient.DisconnectAsync();
        }

        // TODO: Move elsewhere
        private async Task OnWebsocketDisconnected(object sender, EventArgs e)
        {
            jerpBot.instance.logConnection.writeAndLog($"Websocket {_eventSubWebsocketClient.SessionId} disconnected!");

            // Don't do this in production. You should implement a better reconnect strategy
            while (!await _eventSubWebsocketClient.ReconnectAsync())
            {
                jerpBot.instance.logConnection.writeAndLog("Websocket reconnect failed!");
                await Task.Delay(10000);
            }
        }

        private async Task OnWebsocketReconnected(object sender, EventArgs e)
        {
            jerpBot.instance.logConnection.writeAndLog($"Websocket {_eventSubWebsocketClient.SessionId} reconnected");
        }
    }
}
