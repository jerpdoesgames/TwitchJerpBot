using System.IO;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
    internal class autoExec : botModule
    {
        private bool m_IsLoaded;
        private autoExecConfig m_Config;
        private throttler m_Throttler;

        /// <summary>
        /// Load configuration json for the autoExec system.
        /// </summary>
        /// <returns></returns>
        private bool loadConfig()
        {
            string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_autoexec.json");
            if (File.Exists(configPath))
            {
                string configFileString = File.ReadAllText(configPath);
                if (!string.IsNullOrEmpty(configFileString))
                {
                    m_Config = new JavaScriptSerializer().Deserialize<autoExecConfig>(configFileString);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Reloads autoexec 
        /// </summary>
        /// <param name="commandUser"></param>
        /// <param name="argumentString"></param>
        /// <param name="aSilent"></param>
        public void reloadConfig(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            m_IsLoaded = loadConfig();
            if (m_IsLoaded)
            {
                if (!aSilent)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("autoExecReloadSuccess"));
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("autoExecReloadFail"));
            }
        }

        /// <summary>
        /// Executes commands after changing categories.
        /// </summary>
        public override void onCategoryIDChanged()
        {
            if (m_IsLoaded)
            {
                foreach (autoExecConfigEntry curEntry in m_Config.entries)
                {
                    if (curEntry.activateOnCategoryChange && (curEntry.requirements == null || curEntry.requirements.isMet()))
                    {
                        foreach (string curCommandString in curEntry.commands)
                        {
                            m_BotBrain.messageOrCommand(curCommandString);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Executes commands after the bot is first loaded.
        /// </summary>
        public override void onBotFullyLoaded()
        {
            if (m_IsLoaded)
            {
                foreach (autoExecConfigEntry curEntry in m_Config.entries)
                {
                    if (curEntry.activateOnBotLoad && (curEntry.requirements == null || curEntry.requirements.isMet()))
                    {
                        foreach (string curCommandString in curEntry.commands)
                        {
                            m_BotBrain.messageOrCommand(curCommandString);
                        }
                    }
                }
            }
        }
        public override void onUserMessage(userEntry aUser, string aMessage)
        {
            if (m_IsLoaded)
            {
                if (aUser.Nickname.ToLower() != jerpBot.instance.botUsername.ToLower())
                {
                    foreach (autoExecConfigEntry curEntry in m_Config.entries)
                    {
                        if (curEntry.activateOnMessageTerm && (curEntry.requirements == null || curEntry.requirements.isMet()) && (curEntry.lastActivationTimeMS == -1 || m_BotBrain.actionTimer.ElapsedMilliseconds >= curEntry.lastActivationTimeMS + (curEntry.cooldownTimeSeconds * 1000)))
                        {
                            if (curEntry.messageTermsToCheck != null && curEntry.messageTermsToCheck.Count > 0)
                            {
                                bool termsValid = true;

                                foreach (string curTerm in curEntry.messageTermsToCheck)
                                {
                                    if (aMessage.ToLower().Contains(curTerm.ToLower()))
                                    {
                                        if (curEntry.messageTermsUseORCheck)
                                        {
                                            break;
                                        }
                                    }
                                    else if (!curEntry.messageTermsUseORCheck)
                                    {
                                        termsValid = false;
                                        break;
                                    }
                                }

                                if (termsValid)
                                {
                                    foreach (string curCommandString in curEntry.commands)
                                    {
                                        m_BotBrain.messageOrCommand(curCommandString);
                                    }

                                    curEntry.lastActivationTimeMS = m_BotBrain.actionTimer.ElapsedMilliseconds;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Executes commands periodically, checking a throttler every frame.
        /// </summary>
        public override void onFrame()
        {
            if (m_IsLoaded && m_Throttler.isReady)
            {
                foreach (autoExecConfigEntry curEntry in m_Config.entries)
                {
                    if (curEntry.activateOnTimer && (curEntry.requirements == null || curEntry.requirements.isMet()) && m_BotBrain.actionTimer.ElapsedMilliseconds >= curEntry.lastActivationTimeMS + (curEntry.cooldownTimeSeconds * 1000))
                    {
                        foreach(string curCommandString in curEntry.commands)
                        {
                            m_BotBrain.messageOrCommand(curCommandString);
                        }

                        curEntry.lastActivationTimeMS = m_BotBrain.actionTimer.ElapsedMilliseconds;
                    }
                }    

                m_Throttler.trigger();
            }
        }

        /// <summary>
        /// Initialize command entries for the automatic command executor.
        /// </summary>
        /// <param name="aJerpBot"></param>
        public autoExec(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
        {
            m_IsLoaded = loadConfig();

            if (m_IsLoaded)
            {
                m_Throttler = new throttler(aJerpBot);
                m_Throttler.waitTimeMSMax = 5000;
                m_Throttler.messagesReduceTimer = false;
                m_Throttler.requiresUserMessages = false;

                chatCommandDef tempDef = new chatCommandDef("autoexec", null, false, false);
                tempDef.addSubCommand(new chatCommandDef("reload", reloadConfig, false, false));
                m_BotBrain.addChatCommand(tempDef);
            }
        }
    }
}
