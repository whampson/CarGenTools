using GTASaveData;
using GTASaveData.Common;
using GTASaveData.Common.Blocks;
using GTASaveData.GTA3;
using GTASaveData.VC;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using GTA3CarGenerator = GTASaveData.GTA3.CarGenerator;
using GTA3CarGeneratorBlock = GTASaveData.GTA3.Blocks.CarGeneratorBlock;
using VCCarGenerator = GTASaveData.VC.CarGenerator;
using VCCarGeneratorBlock = GTASaveData.VC.Blocks.CarGeneratorBlock;

namespace CarGenMerger
{
    public class Merger
    {
        private readonly Options m_opts;
        private readonly bool m_setTitle;
        private readonly bool m_userProvidedPriorityMap;
        private readonly Dictionary<int, List<int>> m_priorityMap;
        private readonly List<ICarGeneratorBlock> m_sourceCarGenBlocks;
        private readonly Random m_rand;
        
        private ICarGeneratorBlock m_targetCarGenBlock;
        private GrandTheftAutoSave m_targetSave;

        private static readonly Dictionary<Mode, int> CarGeneratorMaxCapacity = new Dictionary<Mode, int>()
        {
            { Mode.GTA3, GTA3CarGeneratorBlock.Limits.CarGeneratorsCapacity },
            { Mode.VC, VCCarGeneratorBlock.Limits.CarGeneratorsCapacity },
        };
        private int MaxCapacity => CarGeneratorMaxCapacity[m_opts.Mode];

        public Merger(Options o)
        {
            m_opts = o;
            m_setTitle = !string.IsNullOrEmpty(o.OutputTitle);
            m_userProvidedPriorityMap = !string.IsNullOrEmpty(o.PriorityFile);
            m_priorityMap = new Dictionary<int, List<int>>();
            m_sourceCarGenBlocks = new List<ICarGeneratorBlock>();
            m_rand = new Random(Guid.NewGuid().GetHashCode());
        }

        public ExitCode Initialize()
        {
            Logger.InfoVerbose(">> Initializing...\n");
            if (string.IsNullOrEmpty(m_opts.OutputFile))
            {
                m_opts.OutputFile = m_opts.TargetFile;
            }
            PrintLaunchOptions();

            if (!TryOpenSaveData(m_opts.TargetFile, out m_targetSave))
            {
                return ExitCode.BadIO;
            }
            m_targetCarGenBlock = (m_targetSave as IGrandTheftAutoSave).CarGenerators;

            foreach (string path in m_opts.SourceFiles)
            {
                if (!TryOpenSaveData(path, out GrandTheftAutoSave src))
                {
                    return ExitCode.BadIO;
                }
                m_sourceCarGenBlocks.Add((src as IGrandTheftAutoSave).CarGenerators);
            }

            if (m_userProvidedPriorityMap)
            {
                if (!LoadPriorityMap())
                {
                    return ExitCode.BadIO;
                }
            }

            return ExitCode.Success;
        }

