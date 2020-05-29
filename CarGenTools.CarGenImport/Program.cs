namespace CarGenTools.CarGenImport
{
    internal class Program : ProgramBase
    {
        internal static int Main(string[] args)
        {
            ProgramTitle = "GTA Car Generator Import Tool";
            ProgramUsage = "cgimport [options] savefile cargenfile";
            ProgramDescription = "Imports car generators from a JSON file into a GTA3/VC savedata file.";
            ProgramCopyright = "(C) 2020 thehambone";

            Run<Import, Options>(args);
            return (int) RunResult;
        }
    }
}
