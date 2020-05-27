using GTASaveData;
using GTASaveData.GTA3;
using GTASaveData.Types;
using GTASaveData.Types.Interfaces;
using GTASaveData.VC;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using GTA3CarGen = GTASaveData.GTA3.CarGenerator;
using GTA3CarGenBlock = GTASaveData.GTA3.CarGeneratorData;
using VCCarGen = GTASaveData.VC.CarGenerator;
using VCCarGenBlock = GTASaveData.VC.CarGeneratorData;

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
        private GTASaveFile m_targetSave;

        private static readonly Dictionary<Mode, int> CarGeneratorMaxCapacity = new Dictionary<Mode, int>()
        {
            { Mode.GTA3, GTA3CarGenBlock.Limits.MaxNumCarGenerators },
            { Mode.VC, VCCarGenBlock.Limits.MaxNumCarGenerators },
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
            m_targetCarGenBlock = GetCarGenBlock(m_targetSave);

            foreach (string path in m_opts.SourceFiles)
            {
                if (!TryOpenSaveData(path, out GTASaveFile src))
                {
                    return ExitCode.BadIO;
                }
                m_sourceCarGenBlocks.Add(GetCarGenBlock(src));
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
                    ICarGenerator tgt = m_targetCarGenBlock[i];
                    ICarGenerator src = cgBlock[i];
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
                    ICarGenerator collidedWith = m_targetCarGenBlock[colIdx];
                    Logger.Error("Error: Collision detected!\n" +
                        "Model {0} at ({1}) in source collides with model {2} at ({3}) in target. (distance = {4:0.###}, target index = {5})",
                        replacement.Model, replacement.Position, collidedWith.Model, collidedWith.Position, colDist, colIdx);
                    return ExitCode.Collision;
                }

                SetCarGen(cgIndex, replacement);
                Logger.InfoVerbose("Replaced car generator {0} with model {1}.\n", cgIndex, replacement.Model);
            }
            Logger.Info("{0} car generator{1} replaced.\n", numReplaced, Pluralize(numReplaced));


            Logger.InfoVerbose(">> Setting number of car generators to {0}...\n", MaxCapacity);
            m_targetCarGenBlock.NumberOfCarGenerators = MaxCapacity;
            m_targetCarGenBlock.CurrentActiveCount = MaxCapacity;

            if (m_setTitle)
            {
                Logger.InfoVerbose(">> Setting save title...\n");
                m_targetSave.Name = m_opts.OutputTitle;
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
                Logger.Error($"Error: {e.GetType().Name}: {e.Message}");
                return ExitCode.BadIO;
            }

            return ExitCode.Success;
        }

        private bool TryOpenSaveData(string path, out GTASaveFile data)
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
                        data = GTASaveFile.Load<GTA3Save>(path);
                        break;
                    case Mode.VC:
                        data = GTASaveFile.Load<ViceCitySave>(path);
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
                Logger.Error("Error: " + errMsg);

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

        private ICarGeneratorBlock GetCarGenBlock(GTASaveFile save)
        {
            ICarGeneratorBlock carGens = null;
            switch (m_opts.Mode)
            {
                case Mode.GTA3:
                    carGens = (save as GTA3Save).CarGenerators;
                    break;
                case Mode.VC:
                    carGens = (save as ViceCitySave).CarGenerators;
                    break;
            }

            return carGens;
        }

        private void SetCarGen(int index, ICarGenerator value)
        {
            value.Timer = 0;

            if (m_opts.Mode == Mode.GTA3)
            {
                (m_targetCarGenBlock as GTA3CarGenBlock).CarGenerators[index] = value as GTA3CarGen;
            }
            else if (m_opts.Mode == Mode.VC)
            {
                (m_targetCarGenBlock as VCCarGenBlock).CarGenerators[index] = value as VCCarGen;
            }
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
                Logger.Error($"Error: {e.GetType().Name}: {e.Message}");
                ret = false;
            }

            return ret;
        }

        private void GeneratePriorityMap()
        {
            m_priorityMap.Clear();

            var indices = Enumerable.Range(0, MaxCapacity)/*.OrderBy(e => m_rand.Next())*/;

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

            if (m_opts.CollisionRadius <= 0)
            {
                return false;
            }

            bool collision = false;
            int index = 0;
            foreach (ICarGenerator tgtCg in m_targetCarGenBlock.CarGenerators)
            {
                double dist = Vector3D.Distance(cg.Position, tgtCg.Position);
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
