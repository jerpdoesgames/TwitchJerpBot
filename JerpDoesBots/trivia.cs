﻿using System;
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
        private throttler m_Throttler;
        private List<triviaCategory> m_Categories { get; set; }
        private Dictionary<userEntry, int> m_Scores;
        private long m_TimeToAnswer = 450000;
        private long m_TimeSinceLastAnswer = 0;
        private bool m_LoadSuccessful = false;

		private bool m_IsActive = false;
        private int m_TotalQuestions = 15;
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
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("triviaQuestionCurrent"), getCurrentQuestion().getFormattedTitle()));
        }

        public void start(userEntry commandUser, string argumentString)
		{
            if (!string.IsNullOrEmpty(argumentString))
            {
                List<string> tagList = new List<string>();

                if (argumentString == "all")
                {
                    foreach (triviaCategory curCategory in m_Categories)
                    {
                        tagList.Add(curCategory.code);
                    }
                }
                else
                {
                    tagList = argumentString.Split(' ').ToList();
                }
                

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


                    m_BotBrain.sendDefaultChannelAnnounce(string.Format(m_BotBrain.localizer.getString("triviaStart"), tagListString, m_Questions.Count));
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("triviaQuestionFirst"), getCurrentQuestion().getFormattedTitle()));

                    m_Throttler.trigger();
                    m_TimeSinceLastAnswer = m_BotBrain.actionTimer.ElapsedMilliseconds;
                    m_IsActive = true;
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("triviaNoQuestionsFound"), tagListString));
                }
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("triviaStartNoTags"));
            }
		}

		public void stop(userEntry commandUser, string argumentString)
		{
			m_IsActive = false;
			m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("triviaStopped"));
		}

        public void reload(userEntry commandUser, string argumentString)
        {
            m_IsActive = false;
            m_LoadSuccessful = load();

            if (m_LoadSuccessful)
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("triviaStoppedReloaded"));
            else
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("triviaStoppedLoadFail"));
        }

        private bool isTagInQuestion(triviaQuestion aQuestion, string aTag)
        {
            foreach (string curTag in aQuestion.tags)
            {
                if (m_BotBrain.stripPunctuation(curTag.ToLower(), true) == m_BotBrain.stripPunctuation(aTag.ToLower(), true))
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

            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("triviaTopContestants"), scoreString));
        }

        public void setMaxQuestions(userEntry commandUser, string argumentString)
        {
            int newMax;

            if (Int32.TryParse(argumentString, out newMax))
            {
                m_TotalQuestions = newMax;
                string nextString = "";

                if (m_IsActive)
                    nextString = "  " + m_BotBrain.localizer.getString("triviaMaxQuestionsSetWhileActive");

                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("triviaMaxQuestionsSet"), m_TotalQuestions) + nextString);
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

            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("triviaTopics"), m_TotalQuestions) + topicString);
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
                        if (m_BotBrain.stripPunctuation(curAnswer.ToLower(), true) == m_BotBrain.stripPunctuation(aMessage.ToLower(), true))
                        {
                            string addendumString = "";

                            if (!string.IsNullOrEmpty(currentQuestion.addendum))
                            {
                                addendumString = "  " + currentQuestion.addendum;
                            }

                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("triviaAnswerSuccess"), aUser.Nickname, aMessage) + addendumString);

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
                if (m_BotBrain.actionTimer.ElapsedMilliseconds  > m_TimeSinceLastAnswer + m_TimeToAnswer)
                {
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("triviaTimeExpired"));
                    advanceToNextQuestion(true);
                }
                else if (m_Throttler.isReady)
                {
                    m_Throttler.trigger();
                    string questionString = "";
                    triviaQuestion currentQuestion = getCurrentQuestion();
                    if (currentQuestion != null)
                        questionString = string.Format(m_BotBrain.localizer.getString("triviaQuestionCurrent"), getCurrentQuestion().getFormattedTitle());

                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("triviaTimeExpired") + "  " + questionString);
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
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("triviaQuestionCurrent"), scoreString));
                }
            }
            else
            {
                m_Throttler.trigger();
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("triviaQuestionNext"), getCurrentQuestion().getFormattedTitle()));
            }
            m_TimeSinceLastAnswer = m_BotBrain.actionTimer.ElapsedMilliseconds;
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
            m_Throttler = new throttler(aJerpBot);
            m_Throttler.waitTimeMax = 120000;
            m_Throttler.lineCountReductionMax = 15;
            m_Throttler.lineCountReduction = 4000;

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
            tempDef.useGlobalCooldown = false;
			m_BotBrain.addChatCommand(tempDef);
		}

	}
}
