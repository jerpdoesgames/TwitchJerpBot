using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.IO;

namespace JerpDoesBots
{

    public class triviaQuestion
    {
        public string title { get; set; }
        public string addendum { get; set; }
        public List<string> tags { get; set; }
        public List<string> answers { get; set; }
        public triviaCategory parentCategory { get; set; }

        public string getFormattedTitle()
        {
            return parentCategory.name + " | " + title;
        }
    }

    public class triviaCategory
    {
        public string name { get; set; }
        public string code { get; set; }
        public List<triviaQuestion> questions { get; set; }
    }

    class trivia : botModule
	{
        private List<triviaCategory> m_Categories { get; set; }
        private Dictionary<userEntry, int> m_Scores;
        private long m_TimeToAnswer = 450000;
        private long m_TimeSinceLastAnswer = 0;
        private bool m_LoadSuccessful = false;

		private long m_MessageThrottle = 60000;
        private long m_MessageThrottleMax = 120000;
		private long m_MessageTimeLast = 0;
		private bool m_IsActive = false;
        private int m_TotalQuestions = 15;
		private long m_LastLineCount = -2;
		private long m_LineCountMinimum = 6;
        private List<triviaQuestion> m_Questions;
        private int m_CurrentQuestionIndex = 0;

        public struct triviaParticipant
        {
            public userEntry user;
            public int score;

            public triviaParticipant(userEntry aUser, int aScore = 0)
            {
                user = aUser;
                score = aScore;
            }
        }

        private triviaQuestion getCurrentQuestion()
        {
            if (m_CurrentQuestionIndex < m_Questions.Count)
                return m_Questions[m_CurrentQuestionIndex];

            return null;
        }

        private void checkCreateParticipant(userEntry aUser)
        {
            if (!m_Scores.ContainsKey(aUser))
            {
                m_Scores[aUser] = 0;
            }
        }

        public void question(userEntry commandUser, string argumentString)
        {
            triviaQuestion currentQuestion = getCurrentQuestion();

            if (currentQuestion != null)
                m_BotBrain.sendDefaultChannelMessage("The current question is: " + getCurrentQuestion().getFormattedTitle());
        }

        public void start(userEntry commandUser, string argumentString)
		{
            if (!string.IsNullOrEmpty(argumentString))
            {
                List<string> tagList = new List<string>();
                tagList = argumentString.Split(' ').ToList();

                List<triviaQuestion> newQuestions = getQuestionsForTags(tagList);
                string tagListString = string.Join(", ", tagList.ToArray());

                if (newQuestions.Count > 0)
                {
                    newQuestions.Sort(delegate (triviaQuestion a, triviaQuestion b)
                    {
                        return m_BotBrain.randomizer.Next() - m_BotBrain.randomizer.Next();
                    });

                    m_Questions = newQuestions.GetRange(0, Math.Min(m_TotalQuestions, newQuestions.Count));
                    m_CurrentQuestionIndex = 0;
                    m_Scores = new Dictionary<userEntry, int>();

                    m_BotBrain.sendDefaultChannelMessage("A new Trivia game has started!  Topics include " + tagListString + " (" + m_Questions.Count + " questions total.)");
                    m_BotBrain.sendDefaultChannelMessage("First question is : " + getCurrentQuestion().getFormattedTitle());

                    m_MessageTimeLast = m_BotBrain.ActionTimer.ElapsedMilliseconds;
                    m_LastLineCount = m_BotBrain.LineCount;
                    m_TimeSinceLastAnswer = m_BotBrain.ActionTimer.ElapsedMilliseconds;
                    m_IsActive = true;
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage("No questions found for these tags: " + tagListString);
                }
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage("No tags specified, Trivia not started.");
            }
		}

		public void stop(userEntry commandUser, string argumentString)
		{
			m_IsActive = false;
			m_BotBrain.sendDefaultChannelMessage("Trivia stopped.");
		}

