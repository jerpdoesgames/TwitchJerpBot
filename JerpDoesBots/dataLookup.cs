using System;
using System.IO;
using System.Web.Script.Serialization;
using System.Diagnostics;
using System.Collections.Generic;

namespace JerpDoesBots
{
    class dataLookup : botModule
	{
        private bool m_IsLoaded;
        dataLookupConfig m_Config;

        private bool loadConfig()
        {
            m_Config = new dataLookupConfig();

            string dirPath = System.IO.Path.Combine(jerpBot.storagePath, "datalookup");

            if (Directory.Exists(dirPath))
            {
                string[] catalogFiles = Directory.GetFiles(dirPath);
                if (catalogFiles.Length > 0)
                {
                    foreach (string filePath in catalogFiles)
                    {
                        if (File.Exists(filePath))
                        {
                            string catalogFileString = File.ReadAllText(filePath);
                            {
                                dataLookupConfigCatalog newCategory = new JavaScriptSerializer().Deserialize<dataLookupConfigCatalog>(catalogFileString);
                                m_Config.entries[newCategory.code] = newCategory;
                            }
                        }
                    }

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
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("dataLookupLoadSuccess"));
            }
            else
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("dataLookupLoadFail"));
        }


        private string getEntryNumeric(dataLookupConfigCatalog aCatalog, string aQueryString)
        {
            string output = "";

            int queryValue;
            if (int.TryParse(aQueryString, out queryValue))
            {
                for (int valueIndex = aCatalog.numericEntries.Count - 1; valueIndex >= 0; valueIndex--)
                {
                    int valueAtIndex = (int)aCatalog.numericEntries[valueIndex];

                    if (valueIndex == 0 && queryValue < valueAtIndex)  // Below the mininum value
                    {
                        return string.Format(aCatalog.outputStringBelow, valueIndex + aCatalog.numericIndexOffset, aCatalog.numericEntries[valueIndex]);

                    }
                    else if (queryValue == valueAtIndex)    // Exact match
                    {
                        return string.Format(aCatalog.outputStringMatch, valueIndex + aCatalog.numericIndexOffset, aCatalog.numericEntries[valueIndex]);
                    }
                    else if (queryValue > valueAtIndex) // Above some value
                    {
                        if (valueIndex == aCatalog.numericEntries.Count - 1)    // Higher than the max value?
                        {
                            return string.Format(aCatalog.outputStringAbove, valueIndex + aCatalog.numericIndexOffset, aCatalog.numericEntries[valueIndex]);
                        }
                        else // Between two values
                        {
                            return string.Format(aCatalog.outputStringBetween, valueIndex + aCatalog.numericIndexOffset, aCatalog.numericEntries[valueIndex], valueIndex + 1 + aCatalog.numericIndexOffset, aCatalog.numericEntries[valueIndex + 1]);
                        }
                    }
                }
            }
            else
            {
                output = string.Format(m_BotBrain.localizer.getString("dataLookupSearchInvalidQueryNumeric"));
            }

            return output;
        }

        public string getEntry(dataLookupConfigCatalog aCatalog, string aQuery)
        {
            string output = "";

            if (aCatalog.isNumeric)
            {
                return getEntryNumeric(aCatalog, aQuery);
            }
            else
            {
                if (aCatalog.entries.ContainsKey(aQuery))
                {
                    output = string.Format(aCatalog.outputStringMatch, aCatalog.entries[aQuery]);
                }
                else
                {
                    output = string.Format(m_BotBrain.localizer.getString("dataLookupSearchInvalidQueryNotFound"));
                }
            }

            return output;
        }

    public void searchForEntry(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (m_IsLoaded)
            {
                if (!string.IsNullOrEmpty(argumentString))
                {
                    string[] argumentList = argumentString.Split(new[] { ' ' }, 2);
                    if (argumentList.Length == 2)
                    {
                        string catalogName = argumentList[0];
                        
                        if (m_Config.entries.ContainsKey(catalogName))
                        {
                            string catalogKey = argumentList[1];
                            dataLookupConfigCatalog useCatalog = m_Config.entries[catalogName];
                            string output = getEntry(useCatalog, catalogKey);
                            m_BotBrain.sendDefaultChannelMessage(output);
                        }
                    }
                }
            }
        }

        public dataLookup(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
            m_IsLoaded = loadConfig();

            if (m_IsLoaded)
            {
                chatCommandDef tempDef = new chatCommandDef("lookup", searchForEntry, true, true);
                tempDef.addSubCommand(new chatCommandDef("reload", reloadConfig, false, false));

                m_BotBrain.addChatCommand(tempDef);
            }
        }
	}
}
