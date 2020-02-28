using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace CarGenMerger
{
    public enum ExitCode
    {
        Success = 0,
        BadCommandLine = 1,
        BadIO = 2,
        Collision = 3,

        UnknownError = 127
    }

    public enum Mode
    {
        GTA3,
        VC
    }

    internal static class Program
    {
        private static ExitCode ExitStatus;

        internal static int Main(string[] args)
        {
            var parser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = true;
                with.HelpWriter = null;
            });

            ExitStatus = ExitCode.UnknownError;
            var result = parser.ParseArguments<Options>(args);
            result
                .WithParsed(options => Run(options))
                .WithNotParsed(errors => HandleCommandLineParseErrors(result, errors));

            return (int) ExitStatus;
        }

        private static void HandleCommandLineParseErrors<T>(ParserResult<T> result, IEnumerable<Error> errors)
        {
            if (errors.IsVersion())
            {
                Console.WriteLine(HelpText.AutoBuild(result));
                return;
            }
            else if (errors.IsHelp())
            {
                HelpText helpText = HelpText.AutoBuild(result, h =>
                {
                    h.AddEnumValuesToHelpText = true;
                    h.AdditionalNewLineAfterOption = false;
                    h.MaximumDisplayWidth = 100;
                    h.Heading = Strings.AppTitle;
                    h.Copyright += "\n";
                    h.AddPreOptionsLines(new[]
                    {
                        Strings.AppUsage, "",
                        Strings.AppDescription, ""
                    });

                    return h;
                }, e => e);

                Console.WriteLine(helpText);
                return;
            }

            foreach (Error e in errors)
            {
                string msg = "";
                switch (e.Tag)
                {
                    case ErrorType.BadFormatConversionError:
                        msg = string.Format(Strings.ErrorText_BadOptionValue, ((BadFormatConversionError) e).NameInfo.LongName);
                        break;
                    case ErrorType.MissingRequiredOptionError:
                        var eMissingRequired = (MissingRequiredOptionError) e;
                        msg = (eMissingRequired.NameInfo.Equals(NameInfo.EmptyName))
                            ? string.Format("{0}\n{1}: {2}", Strings.ErrorText_MissingRequiredPositional, Strings.AppUsagePrefix, Strings.AppUsage)
                            : string.Format(Strings.ErrorText_MissingRequiredOption, eMissingRequired.NameInfo.LongName);
                        break;
                    case ErrorType.MissingValueOptionError:
                        var eMissingValue = (MissingValueOptionError) e;
                        msg = string.Format(Strings.ErrorText_MissingOptionValue, eMissingValue.NameInfo.LongName);
                        break;
                    case ErrorType.RepeatedOptionError:
                        var eRepeated = (RepeatedOptionError) e;
                        msg = string.Format(Strings.ErrorText_RepeatedOption, eRepeated.NameInfo.NameText);
                        break;
                    case ErrorType.UnknownOptionError:
                        var eUnknown = (UnknownOptionError) e;
                        msg = string.Format(Strings.ErrorText_UnknownOption, eUnknown.Token);
                        break;
                    default:
                        msg = string.Format(Strings.ErrorText_UnknownError, e);
                        break;
                }

                Console.Error.WriteLine(msg);
            }

            ExitStatus = ExitCode.BadCommandLine;
        }

        private static void Run(Options o)
        {
            Logger.VerbosityEnabled = o.Verbose;
            Merger m = new Merger(o);

#if DEBUG
            if (o.Debug)
            {
                Logger.Info("Waiting for debugger...");
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(100);
                }
                Logger.Info("\n");
            }
#endif

            ExitStatus = m.Initialize();
            if (ExitStatus == ExitCode.Success)
            {
                ExitStatus = m.Merge();
            }
        }
    }
}