        public ExitCode Merge()
        {
            List<ICarGenerator> differingCarGens = new List<ICarGenerator>();
            CarGeneratorComparer cgComparer = new CarGeneratorComparer();

            Logger.InfoVerbose(">> Counting number of differing car generators...\n");
            foreach (ICarGeneratorBlock cgBlock in m_sourceCarGenBlocks)
            {
                for (int i = 0; i < MaxCapacity; i++)
                {
                    ICarGenerator tgt = m_targetCarGenBlock.ParkedCars.ElementAt(i);
                    ICarGenerator src = cgBlock.ParkedCars.ElementAt(i);
                    if (src.Model != 0 && !cgComparer.Equals(src, tgt))
                    {
                        differingCarGens.Add(src);
                    }
                }
            }

            int numDiffering = differingCarGens.Count;
            Logger.Info("Found {0} differing car generators.\n", numDiffering, Pluralize(numDiffering));

            if (!m_userProvidedPriorityMap)
            {
                GeneratePriorityMap();
            }

            // Reduce the priority map to a list of car generator indices.
            // First, sort the map in ascending order by key (priority) and exclude negatives.
            // Next, flatten all values (list of car generator indices) into a single list of
            // integers, randomizing each sub-list along the way.
            List<int> replacementOrder = m_priorityMap
                .OrderBy(pair => pair.Key).Where(pair => pair.Key > -1)
                .SelectMany(pair => pair.Value.OrderBy(i => m_rand.Next()))
                .ToList();

            Logger.InfoVerbose(">> Merging car generators...\n");
            int numReplaced = 0;
            foreach (int cgIndex in replacementOrder)
            {
                if (numReplaced >= numDiffering)
                {
                    break;
                }

                ICarGenerator replacement = differingCarGens[numReplaced++];
                
                if (CheckForCollision(replacement, out int colIdx, out double colDist))
                {
                    Logger.Error("Collision found at <{0:0.###},{1:0.###},{2:0.###}>! (index = {3}, distance = {4:0.###})\n",
                        replacement.Position.X, replacement.Position.Y, replacement.Position.Z,
                        colIdx, colDist);
                    return ExitCode.Collision;
                }

                m_targetCarGenBlock.SetParkedCar(cgIndex, replacement);
                Logger.InfoVerbose("Replaced car generator {0} with model {1}.\n", cgIndex, replacement.Model);
            }
            Logger.Info("{0} car generator{1} replaced.\n", numReplaced, Pluralize(numReplaced));


            Logger.InfoVerbose(">> Setting number of car generators to {0}...\n", MaxCapacity);
            m_targetCarGenBlock.NumberOfCarGenerators = MaxCapacity;
            m_targetCarGenBlock.NumberOfActiveCarGenerators = MaxCapacity;

            if (m_setTitle)
            {
                Logger.InfoVerbose(">> Setting save title...\n");
                (m_targetSave as IGrandTheftAutoSave).SimpleVars.SaveName = m_opts.OutputTitle;
            }

            try
            {
                Logger.InfoVerbose($">> Writing {m_opts.OutputFile}...");
                m_targetSave.Save(m_opts.OutputFile);
                Logger.InfoVerbose(" success!\n");
            }
            catch (IOException e)
            {
                Logger.InfoVerbose(" failed!\n");
                Logger.Error($"{e.GetType().Name}: {e.Message}");
                return ExitCode.BadIO;
            }

            //// Create random ordering
            //Random rand = new Random();
            //List<int> ordering = new List<int>();

            //for (int i = 0; i < differingCarGens.Count; i++)
            //{
            //    int cgIndex = rand.Next(0, maxCapacity);
            //    ordering.Add(cgIndex);

            //    // TODO: linq
            //    for (int k = 0; k < maxCapacity; k++)
            //    {
            //        ICarGenerator tgt = targetCarGenBlock.ParkedCars[k] as ICarGenerator;
            //        if (tgt.Position.DistanceTo(differingCarGens[i].Position) < m_opts.Radius)
            //        {
            //            // TODO: check for collisions between differing car gens too
            //            Log("Collision found! (tgt = {0}; src.Pos = <{1:0.###},{2:0.###},{3:0.###}>; tgt.Pos = <{4:0.###},{5:0.###},{6:0.###}>)",
            //                k,
            //                differingCarGens[i].Position.X,
            //                differingCarGens[i].Position.Y,
            //                differingCarGens[i].Position.Z,
            //                tgt.Position.X,
            //                tgt.Position.Y,
            //                tgt.Position.Z);
            //        }
            //    }
            //}

            //// Replace!
            //int diffIndex = 0;
            //foreach (int tgtIndex in ordering)
            //{
            //    ICarGenerator replacement = differingCarGens[diffIndex++];

            //    targetCarGenBlock.ParkedCars[tgtIndex] = replacement;
            //    LogVerbose("Replaced car generator {0} with model {1}.", tgtIndex, replacement.Model);
            //}
            //Log("Replaced {0} car generators.", differingCarGens.Count);

            //targetCarGenBlock.NumberOfCarGenerators = maxCapacity;
            //targetCarGenBlock.NumberOfActiveCarGenerators = maxCapacity;

            //if (!string.IsNullOrEmpty(m_opts.Title))
            //{
            //    LogAction("Setting title to {0}", m_opts.Title);
            //    (target as IGrandTheftAutoSave).SimpleVars.SaveName = m_opts.Title;
            //}

            //target.Save(m_opts.OutputFile);
            //Log("Saved file: {0}", m_opts.OutputFile);

            return ExitCode.Success;
        }

        private bool TryOpenSaveData(string path, out GrandTheftAutoSave data)
        {
            const int LeftPadLength = 12;
            string errMsg = "";

            data = null;
            try
            {
                Logger.InfoVerbose($">> Opening {path}...");
                switch (m_opts.Mode)
                {
                    case Mode.GTA3:
                        data = GrandTheftAutoSave.Load<GTA3Save>(path);
                        break;
                    case Mode.VC:
                        data = GrandTheftAutoSave.Load<ViceCitySave>(path);
                        break;
                }
                Logger.InfoVerbose(" success!\n");
            }
            catch (IOException e)
            {
                errMsg = $"{e.GetType().Name}: {e.Message}";
            }

            if (data == null)
            {
                Logger.InfoVerbose(" failed!\n");
                errMsg = (string.IsNullOrEmpty(errMsg))
                    ? string.Format(Strings.ErrorText_FailedToOpenFile, path)
                    : errMsg;
                Logger.Error(errMsg);

                return false;
            }

            Logger.InfoVerbose("File info:\n");
            Logger.InfoVerbose($"Game: ".PadLeft(LeftPadLength));
            Logger.InfoVerbose(m_opts.Mode + "\n");
            Logger.InfoVerbose("Name: ".PadLeft(LeftPadLength));
            Logger.InfoVerbose(data.Name + "\n");
            Logger.InfoVerbose("Format: ".PadLeft(LeftPadLength));
            Logger.InfoVerbose(data.FileFormat + "\n");

            return true;
        }

