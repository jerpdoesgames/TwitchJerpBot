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

		public override void frame()
		{
			if (isActive)
			{
				lock (messageLastLock)
				{

					if (m_Throttler.isReady)
					{
						string choiceNewString = "";
						string choiceUpdatedString = "";
						if (choicesNew > 0)
							choiceNewString = "  " + choicesNew + " new votes.";

						if (choicesUpdated > 0)
							choiceUpdatedString = "  " + choicesUpdated + " updated votes.";

						string choiceString = getChoiceString();

						if (!string.IsNullOrEmpty(description))
							m_BotBrain.sendDefaultChannelMessage("A poll is running (\"" + description + "\") - choices are " + choiceString + "  Type !vote # to cast your vote.  " + userChoices.Count + " vote(s) so far." + choiceNewString + choiceUpdatedString);
						else
							m_BotBrain.sendDefaultChannelMessage("A poll is currently open - choices are " + choiceString + "  Type !vote # to cast your vote.  " + userChoices.Count + " vote(s) so far." + choiceNewString + choiceUpdatedString);

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
				m_BotBrain.sendDefaultChannelMessage("Poll closed and cleared.");
			else
				m_BotBrain.sendDefaultChannelMessage("Poll cleared.");

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

		public void vote(userEntry commandUser, string argumentString)
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

		public void open(userEntry commandUser, string argumentString)
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
                        string choiceString = getChoiceString();
                        string descString = "";


                        if (!string.IsNullOrEmpty(description))
                        {
                            descString = " (" + description + ")";
                        }

                        m_BotBrain.sendDefaultChannelMessage("A new poll has been opened!" + descString + " Choices are " + choiceString + ".  Type !vote # to cast your vote.");
						m_Throttler.trigger();
                    }
                    else
                    {
                        m_BotBrain.sendDefaultChannelMessage("Unable to open poll - " + newChoices.Count + " choices is more than the max of " + choicesMax);
                    }
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage("Invalid choice list - poll not created!");
                }
            }

		}

		public void close(userEntry commandUser, string argumentString)
		{
			isActive = false;
			m_BotBrain.sendDefaultChannelMessage("Poll closed.");
		}

		public void about(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(description))
				m_BotBrain.sendDefaultChannelMessage("This Poll: " + description);
			else
				m_BotBrain.sendDefaultChannelMessage("This poll has not yet been described");
		}

        public void choices(userEntry commandUser, string argumentString)
        {
            if (isActive)
            {
                string choiceString = getChoiceString();
                m_BotBrain.sendDefaultChannelMessage("Choices are " + choiceString + ".  Type !vote # to cast your vote.");
            }
        }

        public void describe(userEntry commandUser, string argumentString)
		{
			if (!string.IsNullOrEmpty(argumentString))
			{
				description = argumentString;
                if (isActive)
                {
                    m_BotBrain.sendDefaultChannelMessage("Poll description updated.");
                }
			}
		}

		public void count(userEntry commandUser, string argumentString)
		{
			if (hasPoll)
			{
				if (isActive)
					m_BotBrain.sendDefaultChannelMessage(userChoices.Count + " vote(s) have been counted so far.");
				else
					m_BotBrain.sendDefaultChannelMessage(userChoices.Count + " vote(s) were counted in the last poll.");
			} else
			{
				m_BotBrain.sendDefaultChannelMessage("No poll on record.  Why not start one?");
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

		public void results(userEntry commandUser, string argumentString)
		{
			if (hasPoll)
			{
                string closeString = "";
                if (!string.IsNullOrEmpty(argumentString) && argumentString == "1")
                {
                    isActive = false;
                    closeString = "Poll Closed.  ";
                }

				if (userChoices.Count > 0)
				{
					string output = closeString+ "Poll results: ";
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

					for (var i = 0; i < choiceTotalList.Count; i++)
					{
                        if (choiceTotalList[i].count >= topValue)
                        {
                            topValue = choiceTotalList[i].count;
                            tieList.Add(i);
                        }

						if (counted)
							output += ", ";

						output += choiceTotalList[i].name + ": " + choiceTotalList[i].count;
						counted = true;
					}

                    string tieString = "";
                    if (tieList.Count > 1)
                    {
                        int randomChoice = m_BotBrain.randomizer.Next(0, tieList.Count - 1);
                        tieString = "Random tiebreaker is " + (tieList[randomChoice] + 1) + ": " + choiceList[tieList[randomChoice]];

                    }

                    m_BotBrain.sendDefaultChannelMessage(output);
				}
                else
				{
                    int randomChoice = m_BotBrain.randomizer.Next(0, choiceList.Count - 1);

					m_BotBrain.sendDefaultChannelMessage(closeString + "No votes yet!  Random tiebreaker is "+(randomChoice + 1) +":" + choiceList[randomChoice]);
				}

			}
		}

		public pollManager(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			m_Throttler = new throttler(aJerpBot);
			m_Throttler.waitTimeMax = 30000;
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
			tempDef.UseGlobalCooldown = false;
			m_BotBrain.addChatCommand(tempDef);
		}
	}
}