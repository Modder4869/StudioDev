using System;
using System.Diagnostics;
using System.Text;

namespace AssetStudio
{
    public static class Logger
    {
        private static bool _fileLogging;

        public static ILogger Default = new DummyLogger();
        public static ILogger File;

        public static bool Silent { get; set; }
        public static bool enableVerbose { get; set; } = false;
        public static LoggerEvent Flags { get; set; }

        public static bool FileLogging
        {
            get => _fileLogging;
            set
            {
                _fileLogging = value;
                if (_fileLogging)
                {
                    try
                    {
                        File = new FileLogger();
                    }
                    catch
                    {
                        _fileLogging = false;
                        Error("log file is already in use, disabling...");
                        return;
                    }
                }
                else
                {
                    ((FileLogger)File)?.Dispose();
                    File = null;
                }
            }
        }

        public static void Verbose(string message)
        {
            if ((Flags & LoggerEvent.Verbose) == 0 || Silent)
                return;

            try
            {
                var callerMethod = new StackTrace().GetFrame(1).GetMethod();
                var callerMethodClass = callerMethod.ReflectedType.Name;
                if (!string.IsNullOrEmpty(callerMethodClass))
                {
                    message = $"[{callerMethodClass}] {message}";
                }
            }
            catch (Exception) { }
            if (FileLogging) File.Log(LoggerEvent.Verbose, message);
            Default.Log(LoggerEvent.Verbose, message);
        }
        public static void Debug(string message)
        {
            if ((Flags & LoggerEvent.Debug) == 0 || Silent)
                return;

            if (FileLogging) File.Log(LoggerEvent.Debug, message);
            Default.Log(LoggerEvent.Debug, message);
        }
        public static void Info(string message)
        {
            if ((Flags & LoggerEvent.Info) == 0 || Silent)
                return;

            if (FileLogging) File.Log(LoggerEvent.Info, message);
            Default.Log(LoggerEvent.Info, message);
        }
        public static void Perf(string message)
        {
            if ((Flags & LoggerEvent.Debug) == 0 || Silent)
                return;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        public static void Warn(string message)
        {
            if ((Flags & LoggerEvent.Warning) == 0 || Silent)
                return;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        public static void Warning(string message)
        {
            if ((Flags & LoggerEvent.Warning) == 0 || Silent)
                return;

            if (FileLogging) File.Log(LoggerEvent.Warning, message);
            Default.Log(LoggerEvent.Warning, message);
        }
        public static void Error(string message)
        {
            if ((Flags & LoggerEvent.Error) == 0 || Silent)
                return;
            Console.ForegroundColor = ConsoleColor.Red;
            if (FileLogging) File.Log(LoggerEvent.Error, message);
            Default.Log(LoggerEvent.Error, message);
            Console.ResetColor();
        }

        public static void Error(string message, Exception e)
        {
            if ((Flags & LoggerEvent.Error) == 0 || Silent)
                return;

            var sb = new StringBuilder();
            sb.AppendLine(message);
            sb.AppendLine(e.ToString());

            message = sb.ToString();
            if (FileLogging) File.Log(LoggerEvent.Error, message);
            Default.Log(LoggerEvent.Error, message);
        }
    }
}
