using CommandLine;

namespace CarGenTools
{
    public class ToolOptions
    {
        [Option('g', "game", HelpText = "Select the game to work with.", Default = Game.GTA3)]
        public Game Game { get; set; }

        [Option('o', "output", HelpText = "Set the output file path.")]
        public string OutputFile { get; set; }

        [Option('v', "verbose", HelpText = "Enable verbose output for hackers.")]
        public bool Verbose { get; set; }

#if DEBUG
        [Option('d', "debug", HelpText = "Pause until a debugger is attached.")]
#endif
        public bool Debug { get; set; }
    }
}
