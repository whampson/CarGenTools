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
using System.Security;
using GTA3CarGenBlock = GTASaveData.GTA3.CarGeneratorData;
using VCCarGenBlock = GTASaveData.VC.CarGeneratorData;

namespace CarGenTools.CarGenMerge
{
    public class Merge : Tool<Options>
    {
        private string OutputFilePath { get; }
        private bool SetTitle { get; }
        private bool HasUserProvidedPriorityMap { get; }
        private Dictionary<int, List<int>> PriorityMap { get; }
        private Random RandGen { get; }
        private int MaxCapacity => MaxCapacityMap[Options.Game];

        public Merge(Options options)
            : base(options)
        {
            OutputFilePath = !string.IsNullOrEmpty(options.OutputFile) ? options.OutputFile : options.TargetFile;
            SetTitle = !string.IsNullOrEmpty(options.Title);
            HasUserProvidedPriorityMap = !string.IsNullOrEmpty(options.PriorityFile);
            PriorityMap = new Dictionary<int, List<int>>();
            RandGen = new Random(Guid.NewGuid().GetHashCode());
        }

        private void UpdateCarGenMetadata(ICarGeneratorData carGens)
        {
            carGens.NumberOfCarGenerators = MaxCapacity;
            carGens.NumberOfEnabledCarGenerators = 0;
            foreach (ICarGenerator g in carGens.CarGenerators)
            {
                if (g.Model != 0 && g.Enabled)
                {
                    carGens.NumberOfEnabledCarGenerators++;
                }
            }
        }

        private void MergeCarGens<TSaveData>()
            where TSaveData : SaveData, new()
        {
            List<TSaveData> sourceSaves = new List<TSaveData>();
            List<ICarGenerator> differingCarGens = new List<ICarGenerator>();
            CarGenComparer cgComparer = new CarGenComparer();

            // Load priority map
            if (HasUserProvidedPriorityMap)
            {
                if (!TryLoadPriorityMap()) return;
            }
            else
            {
                GeneratePriorityMap();
            }

            // Load target file
            if (!TryOpenSaveData(Options.TargetFile, out TSaveData targetSave)) return;

            //  Load source files and find differing car generators
            foreach (string sourcePath in Options.SourceFiles)
            {
                if (!TryOpenSaveData(sourcePath, out TSaveData sourceSave)) return;

                int numDifferingThisSave = 0;
                for (int i = 0; i < MaxCapacity; i++)
                {
                    ICarGenerator tgt = (targetSave as ISaveData).CarGenerators[i];
                    ICarGenerator src = (sourceSave as ISaveData).CarGenerators[i];
                    if (src.Model != 0 && !cgComparer.Equals(src, tgt))
                    {
                        Log.InfoF($"Difference found in slot {i}.");
                        differingCarGens.Add(src);
                        numDifferingThisSave++;
                    }
                }
                Log.Info($"Found {numDifferingThisSave} differing car generator{Pluralize(numDifferingThisSave)} in {Path.GetFileName(sourcePath)}.");
            }
            int numDiffering = differingCarGens.Count;

            // Reduce the priority map to a list of car generator indices representing the replacement order.
            // First, sort the map in ascending order by key (priority) and exclude negatives. Next, flatten
            // all values (list of car generator indices) into a single list of integers, randomizing each
            // sub-list along the way.
            List<int> replacementOrder = PriorityMap
                .OrderBy(pair => pair.Key).Where(pair => pair.Key > -1)
                .SelectMany(pair => pair.Value.OrderBy(i => RandGen.Next()))
                .ToList();

            // Merge!
            ISaveData target = (targetSave as ISaveData);
            ICarGeneratorData targetCarGens = target.CarGenerators;
            int numReplaced = 0;
            foreach (int cgIndex in replacementOrder)
            {
                if (numReplaced >= numDiffering)
                {
                    break;
                }

                ICarGenerator cg = differingCarGens[numReplaced++];
                if (CheckForCollision(targetCarGens, cg, out int idx, out double dist))
                {
                    Log.Error($"Collision found: Slot = {idx}; Location = {targetCarGens[idx].Position}; Distance = {dist:0.###}");
                    Result = ExitCode.Error;
                    return;
                }

                targetCarGens[cgIndex] = cg;
                Log.InfoV($"Wrote slot {cgIndex}: Enabled = {cg.Enabled}; Model = {cg.Model}; Location = {cg.Position}");
            }
            Log.Info($"Merged {numReplaced} car generator{Pluralize(numReplaced)}.");

            UpdateCarGenMetadata(targetCarGens);
            if (SetTitle)
            {
                targetSave.Name = Options.Title;
                Log.Info($"Title set to: {targetSave.Name}");
            }
            targetSave.TimeLastSaved = DateTime.Now;

            if (!TryWriteSaveData(OutputFilePath, targetSave)) return;
            Result = ExitCode.Success;
        }

        private bool CheckForCollision(ICarGeneratorData targetCarGens, ICarGenerator gen, out int targetIndex, out double distance)
        {
            targetIndex = -1;
            distance = 0;

            if (Options.Radius <= 0)
            {
                return false;
            }

            bool collision = false;
            int index = 0;
            foreach (ICarGenerator tgtCg in targetCarGens.CarGenerators)
            {
                double dist = Vector3D.Distance(gen.Position, tgtCg.Position);
                if (tgtCg.Model != 0 && dist <= Options.Radius)
                {
                    collision = true;
                    targetIndex = index;
                    distance = dist;
                    break;
                }
                index++;
            }

            return collision;
        }

        private bool TryLoadPriorityMap()
        {
            try
            {
                PriorityMap.Clear();

                Log.InfoV($"Reading {Options.PriorityFile}...");
                using (TextFieldParser p = new TextFieldParser(Options.PriorityFile))
                {
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
                            Log.Info($"Warning: {Path.GetFileName(Options.PriorityFile)}:{line}: Line is malformatted.");
                            continue;
                        }
                        if (index >= MaxCapacity)
                        {
                            Log.Info($"Warning: {Path.GetFileName(Options.PriorityFile)}:{line}: Index exceeds maximum value of {MaxCapacity - 1}.");
                            continue;
                        }
                        if (!PriorityMap.ContainsKey(priority))
                        {
                            PriorityMap[priority] = new List<int>();
                        }
                        PriorityMap[priority].Add(index);
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                if (e is IOException || e is UnauthorizedAccessException || e is SecurityException)
                {
                    Result = ExitCode.BadIO;
                    Log.Exception(e);
                    return false;
                }
                throw;
            }
        }

        private void GeneratePriorityMap()
        {
            PriorityMap.Clear();

            IEnumerable<int> carGenIndices = Enumerable.Range(0, MaxCapacity).OrderBy(e => RandGen.Next());

            int i = 0;
            foreach (int index in carGenIndices)
            {
                PriorityMap[i++] = new List<int>() { index };
            }
        }

        public override void Run()
        {
            switch (Options.Game)
            {
                case Game.GTA3: MergeCarGens<GTA3Save>(); break;
                case Game.VC: MergeCarGens<ViceCitySave>(); break;
            }
        }

        private static readonly Dictionary<Game, int> MaxCapacityMap = new Dictionary<Game, int>()
        {
            { Game.GTA3, GTA3CarGenBlock.Limits.MaxNumCarGenerators },
            { Game.VC, VCCarGenBlock.Limits.MaxNumCarGenerators },
        };
    }
}
