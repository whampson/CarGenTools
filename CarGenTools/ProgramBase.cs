using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace CarGenTools
{
    public class ProgramBase
    {
        public static string ProgramUsage { get; protected set; }
        public static string ProgramTitle { get; protected set; }
        public static string ProgramDescription { get; protected set; }
        public static string ProgramCopyright { get; protected set; }
        public static ExitCode RunResult { get; protected set; }

        public static void Run<T, O>(string[] args)
            where T : Tool<O>
            where O : ToolOptions
        {
            RunResult = ExitCode.UnknownError;
            Parser parser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = true;
                with.HelpWriter = null;
            });

            ParserResult<O> result = parser.ParseArguments<O>(args);
            result
                .WithParsed(options => RunTool<T, O>(options))
                .WithNotParsed(errors => HandleParseErrors(result, errors));
        }

        public static void RunTool<T, O>(O options)
            where T : Tool<O>
            where O : ToolOptions
        {
            Log.EnableVerbosity = options.Verbose;
            WaitForDebuggerAttach(options.Debug);

            T tool = (T) Activator.CreateInstance(typeof(T), options);
            tool.Run();
            RunResult = tool.Result;
        }

        private static void HandleParseErrors<T>(ParserResult<T> result, IEnumerable<Error> errors)
        {
            if (errors.IsVersion())
            {
                Log.Info(HelpText.AutoBuild(result));
                return;
            }
            else if (errors.IsHelp())
            {
                HelpText helpText = HelpText.AutoBuild(result, h =>
                {
                    h.AddEnumValuesToHelpText = true;
                    h.AdditionalNewLineAfterOption = false;
                    h.MaximumDisplayWidth = 100;
                    h.Heading = ProgramTitle;
                    if (!string.IsNullOrEmpty(ProgramCopyright)) h.Copyright = ProgramCopyright;
                    h.Copyright += "\n";
                    h.AddPreOptionsLines(new[]
                    {
                        ProgramUsage, "",
                        ProgramDescription, ""
                    });

                    return h;
                }, e => e);

                Log.Info(helpText);
                return;
            }

            foreach (Error e in errors)
            {
                string msg = "";
                switch (e.Tag)
                {
                    case ErrorType.BadFormatConversionError:
                        msg = string.Format(ParseErrorBadOptionValue, ((BadFormatConversionError) e).NameInfo.LongName);
                        break;
                    case ErrorType.MissingRequiredOptionError:
                        var eMissingRequired = (MissingRequiredOptionError) e;
                        msg = (eMissingRequired.NameInfo.Equals(NameInfo.EmptyName))
                            ? string.Format("{0}\nUsage: {1}", ParseErrorMissingRequiredPositional, ProgramUsage)
                            : string.Format(ParseErrorMissingRequiredOption, eMissingRequired.NameInfo.LongName);
                        break;
                    case ErrorType.MissingValueOptionError:
                        var eMissingValue = (MissingValueOptionError) e;
                        msg = string.Format(ParseErrorMissingOptionValue, eMissingValue.NameInfo.LongName);
                        break;
                    case ErrorType.RepeatedOptionError:
                        var eRepeated = (RepeatedOptionError) e;
                        msg = string.Format(ParseErrorRepeatedOption, eRepeated.NameInfo.NameText);
                        break;
                    case ErrorType.UnknownOptionError:
                        var eUnknown = (UnknownOptionError) e;
                        msg = string.Format(ParseErrorUnknownOption, eUnknown.Token);
                        break;
                    default:
                        msg = string.Format(ParseErrorOops, e);
                        break;
                }
                Log.Error(msg);
            }
            RunResult = ExitCode.BadCommandLine;
        }

        [Conditional("DEBUG")]
        private static void WaitForDebuggerAttach(bool spin)
        {
            if (spin)
            {
                Log.Info("Waiting for debugger...");
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(100);
                }
            }
            
        }

        private const string ParseErrorBadOptionValue = "Bad value for '{0}'.";
        private const string ParseErrorMissingOptionValue = "Missing value for '{0}'.";
        private const string ParseErrorMissingRequiredOption = "Missing required option '{0}'.";
        private const string ParseErrorMissingRequiredPositional = "Missing required argument.";
        private const string ParseErrorRepeatedOption = "Please provide one value for '{0}'.";
        private const string ParseErrorUnknownOption = "Unknown option '{0}'.";
        private const string ParseErrorOops = "An unknown error occurred while parsing command-line options. ({0})";
    }
}
