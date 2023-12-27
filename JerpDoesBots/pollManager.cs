using System;
using System.Collections.Generic;

namespace JerpDoesBots
{
	class pollManager : botModule
	{
		private Dictionary<userEntry, int> userChoices;
		private bool isActive = false;
		private bool hasPoll = false;
		private List<string> choiceList;
		private string description;
		private int choicesMax = 10;

		private uint choicesNew = 0;
		private uint choicesUpdated = 0;

		private throttler m_Throttler;
		private readonly object messageLastLock = new object();

		private void reset()
		{
			userChoices.Clear();
		}

		public override void onFrame()
		{
			if (isActive)
			{
				lock (messageLastLock)
				{

					if (m_Throttler.isReady)
					{
						string output = !string.IsNullOrEmpty(description) ? m_BotBrain.localizer.getString("pollAnnounceDescribed") : m_BotBrain.localizer.getString("pollAnnounce");
						output += "  " + string.Format(m_BotBrain.localizer.getString("pollChoiceList"), getChoiceString()) + "  ";
						output += m_BotBrain.localizer.getString("pollVoteHint") + "  ";
						output += string.Format(m_BotBrain.localizer.getString("pollVotesCurrent"), userChoices.Count);

						if (choicesNew > 0)
							output += "  " + string.Format(m_BotBrain.localizer.getString("pollNewVoteCount"), choicesNew);

						if (choicesUpdated > 0)
							output += "  " + string.Format(m_BotBrain.localizer.getString("pollUpdatedVoteCount"), choicesUpdated);

						m_BotBrain.sendDefaultChannelMessage(output);

						choicesNew = 0;
						choicesUpdated = 0;

						m_Throttler.trigger();
					}
				}
			}
		}

		public void clear(userEntry commandUser, string argumentString)
		{
			reset();
			description = null;
			hasPoll = false;

			if (isActive)
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("pollCloseClear"));
			else
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("pollClear"));

