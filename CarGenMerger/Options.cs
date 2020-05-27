using CommandLine;
using GTASaveData;
using System.Collections.Generic;

namespace CarGenMerger
{
    public class Options
    {
        [Value(0, Required = true, Hidden = true)]
        public string TargetFile { get; set; }

        [Value(1, Required = true, Hidden = true)]
        public IEnumerable<string> SourceFiles { get; set; }

        [Option('m', "mode", HelpText = "HelpText_Mode", ResourceType = typeof(Strings), Default = Mode.GTA3)]
        public Mode Mode { get; set; }

        [Option('o', "output", HelpText = "HelpText_Output", ResourceType = typeof(Strings))]
        public string OutputFile { get; set; }

        [Option('p', "priority-list", HelpText = "HelpText_PriorityList", ResourceType = typeof(Strings))]
        public string PriorityFile { get; set; }

        [Option('r', "radius", HelpText = "HelpText_Radius", ResourceType = typeof(Strings), Default = 10.0f)]
        public float CollisionRadius { get; set; }

        [Option('t', "title", HelpText = "HelpText_Title", ResourceType = typeof(Strings))]
        public string OutputTitle { get; set; }

        [Option('v', "verbose", HelpText = "HelpText_Verbose", ResourceType = typeof(Strings))]
        public bool Verbose { get; set; }

#if DEBUG
        [Option('d', "debug", HelpText = "Pause until a debugger is attached.")]
        public bool Debug { get; set; }
#endif
    }
}
