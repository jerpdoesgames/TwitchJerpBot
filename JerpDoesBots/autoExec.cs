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
        /// Reloads json configuration for autoExec module.
        /// </summary>
        /// <param name="commandUser">User attempting to trigger a reload.</param>
        /// <param name="argumentString">Unused</param>
        /// <param name="aSilent">Wehther to output on success/fail.</param>
        public void reloadConfig(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            m_IsLoaded = loadConfig();
            if (m_IsLoaded)
            {
                if (!aSilent)
                    jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("autoExecReloadSuccess"));
            }
            else
            {
                if (!aSilent)
                    jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("autoExecReloadFail"));
            }
        }

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
                            jerpBot.instance.messageOrCommand(curCommandString);
                        }
                    }
                }
            }
        }

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
                            jerpBot.instance.messageOrCommand(curCommandString);
                        }
                    }
                }
            }
        }

        public override void onStreamLive()
        {
            if (m_IsLoaded)
            {
                foreach (autoExecConfigEntry curEntry in m_Config.entries)
                {
                    if (curEntry.activateOnStreamLive && (curEntry.requirements == null || curEntry.requirements.isMet()))
                    {
                        foreach (string curCommandString in curEntry.commands)
                        {
                            jerpBot.instance.messageOrCommand(curCommandString);
                        }
                    }
                }
            }
        }

        public override void onStreamOffline()
        {
            if (m_IsLoaded)
            {
                foreach (autoExecConfigEntry curEntry in m_Config.entries)
                {
                    if (curEntry.activateOnStreamOffline && (curEntry.requirements == null || curEntry.requirements.isMet()))
                    {
                        foreach (string curCommandString in curEntry.commands)
                        {
                            jerpBot.instance.messageOrCommand(curCommandString);
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
                        if (curEntry.activateOnMessageTerm && (curEntry.requirements == null || curEntry.requirements.isMet()) && (curEntry.lastActivationTimeMS == -1 || jerpBot.instance.actionTimer.ElapsedMilliseconds >= curEntry.lastActivationTimeMS + (curEntry.cooldownTimeSeconds * 1000)))
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
                                        jerpBot.instance.messageOrCommand(curCommandString);
                                    }

                                    curEntry.lastActivationTimeMS = jerpBot.instance.actionTimer.ElapsedMilliseconds;
                                }
                            }
                        }
                    }
                }
            }
        }

        public override void onFrame()
        {
            if (m_IsLoaded && m_Throttler.isReady)
            {
                foreach (autoExecConfigEntry curEntry in m_Config.entries)
                {
                    if (curEntry.activateOnTimer && (curEntry.requirements == null || curEntry.requirements.isMet()) && jerpBot.instance.actionTimer.ElapsedMilliseconds >= curEntry.lastActivationTimeMS + (curEntry.cooldownTimeSeconds * 1000))
                    {
                        foreach(string curCommandString in curEntry.commands)
                        {
                            jerpBot.instance.messageOrCommand(curCommandString);
                        }

                        curEntry.lastActivationTimeMS = jerpBot.instance.actionTimer.ElapsedMilliseconds;
                    }
                }    

                m_Throttler.trigger();
            }
        }

        /// <summary>
        /// Automatic command executor - can output messages and commands in response to specific events and on specific intervals.
        /// </summary>
        public autoExec() : base(true, true, false)
        {
            m_IsLoaded = loadConfig();

            if (m_IsLoaded)
            {
                m_Throttler = new throttler();
                m_Throttler.waitTimeMSMax = 5000;
                m_Throttler.messagesReduceTimer = false;
                m_Throttler.requiresUserMessages = false;

                chatCommandDef tempDef = new chatCommandDef("autoexec", null, false, false);
                tempDef.addSubCommand(new chatCommandDef("reload", reloadConfig, false, false));
                jerpBot.instance.addChatCommand(tempDef);
            }
        }
    }
}