        public void reload(userEntry commandUser, string argumentString)
        {
            m_IsActive = false;
            m_LoadSuccessful = load();

            if (m_LoadSuccessful)
                m_BotBrain.sendDefaultChannelMessage("Trivia stopped and reloaded.");
            else
                m_BotBrain.sendDefaultChannelMessage("Trivia stopped but load FAILED (?!).");
        }

        private bool isTagInQuestion(triviaQuestion aQuestion, string aTag)
        {
            foreach (string curTag in aQuestion.tags)
            {
                if (curTag.ToLower() == aTag.ToLower())
                {
                    return true;
                }
            }

            return false;
        }

        private List<triviaQuestion> getQuestionsForTags(List<string> aTags)
        {
            List<triviaQuestion> questionList = new List<triviaQuestion>();

            foreach (triviaCategory curCategory in m_Categories)
            {
                foreach (triviaQuestion curQuestion in curCategory.questions)
                {
                    foreach (string curTag in aTags)
                    {
                        if (isTagInQuestion(curQuestion, curTag))
                        {
                            questionList.Add(curQuestion);
                            break;
                        }
                    }
                }
            }

            return questionList;
        }

        private List<triviaParticipant> getTopScores(int aCount = 3)
        {
            List<triviaParticipant> listed = new List<triviaParticipant>();
            foreach(KeyValuePair<userEntry, int> entry in m_Scores)
            {
                listed.Add(new triviaParticipant(entry.Key, entry.Value));
            }

            listed.Sort(delegate (triviaParticipant a, triviaParticipant b)
            {
                if (a.score == b.score)
                    return 0;
                else
                    return b.score - a.score;
            });

            return listed.GetRange(0, Math.Min(aCount, listed.Count));
        }

        private string getTopScoreString(int aCount = 3)
        {
            string scoreString = "";

            List<triviaParticipant> topScores = getTopScores();
            List<string> topNames = new List<string>();

            int scoreIndex = 0;

            foreach (triviaParticipant topEntry in topScores)
            {
                scoreIndex++;
                topNames.Add(scoreIndex + ") " + topEntry.user.Nickname + ": " + topEntry.score);
            }

            scoreString = String.Join(", ", topNames.ToArray());

            return scoreString;
        }

        public void scores(userEntry commandUser, string argumentString)
        {
            string scoreString = getTopScoreString();

            m_BotBrain.sendDefaultChannelMessage("Top 3 Trivia Contestants: " + scoreString);
        }

        public void setMaxQuestions(userEntry commandUser, string argumentString)
        {
            int newMax;

            if (Int32.TryParse(argumentString, out newMax))
            {
                m_TotalQuestions = newMax;
                string nextString = "";

                if (m_IsActive)
                    nextString = "  (Will be applied next time Trivia starts)";

                m_BotBrain.sendDefaultChannelMessage("Max Questions set to : " + m_TotalQuestions + nextString);
            }
        }

        public void topics(userEntry commandUser, string argumentString)
        {
            string topicString = "";
            bool hasOne = false;

            foreach (triviaCategory curCategory in m_Categories)
            {
                if (hasOne)
                    topicString += ", ";

                topicString += curCategory.code + " (" +curCategory.name + ")";
                hasOne = true;
            }

            m_BotBrain.sendDefaultChannelMessage("Available Topics are: " + topicString);
        }

        public override void onUserMessage(userEntry aUser, string aMessage)
        {
            if (m_IsActive)
            {
                triviaQuestion currentQuestion = getCurrentQuestion();
                if (currentQuestion != null)
                {
                    foreach (string curAnswer in currentQuestion.answers)
                    {
                        if (curAnswer.ToLower() == aMessage.ToLower())
                        {
                            string addendumString = "";

                            if (!string.IsNullOrEmpty(currentQuestion.addendum))
                            {
                                addendumString = ". " + currentQuestion.addendum;
                            }

                            m_BotBrain.sendDefaultChannelMessage(aUser.Nickname + " has answered correctly with \"" + aMessage + "\"" + addendumString);
                            checkCreateParticipant(aUser);
                            m_Scores[aUser]++;
                            advanceToNextQuestion(true);
                            return;
                        }
                    }
                }
            }
        }