        private bool LoadPriorityMap()
        {
            bool ret;

            try
            {
                m_priorityMap.Clear();
                Logger.InfoVerbose($">> Opening {m_opts.PriorityFile}...");

                using (TextFieldParser p = new TextFieldParser(m_opts.PriorityFile))
                {
                    Logger.InfoVerbose(" success!\n");

                    p.CommentTokens = new string[] { "#" };
                    p.SetDelimiters(new string[] { "," });

                    while (!p.EndOfData)
                    {
                        bool hasPriority = false;
                        bool hasIndex = false;
                        int priority = -1;
                        int index = -1;
                        long line = p.LineNumber;
                        string[] fields = p.ReadFields();

                        if (fields.Length >= 2)
                        {
                            hasPriority = int.TryParse(fields[0], out priority);
                            hasIndex = int.TryParse(fields[1], out index);
                        }

                        if (!hasPriority || !hasIndex || index < 0)
                        {
                            Logger.Info(Strings.WarningText_PriorityListMalformedLine + "\n",
                                Path.GetFileName(m_opts.PriorityFile), line);
                            continue;
                        }

                        if (index >= MaxCapacity)
                        {
                            Logger.Info(Strings.WarningText_PriorityListIndexExceedsMaximum + "\n",
                                Path.GetFileName(m_opts.PriorityFile), line, MaxCapacity - 1);
                            continue;
                        }

                        if (!m_priorityMap.ContainsKey(priority))
                        {
                            m_priorityMap[priority] = new List<int>();
                        }
                        m_priorityMap[priority].Add(index);
                    }
                }
                ret = true;
            }
            catch (IOException e)
            {
                Logger.InfoVerbose(" failed!\n");
                Logger.Error($"{e.GetType().Name}: {e.Message}");
                ret = false;
            }

            return ret;
        }

        private void GeneratePriorityMap()
        {
            m_priorityMap.Clear();

            var indices = Enumerable.Range(0, MaxCapacity).OrderBy(e => m_rand.Next());

            int i = 0;
            foreach (int index in indices)
            {
                m_priorityMap[i++] = new List<int>() { index };
            }
        }

        private bool CheckForCollision(ICarGenerator cg, out int collidedIndex, out double distance)
        {
            collidedIndex = -1;
            distance = 0;

            bool collision = false;
            int index = 0;
            foreach (ICarGenerator tgtCg in m_targetCarGenBlock.ParkedCars)
            {
                double dist = cg.Position.DistanceTo(tgtCg.Position);
                if (dist <= m_opts.CollisionRadius)
                {
                    collision = true;
                    collidedIndex = index;
                    distance = dist;
                    break;
                }
                index++;
            }

            return collision;
        }

        private void PrintLaunchOptions()
        {
            const int LeftPadLength = 18;
            int i = 0;

            Logger.InfoVerbose("Launch options:\n");

            Logger.InfoVerbose("Mode: ".PadLeft(LeftPadLength));
            Logger.InfoVerbose(m_opts.Mode + "\n");
            if (m_userProvidedPriorityMap)
            {
                Logger.InfoVerbose("Priority list: ".PadLeft(LeftPadLength));
                Logger.InfoVerbose(m_opts.PriorityFile + "\n");
            }
            foreach (string s in m_opts.SourceFiles)
            {
                Logger.InfoVerbose($"Source[{i++}]: ".PadLeft(LeftPadLength));
                Logger.InfoVerbose(s + "\n");
            }
            Logger.InfoVerbose("Target: ".PadLeft(LeftPadLength));
            Logger.InfoVerbose(m_opts.TargetFile + "\n");
            Logger.InfoVerbose("Output: ".PadLeft(LeftPadLength));
            Logger.InfoVerbose(m_opts.OutputFile + "\n");
            if (m_setTitle)
            {
                Logger.InfoVerbose("Output title: ".PadLeft(LeftPadLength));
                Logger.InfoVerbose(m_opts.OutputTitle + "\n");
            }
            Logger.InfoVerbose("Collision radius: ".PadLeft(LeftPadLength));
            Logger.InfoVerbose(m_opts.CollisionRadius + "\n");
        }
        private string Pluralize(int count)
        {
            return (count != 1) ? "s" : "";
        }
    }
}
