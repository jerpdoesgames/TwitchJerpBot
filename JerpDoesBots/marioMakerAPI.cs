using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
    public enum marioMakerGameStyle : int // mario world, mario 3, etc.
	{
		mario,
		mario3,
		marioWorld,
		newSMB,
		mario3DWorld
	}

	public enum marioMakerTileTheme : int // underground, airship, etc.
	{
		Overworld,
		Underground,
		Castle,
		Airship,
		Underwater,
		GhostHouse,
		Snow,
		Desert,
		Sky,
		Forest
	}

	public enum marioMakerLevelTag : int
	{
		none,
		standard,
		puzzle,
		speedrun,
		autoScroll,
		autoMario,
		shortSweet,
		multiplayerVS,
		themed,
		music,
		art,
		technical,
		shooter,
		bossBattle,
		singlePlayer,
		link
	};

	public enum marioMakerClearConditionRequirement : int
	{
		either,
		forbidden,
		mandatory
	}

	class marioMakerLevelInfo
	{
		public string name { get; set; }
		public string description { get; set; }
		public marioMakerGameStyle game_style { get; set; }
		public marioMakerTileTheme theme { get; set; }    // Main level (sub-level isn't shown?)
		public List<marioMakerLevelTag> tags { get; set; }
		public string clear_condition_name { get; set; }	// The actual clear_condition is an ID that could refer to many/most entities in the game
		public int clear_condition_magnitude { get; set; }
		public int attempts { get; set; }
		public int clears { get; set; }
		public int plays { get; set; }
		public int likes { get; set; }
		public int boos { get; set; }
		public double upload_time { get; set; }
		public double world_record { get; set; }
		public string error { get; set; }

		public string theme_name { get; set; }
		public List<string> tags_name { get; set; }
		public string game_style_name { get; set; }

		public float clearPercentage { get { return (float)clears / (float)attempts; } }
		public float likePercentage {
			get {
				if (likes > 0 && boos == 0)
					return 1.0f;
				else if (likes == 0 && boos > 0)
					return 0.0f;
				else if (likes == 0 && boos == 0)
					return 1.0f;
				else
					return (float)likes / ((float)likes + (float)boos);
			}
		}
		public TimeSpan timeClear { get { return TimeSpan.FromMilliseconds(upload_time); } }
		public TimeSpan timeWR { get { return TimeSpan.FromMilliseconds(world_record); } }

		public bool isValid { get { return string.IsNullOrEmpty(error); } }
		private DateTime m_QueryTime;
		public DateTime queryTime { get { return m_QueryTime; } }
		public TimeSpan fastestClearTime { get { return timeClear.TotalSeconds < timeWR.TotalSeconds ? timeClear : timeWR; } }

		public marioMakerLevelInfo()
        {
			m_QueryTime = DateTime.Now;
		}

		public bool hasAllTags(List<marioMakerLevelTag> aTags)
        {
			for (int i = 0; i < aTags.Count; i++)
				if (tags.IndexOf(aTags[i]) == -1)
					return false;

			return true;
        }
	}

    class marioMakerAPI
	{
		public static Regex marioMaker2CodeReg = new Regex(@"^[a-zA-Z0-9]{3}-[a-zA-Z0-9]{3}-[a-zA-Z0-9]{3}$");
		public static string durationString(TimeSpan aTimeSpan)
        {
			return aTimeSpan.ToString("m\\:ss\\.fff");
        }
		public static marioMakerLevelInfo getLevelInfo(string aLevelID)
        {
			if (!string.IsNullOrEmpty(aLevelID) && marioMaker2CodeReg.IsMatch(aLevelID))
			{
				aLevelID = aLevelID.Replace("-", string.Empty).ToUpper();

				try
				{
					WebClient newClient = new WebClient();
					string jsonString = newClient.DownloadString("https://tgrcode.com/mm2/level_info/" + aLevelID);

					if (!string.IsNullOrEmpty(jsonString))
					{
						marioMakerLevelInfo curLevelInfo = new JavaScriptSerializer().Deserialize<marioMakerLevelInfo>(jsonString);

						if (string.IsNullOrEmpty(curLevelInfo.error))
						{
							return curLevelInfo;
						}

					}
				}
				catch (Exception e)
				{
					Console.WriteLine("Exception on parsing Mario Maker 2 level info: " + e.Message);
				}
			}

			Console.WriteLine("Failed to grab Mario Maker 2 level info.");

			return null;
        }
	}
}