        public override void frame()
		{
			if (m_IsActive)
			{
                if (m_BotBrain.ActionTimer.ElapsedMilliseconds  > m_TimeSinceLastAnswer + m_TimeToAnswer)
                {
                    m_BotBrain.sendDefaultChannelMessage("Time's up!  No-one successfully answered!");
                    advanceToNextQuestion(true);
                }
                else
                {
                    bool minTimeReached = m_BotBrain.ActionTimer.ElapsedMilliseconds > m_MessageTimeLast + m_MessageThrottle;
                    bool maxTimeReached = m_BotBrain.ActionTimer.ElapsedMilliseconds > m_MessageTimeLast + m_MessageThrottleMax;
                    bool linesPassedReached = m_BotBrain.LineCount > m_LastLineCount + m_LineCountMinimum;

                    if (maxTimeReached || (minTimeReached && linesPassedReached))
                    {
                        string questionString = "";
                        triviaQuestion currentQuestion = getCurrentQuestion();
                        if (currentQuestion != null)
                            questionString = "Current question is: " + getCurrentQuestion().getFormattedTitle();

                        m_BotBrain.sendDefaultChannelMessage("A Trivia game's running!  " + questionString);

                        m_MessageTimeLast = m_BotBrain.ActionTimer.ElapsedMilliseconds;
                        m_LastLineCount = m_BotBrain.LineCount;
                    }
                }
			}
		}

        private void advanceToNextQuestion(bool fromSuccessOrFail = false)
        {
            m_CurrentQuestionIndex++;
            if (m_CurrentQuestionIndex >= m_Questions.Count)
            {
                m_CurrentQuestionIndex = 0;
                m_IsActive = false;

                if (fromSuccessOrFail)
                {
                    string scoreString = getTopScoreString();
                    m_BotBrain.sendDefaultChannelMessage("Trivia is over!  The top scorers are: " + scoreString);
                }
            }
            else
            {
                m_MessageTimeLast = m_BotBrain.ActionTimer.ElapsedMilliseconds;
                m_BotBrain.sendDefaultChannelMessage("Next Question: " + getCurrentQuestion().getFormattedTitle());
            }
            m_TimeSinceLastAnswer = m_BotBrain.ActionTimer.ElapsedMilliseconds;
        }

        private bool load()
        {
            string dirPath = System.IO.Path.Combine(jerpBot.storagePath, "trivia");
            m_Categories = new List<triviaCategory>();

            if (Directory.Exists(dirPath))
            {
                string[] soundFiles = Directory.GetFiles(dirPath);
                if (soundFiles.Length > 0)
                {
                    foreach (string filePath in soundFiles)
                    {
                        if (File.Exists(filePath))
                        {
                            string triviaFileString = File.ReadAllText(filePath);
                            if (!string.IsNullOrEmpty(triviaFileString))
                            {
                                m_Categories.Add(new JavaScriptSerializer().Deserialize<triviaCategory>(triviaFileString));
                            }
                        }
                    }

                    foreach (triviaCategory curCategory in m_Categories)
                    {
                        foreach (triviaQuestion curQuestion in curCategory.questions)
                        {
                            curQuestion.parentCategory = curCategory;
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        public trivia(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
            m_LoadSuccessful = load();
            m_Scores = new Dictionary<userEntry, int>();
            m_Questions = new List<triviaQuestion>();

            chatCommandDef tempDef = new chatCommandDef("trivia", null, true, true);
			tempDef.addSubCommand(new chatCommandDef("start", start, false, false));
			tempDef.addSubCommand(new chatCommandDef("stop", stop, true, false));
            tempDef.addSubCommand(new chatCommandDef("scores", scores, true, true));
            tempDef.addSubCommand(new chatCommandDef("topics", topics, true, true));
            tempDef.addSubCommand(new chatCommandDef("setmax", setMaxQuestions, true, true));
            tempDef.addSubCommand(new chatCommandDef("reload", reload, false, false));
            tempDef.UseGlobalCooldown = false;
			m_BotBrain.addChatCommand(tempDef);
		}

	}
}
