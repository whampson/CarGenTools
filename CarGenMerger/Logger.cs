using System;
using System.IO;

namespace CarGenMerger
{
    public static class Logger
    {
        static Logger()
        {
            AutomaticNewline = false;
            VerbosityEnabled = false;
            InfoStream = Console.Out;
            ErrorStream = Console.Error;
        }

        public static bool AutomaticNewline { get; set; }
        public static bool VerbosityEnabled { get; set; }
        public static TextWriter InfoStream { get; set; }
        public static TextWriter ErrorStream { get; set; } 

        public static void Info(object value)
        {
            if (AutomaticNewline)
            {
                InfoStream.WriteLine(value);
            }
            else
            {
                InfoStream.Write(value);
            }
        }

        public static void Info(string format, params object[] args)
        {
            if (AutomaticNewline)
            {
                InfoStream.WriteLine(format, args);
            }
            else
            {
                InfoStream.Write(format, args);
            }
        }

        public static void Error(object value)
        {
            if (AutomaticNewline)
            {
                ErrorStream.WriteLine(value);
            }
            else
            {
                ErrorStream.Write(value);
            }
        }

        public static void Error(string format, params object[] args)
        {
            if (AutomaticNewline)
            {
                ErrorStream.WriteLine(format, args);
            }
            else
            {
                ErrorStream.Write(format, args);
            }
        }

        public static void InfoVerbose(object value)
        {
            if (VerbosityEnabled)
            {
                Info(value);
            }
        }

        public static void InfoVerbose(string format, params object[] args)
        {
            if (VerbosityEnabled)
            {
                Info(format, args);
            }
        }
    }
}
