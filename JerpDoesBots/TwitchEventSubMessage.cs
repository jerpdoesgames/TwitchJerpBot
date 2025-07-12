using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JerpDoesBots
{
    internal class TwitchEventSubMessageMetadata
    {
        public string mesage_id { get; set; }
        public string message_type { get; set; }
        public string message_timestamp { get; set; }
        public string subscription_type { get; set; }
        public string subscription_version { get; set; }
    }

    internal class TwitchEventSubMessagePayload
    {
        public TwitchEventSubMessageSubscription subscription { get; set; }
        public TwitchEventSubMessageSession session { get; set; }
        [JsonPropertyName("event")]
        public TwitchEventSubMessageEvent eventData { get; set; }  // Ugh
    }

    internal class TwitchEventSubMessageSession
    {
        public string id { get; set; }
        public string status { get; set; }
        public int keepalive_timeout_seconds { get; set; }
        public string reconnect_url { get; set; }
        public string connected_at { get; set; }
    }

    internal class TwitchEventSubMessageCondition
    {
        public string broadcaster_user_id { get; set; }
        public string user_id { get; set; }
    }

    internal class TwitchEventSubMessageMessage
    {
        public string text { get; set; }
        public List<TwitchEventSubMessageFragment> fragments { get; set; }
        public List<TwitchEventSubMessageEmote> emotes { get; set; }
    }

    internal class TwitchEventSubMessageEmote
    {
        public string id { get; set; }
        public int begin { get; set; }
        public int end { get; set; }
        public string text { get; set; }
        [JsonPropertyName("set-id")]
        public string setID { get; set; }
        public int amount { get; set; }
        public string prefix { get; set; }
        public int tier { get; set; }

    }

    internal class TwitchEventSubMessageFragment
    {
        public string type { get; set; }
        public string text { get; set; }
        public bool? cheermote { get; set; }
        public TwitchEventSubMessageEmote emote { get; set; }
        public bool? mention { get; set; }
        public List<TwitchEventSubMessageEmote> emotes { get; set; }
        public List<TwitchEventSubMessageEmote> cheermotes { get; set; }
    }

    internal class TwitchEventSubMessageEvent
    {
        public string user_id { get; set; }
        public string user_login { get; set; }
        public string user_name { get; set; }
        public string broadcaster_user_id { get; set; }
        public string broadcaster_user_login { get; set; }
        public string broadcaster_user_name { get; set; }
        public string followed_at { get; set; }
        public string color { get; set; }
        public List<TwitchEventSubMessageBadge> badges { get; set; }
        public string message_type { get; set; }
        public string tier { get; set; }
        public bool? is_gift { get; set; }
        public string cheer { get; set; }
        public string reply { get; set; }
        public string channel_points_custom_reward_id { get; set; }
        public string source_broadcaster_user_id { get; set; }
        public string source_broadcaster_user_login { get; set; }
        public string source_broadcaster_user_name { get; set; }
        public string source_message_id { get; set; }
        public string source_badges { get; set; }
        public int total { get; set; }
        public int cumulative_total { get; set; }
        public bool? is_anonymous { get; set; }
        public int type { get; set; }
        public string started_at { get; set; }
        public string duration_seconds { get; set; }
        public int is_automatic { get; set; }
        public string requester_user_id { get; set; }
        public string requester_user_login { get; set; }
        public string requester_user_name { get; set; }
        public string user_input { get; set; }
        public string redeemed_at { get; set; }
        public bool? is_enabled { get; set; }
        public bool? is_paused { get; set; }
        public bool? is_in_stock { get; set; }
        public string title { get; set; }
        public int cost { get; set; }
        public string prompt { get; set; }
        public bool? is_user_input_required { get; set; }
        public bool? should_redemptions_skip_request_queue { get; set; }
        public string cooldown_expires_at { get; set; }
        public string redemptions_redeemed_current_stream { get; set; }
        public TwitchEventSubMessageGlobalCooldown global_cooldown { get; set; }
        public TwitchEventSubMessageMaxPer max_per_stream { get; set; }
        public TwitchEventSubMessageMaxPer max_per_user_per_stream { get; set; }
        public string background_color { get; set; }
        public Dictionary<string, string> image { get; set; }
        public Dictionary<string, string> default_image { get; set; }
        public string chatter_user_id { get; set; }
        public string chatter_user_login { get; set; }
        public string chatter_user_name { get; set; }
        public TwitchEventSubMessageMessage message { get; set; }
        public TwitchEventSubMessageReward reward { get; set; }
        public string id { get; set; }
    }

    internal class TwitchEventSubMessageMaxPer
    {
        public bool is_enabled { get; set; }
        public int value { get; set; }
    }

    internal class TwitchEventSubMessageGlobalCooldown
    {
        public bool is_enabled { get; set; }
        public int seconds { get; set; }
    }

    internal class TwitchEventSubMessageReward
    {
        public string type { get; set; }
        public int cost { get; set; }
        public string unlocked_emote { get; set; }
        public string title { get; set; }
        public string id { get; set; }
    }

    internal class TwitchEventSubMessageBadge
    {
        [JsonPropertyName("set_id")]
        public string setID { get; set; }
        public string id { get; set; }
        public string info { get; set; }
    }

    internal class TwitchEventSubMessageSubscription
    {
        public string id { get; set; }
        public string status { get; set; }
        public string type { get; set; }
        public string version { get; set; }
        public int cost { get; set; }
        public TwitchEventSubMessageCondition condition { get; set; }
        public TwitchEventSubMessageTransport transport { get; set; }
        public string created_at { get; set; }
    }

    internal class TwitchEventSubMessageTransport
    {
        public string method { get; set; }
        public string session_id { get; set; }
    }

    internal class TwitchEventSubMessage
    {
        public TwitchEventSubMessageMetadata metadata { get; set; }
        public TwitchEventSubMessagePayload payload { get; set; }
    }
}
