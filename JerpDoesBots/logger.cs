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
			write(toWrite);
			Console.WriteLine(toWrite);
		}

		public logger(string filename)
		{
			string logFilePath =  System.IO.Path.Combine(jerpBot.storagePath, filename);
			if (!File.Exists(logFilePath))
			{
				logFile = File.CreateText(logFilePath);
			}
			else
			{
				logFile = File.AppendText(logFilePath);
			}
			this.write("");
			this.write("============ Initializing Log for " + DateTime.Now.ToString() + " ============");
			this.write("");
		}

		public void flush()	// In case we need the log while things are running.
		{
			logFile.Flush();
		}

		~logger()
		{
			// logFile.Flush();
			// logFile.Close();
		}
	}
}
