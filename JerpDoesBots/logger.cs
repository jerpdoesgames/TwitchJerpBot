using System;
using System.IO;

namespace JerpDoesBots
{
	public class logger
	{
		private StreamWriter logFile;
		private string m_logName;
		private string m_filePath;

		/// <summary>
		/// Simple name for this log used for naming the log file and for identifying it in log entries.
		/// </summary>
		public string logName { get { return m_logName; } }

		/// <summary>
		/// Add a new entry to the log.
		/// </summary>
		/// <param name="toWrite">Text to write to the log.</param>
		public void write(string toWrite)
		{
			logFile.WriteLine(toWrite);
			logFile.Flush();
		}

		/// <summary>
		/// Add a new entry to the log and output that same text to the console.
		/// </summary>
		/// <param name="toWrite">Text to write to the log and output to the console.</param>
		public void writeAndLog(string toWrite)
		{
			toWrite = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + " | " + toWrite;

			write(toWrite);
			Console.WriteLine(toWrite);
		}

		/// <summary>
		/// Open the log for writing (create or append, depending on whether the file already exists).
		/// </summary>
		public void initialize()
		{
            if (!File.Exists(m_filePath))
            {
                logFile = File.CreateText(m_filePath);
            }
            else
            {
                logFile = File.AppendText(m_filePath);
            }

            this.write("");
            this.write("============ Initializing Log at " + DateTime.Now.ToString() + " : " + m_logName + " ============");
            this.write("");
        }

		public logger(string aName, bool aFilenameDateSuffix = true, bool aIncludeDayInFilename = false)
		{
			string filenameSuffix = "";

			DateTime curDateTime = DateTime.Now;

			if (aFilenameDateSuffix)
            {
				filenameSuffix += "_" + curDateTime.ToString("yyyy'-'MM");

				if (aIncludeDayInFilename)
                {
					filenameSuffix += curDateTime.ToString("'-'dd");
                }
            }

			m_filePath =  System.IO.Path.Combine(jerpBot.storagePath, "logs", aName + filenameSuffix + ".txt");
			m_logName = aName;
			initialize();
		}
	}
}