			isActive = false;
		}

		private bool validChoice(int choiceID)
		{
			return (choiceID > 0 && choiceID <= choiceList.Count);
		}

        private void registerChoice(userEntry commandUser, int choiceIndex)
        {
            bool hasEntry = (userChoices.ContainsKey(commandUser));

            userChoices[commandUser] = choiceIndex;

            if (hasEntry)
                choicesUpdated++;
            else
                choicesNew++;
        }

		public void vote(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			if (hasPoll && isActive)
			{
                int choiceID; ;

				if (Int32.TryParse(argumentString, out choiceID) && validChoice(choiceID))
				{
                    registerChoice(commandUser, choiceID - 1);
                }
                else
                {
                    choiceID = choiceList.IndexOf(argumentString);

                    if (choiceID >= 0)
                        registerChoice(commandUser, choiceID);
                }
			}
		}

		private string getChoiceString()
		{
			string output = "";
			for (int i = 0; i < choiceList.Count; i++)
			{
				if (output.Length > 0)
					output += ", ";

				output += (i + 1) + ": " + choiceList[i];
			}

			return output;
		}

		public void open(userEntry commandUser, string argumentString, bool aSilent = false)
		{
            lock(messageLastLock)
            {
                if (!string.IsNullOrEmpty(argumentString) && argumentString.Length >= 3 && argumentString.IndexOf(" ") > 0)
                {
                    List<string> newChoices = new List<string>(argumentString.Split(' '));

                    if (newChoices.Count <= choicesMax)
                    {
                        reset();
                        isActive = true;
                        hasPoll = true;

                        choiceList = newChoices;
                        string descString = "";


                        if (!string.IsNullOrEmpty(description))
                        {
                            descString = " (" + description + ")";
                        }

                        m_BotBrain.sendDefaultChannelAnnounce(m_BotBrain.localizer.getString("pollOpenSuccess") + descString + "  " + string.Format(m_BotBrain.localizer.getString("pollChoiceList"), getChoiceString()) + "  " + m_BotBrain.localizer.getString("pollVoteHint"));
						m_Throttler.trigger();
                    }
                    else
                    {
                        m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("pollOpenFailChoiceMax"), newChoices.Count, choicesMax));
					}
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("pollOpenFailInvalidChoices"));
                }
            }

		}

		public void close(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			isActive = false;
			if (!aSilent)
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("pollClose"));
		}

		public void about(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			if (!string.IsNullOrEmpty(description))
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("pollDescriptionAnnounce"), description));
			else
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("pollDescriptionEmpty"));
		}

        public void choices(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (isActive)
            {
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("pollChoiceList"), getChoiceString()) + "  " + m_BotBrain.localizer.getString("pollVoteHint"));
            }
        }

        public void describe(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				description = argumentString;
                if (isActive)
                {
					if (!aSilent)
						m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("pollDescriptionUpdate"));
                }
			}
		}

		public void count(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			if (hasPoll)
			{
				if (isActive)
					m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("pollCountActive"), userChoices.Count));
				else
					m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("pollCountInactive"), userChoices.Count));
			} else
			{
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("pollNotFound"));
			}
		}

		public struct pollVoteTotal
		{
			public string name;
			public int count;

			public pollVoteTotal(string aName, int aCount)
			{
				name = aName;
				count = aCount;
			}
		}

		public void results(userEntry commandUser, string argumentString, bool aSilent = false)
		{
			string output = "";
			if (hasPoll)
			{
                if (!string.IsNullOrEmpty(argumentString) && argumentString == "1")
                {
                    isActive = false;
                    output += m_BotBrain.localizer.getString("pollClose") + "  ";
                }

				if (userChoices.Count > 0)
				{
					Dictionary<int, int> choiceTotals = new Dictionary<int, int>();

					// Clone the list so we can sort it via results without screwing up the voting
					for (int i = 0; i < choiceList.Count; i++)
						choiceTotals[i] = 0;

					foreach (KeyValuePair<userEntry, int> userChoice in userChoices)
					{
						if (choiceTotals.ContainsKey(userChoice.Value))
						{
							choiceTotals[userChoice.Value]++;
						}
					}

					List<pollVoteTotal> choiceTotalList = new List<pollVoteTotal>();

					foreach (KeyValuePair<int, int> vTotal in choiceTotals)
					{
						choiceTotalList.Add(new pollVoteTotal(choiceList[vTotal.Key], vTotal.Value));
					}

					choiceTotalList.Sort(delegate(pollVoteTotal a, pollVoteTotal b)
					{
						if (a.count == b.count)
							return 0;
						else
							return b.count - a.count;
					});

					bool counted = false;
                    List<int> tieList = new List<int>();
                    int topValue = 0;
					string resultsString = "";

					for (var i = 0; i < choiceTotalList.Count; i++)
					{
                        if (choiceTotalList[i].count >= topValue)
                        {
                            topValue = choiceTotalList[i].count;
                            tieList.Add(i);
                        }

						if (counted)
							resultsString += ", ";

						resultsString += choiceTotalList[i].name + ": " + choiceTotalList[i].count;
						counted = true;
					}

					output += string.Format(m_BotBrain.localizer.getString("pollResults"), resultsString);

					if (tieList.Count > 1)
                    {
                        int randomChoice = m_BotBrain.randomizer.Next(0, tieList.Count - 1);
						output += "  " + string.Format(m_BotBrain.localizer.getString("pollTiebreaker"), (tieList[randomChoice] + 1), choiceList[tieList[randomChoice]]);
                    }
				}
                else
				{
					output += m_BotBrain.localizer.getString("pollNoVotes");
                    int randomChoice = m_BotBrain.randomizer.Next(0, choiceList.Count - 1);

					output += "  " + string.Format(m_BotBrain.localizer.getString("pollTiebreaker"), (randomChoice + 1), choiceList[randomChoice]);
				}

				m_BotBrain.sendDefaultChannelMessage(output);
			}
		}

		public pollManager(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			m_Throttler = new throttler(aJerpBot);
			m_Throttler.waitTimeMSMax = 30000;
			m_Throttler.lineCountMinimum = 8;

			userChoices = new Dictionary<userEntry, int>();

			chatCommandDef tempDef = new chatCommandDef("poll", null, false, false);
			tempDef.addSubCommand(new chatCommandDef("open", open, true, false));
			tempDef.addSubCommand(new chatCommandDef("close", close, true, false));
			tempDef.addSubCommand(new chatCommandDef("about", about, true, true));
			tempDef.addSubCommand(new chatCommandDef("describe", describe, true, false));
			tempDef.addSubCommand(new chatCommandDef("count", count, true, false));
			tempDef.addSubCommand(new chatCommandDef("results", results, true, false));
            tempDef.addSubCommand(new chatCommandDef("choices", choices, true, false));
            m_BotBrain.addChatCommand(tempDef);

			tempDef = new chatCommandDef("vote", vote, true, true);
			tempDef.useGlobalCooldown = false;
			m_BotBrain.addChatCommand(tempDef);
		}
	}
}