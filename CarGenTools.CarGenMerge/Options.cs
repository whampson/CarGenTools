using CommandLine;
using System.Collections.Generic;

namespace CarGenTools.CarGenMerge
{
    public class Options : ToolOptions
    {
        [Value(0, Required = true, Hidden = true)]
        public string TargetFile { get; set; }

        [Value(1, Required = true, Hidden = true)]
        public IEnumerable<string> SourceFiles { get; set; }

        [Option('p', "priority-list", HelpText = PriorityListHelp)]
        public string PriorityFile { get; set; }

        [Option('r', "radius", HelpText = RadiusHelp, Default = 10.0f)]
        public float Radius { get; set; }

        [Option('t', "title", HelpText = TitleHelp)]
        public string Title { get; set; }

        private const string RadiusHelp = "Set the collision radius. If two car generators are found within the collision radius, the merge process will be aborted.";
        private const string TitleHelp = "Set the in-game title of the target savefile.";
        private const string PriorityListHelp =
            "A CSV file specifying the order in which to replace car generators. The columns are (priority,index) " +
            "where 'priority' represents the replacement order and 'index' specifies the index of a car generator " +
            "in the target save's car generator list. A priority of 0 is the highest priority. A negative priority " +
            "will exclude row from replacement. If multiple rows share the same priority, one of the rows will be " +
            "chosen at random and this process will repeat until all have been chosen exactly once. Lines beginning " +
            "with '#' are treated as comments and ignored. If no priority list is specified, car generators will be " +
            "replaced at random.";
    }
}
