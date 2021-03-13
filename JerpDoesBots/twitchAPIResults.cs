using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JerpDoesBots
{
    class twitchAPIResults
    {

        public class channelInfo
        {
            public bool mature { get; set; }
            public string status { get; set; }
            public string broadcaster_language { get; set; }
            public string display_name { get; set; }
            public string game { get; set; }
            public string language { get; set; }
            public long _id { get; set; }
            public string name { get; set; }
            public string created_at { get; set; }
            public string updated_at { get; set; }
            public bool partner { get; set; }
            public string logo { get; set; }
            public string video_banner { get; set; }
            public string profile_banner { get; set; }
            public string profile_banner_background_color { get; set; }
            public string url { get; set; }
            public long views { get; set; }
            public long followers { get; set; }
            public linksEntry _links { get; set; }
            // public long delay { get; set; }
            // public string banner { get; set; }
            // public string background { get; set; }
        }

        public class previewInfo
        {
            public string small { get; set; }
            public string medium { get; set; }
            public string large { get; set; }
            public string template { get; set; }
        }

        public class streamInfo
        {
            public long _id { get; set; }
            public string game { get; set; }
            public uint viewers { get; set; }
            public uint video_height { get; set; }
            public float average_fps { get; set; }
            public long delay { get; set; }
            public string created_at { get; set; }
            public bool is_playlist { get; set; }
            public previewInfo preview { get; set; }
            public channelInfo channel { get; set; }
        }

        public class channelStatus
        {
            public streamInfo stream { get; set; }
        }

		public class teamEntry
		{

		}

		public class linksEntry
		{
			public string stream_key { get; set; }
			public string editors { get; set; }
			public string subscriptions { get; set; }
			public string commercial { get; set; }
			public string videos { get; set; }
			public string follows { get; set; }
			public string self { get; set; }
			public string chat { get; set; }
			public string features { get; set; }
		}

		public class streamEntry
		{
			public linksEntry _links { get; set; }
		}

		public class streamResult
		{
			public linksEntry _links { get; set; }
			public streamEntry stream { get; set; }
		}
	}
}
