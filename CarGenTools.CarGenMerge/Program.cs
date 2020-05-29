namespace CarGenTools.CarGenMerge
{
    internal class Program : ProgramBase
    {
        internal static int Main(string[] args)
        {
            ProgramTitle = "GTA Car Generator Merge Tool";
            ProgramUsage = "cgmerge [options] target source...";
            ProgramDescription =
                "Combines car generators from one or more GTA3/VC savedata files into a single savedata file. " +
                "Merging occurs by first comparing the car generators from each source file against the car " +
                "generators in the target file slot-by-slot, then replacing the differing car generators in the " +
                "target file with car generators from the source files. Car generators with the Model set to 0 " +
                "are ignored.";
            ProgramCopyright = "(C) 2020 thehambone";

            Run<Merge, Options>(args);
            return (int) RunResult;
        }
    }
}
