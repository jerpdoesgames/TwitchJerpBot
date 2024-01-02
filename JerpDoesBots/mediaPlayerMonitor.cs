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
                    jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("mediaMonitorLoadSuccess"));
            }
            else
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("mediaMonitorLoadFail"));
        }

        public void currentSong(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (!string.IsNullOrEmpty(m_LastTrackTitle))
            {
                jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("mediaMonitorCurrentTrack"), m_LastTrackTitle));
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
                                        // jerpBot.instance.sendDefaultChannelAnnounce("Now Playing: " + curTitle);
                                        jerpBot.instance.logGeneral.writeAndLog("Playing Media: " + curTitle);
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

        public mediaPlayerMonitor() : base(true, true, false)
		{
            m_Throttler = new throttler();
            m_Throttler.requiresUserMessages = false;
            m_Throttler.messagesReduceTimer = false;
            m_Throttler.waitTimeMSMax = 3000;

            m_IsLoaded = loadConfig();

            if (m_IsLoaded)
            {


                chatCommandDef tempDef = new chatCommandDef("mediamonitor", null, true, true);
                tempDef.addSubCommand(new chatCommandDef("reload", reloadConfig, false, false));

                jerpBot.instance.addChatCommand(tempDef);

                tempDef = new chatCommandDef("song", currentSong, true, true);
                jerpBot.instance.addChatCommand(tempDef);
            }
        }
	}
}
