using System;
using System.IO;

namespace JerpDoesBots
{
	public class logger
	{
		private StreamWriter logFile;

		public void write(string toWrite)
		{
			logFile.WriteLine(toWrite);
			logFile.Flush();
		}

		public void writeAndLog(string toWrite)
		{
			toWrite = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + " | " + toWrite;

			write(toWrite);
			Console.WriteLine(toWrite);
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

			string logFilePath =  System.IO.Path.Combine(jerpBot.storagePath, "logs", aName + filenameSuffix + ".txt");
			if (!File.Exists(logFilePath))
			{
				logFile = File.CreateText(logFilePath);
			}
			else
			{
				logFile = File.AppendText(logFilePath);
			}

			this.write("");
			this.write("============ Initializing Log at " + DateTime.Now.ToString() + " : " + aName + " ============");
			this.write("");
		}
	}
}
