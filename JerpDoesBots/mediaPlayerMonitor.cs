using System;
using System.IO;
using System.Web.Script.Serialization;
using System.Diagnostics;

namespace JerpDoesBots
{
    class mediaPlayerMonitor : botModule
	{
        private bool m_IsLoaded;
        private throttler m_Throttler;
        mediaPlayerMonitorConfig m_Config;
        private string m_LastTrackTitle = "";

        private bool loadConfig()
        {
            string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_mediamonitor.json");
            if (File.Exists(configPath))
            {
                string messageConfigString = File.ReadAllText(configPath);
                if (!string.IsNullOrEmpty(messageConfigString))
                {
                    m_Config = new JavaScriptSerializer().Deserialize<mediaPlayerMonitorConfig>(messageConfigString);
                    return true;
                }
            }
            return false;
        }

        public void reloadConfig(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (loadConfig())
            {
                if (!aSilent)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("mediaMonitorLoadSuccess"));
            }
            else
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("mediaMonitorLoadFail"));
        }

        public void currentSong(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (!string.IsNullOrEmpty(m_LastTrackTitle))
            {
                m_BotBrain.sendDefaultChannelMessage("The current track is: " + m_LastTrackTitle);
            }
        }

        public override void onFrame()
        {
            if (m_IsLoaded && m_Config.enabled)
            {
                if (m_Throttler.isReady)
                {
                    m_Throttler.trigger();

                    Process[] processList = Process.GetProcesses();

                    foreach (Process curProcess in processList)
                    {
                        if (!string.IsNullOrEmpty(curProcess.MainWindowTitle))
                        {
                            if (curProcess.MainWindowTitle.ToLower().Contains(m_Config.suffix.ToLower()))
                            {
                                if (curProcess.MainWindowTitle != m_Config.suffix)  // Anything actually playing
                                {
                                    string curTitle = curProcess.MainWindowTitle.Replace(" - " + m_Config.suffix, "");  // Remove suffix
                                    curTitle = curTitle.Substring(0, curTitle.LastIndexOf("."));    // Remove filename
                                    int bracketIndex = curTitle.LastIndexOf("[");

                                    if (bracketIndex >= 0)
                                        curTitle = curTitle.Substring(0, bracketIndex);    // Removes track IDs left by YT-DLP

                                    if (curTitle != m_LastTrackTitle)
                                    {
                                        // m_BotBrain.sendDefaultChannelAnnounce("Now Playing: " + curTitle);
                                        m_BotBrain.logGeneral.writeAndLog("Playing Media: " + curTitle);
                                        m_LastTrackTitle = curTitle;
                                    }
                                }

                                break;
                            }
                        }
                    }
                }
            }

        }

        public mediaPlayerMonitor(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
            m_Throttler = new throttler(aJerpBot);
            m_Throttler.requiresUserMessages = false;
            m_Throttler.messagesReduceTimer = false;
            m_Throttler.waitTimeMSMax = 3000;

            m_IsLoaded = loadConfig();

            if (m_IsLoaded)
            {


                chatCommandDef tempDef = new chatCommandDef("mediamonitor", null, true, true);
                tempDef.addSubCommand(new chatCommandDef("reload", reloadConfig, false, false));

                m_BotBrain.addChatCommand(tempDef);

                tempDef = new chatCommandDef("song", currentSong, true, true);
                m_BotBrain.addChatCommand(tempDef);
            }
        }
	}
}
