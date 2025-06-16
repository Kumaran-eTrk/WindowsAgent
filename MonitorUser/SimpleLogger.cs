using System;
using System.IO;

namespace MontiorUserStandlone
{

    public class SimpleLogger
    {
        private readonly string _logFilePath;

        public SimpleLogger(string logFilePath)
        {
            _logFilePath = logFilePath;
        }

        public void Log(string message)
        {
            string logMessage = $"{DateTime.Now}: {message}";
            File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
        }
    }

}
