using CommandLine;

namespace CarGenTools.CarGenImport
{
    public class Options : ToolOptions
    {
        [Value(0, Required = true, Hidden = true)]
        public string SaveDataFile { get; set; }

        [Value(1, Required = true, Hidden = true)]
        public string CarGenFile { get; set; }

        [Option('r', "replace", HelpText = "Replace the entire car generator pool instead of individual items. Note: this will overwrite all car generators.")]
        public bool Replace { get; set; }

        [Option('t', "title", HelpText = "Set the in-game title of the savefile.")]
        public string Title { get; set; }

        [Option('x', "export", HelpText = "Export car generators instead of importing. Ignores -r and -t.")]
        public bool Export { get; set; }
    }
}
