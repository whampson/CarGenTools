using GTASaveData;
using GTASaveData.GTA3;
using GTASaveData.Types.Interfaces;
using GTASaveData.VC;
using System;
using System.Collections.Generic;
using System.Linq;
using GTA3CarGenBlock = GTASaveData.GTA3.CarGeneratorData;
using VCCarGenBlock = GTASaveData.VC.CarGeneratorData;

namespace CarGenTools.CarGenImport
{
    public class Import : Tool<Options>
    {
        private bool SetTitle { get; }
        private int MaxCapacity => MaxCapacityMap[Options.Game];

        public Import(Options options)
            : base(options)
        {
            SetTitle = !string.IsNullOrEmpty(options.Title);
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

        private void ImportCarGens<TSaveData>()
            where TSaveData : SaveData, new()
        {
            string outFile = !string.IsNullOrEmpty(Options.OutputFile)
                ? Options.OutputFile
                : Options.SaveDataFile;

            if (!TryOpenSaveData(Options.SaveDataFile, out TSaveData save)) return;
            if (!TryReadTextFile(Options.CarGenFile, out string json)) return;
            if (!TryDeserializeCarGenData(json, out ICarGeneratorData newCarGenData)) return;

            int numCarGens = newCarGenData.CarGenerators.Count();
            if (numCarGens > MaxCapacity)
            {
                Log.Info($"Warning: Car generator limit exceeded! A maximum of {MaxCapacity} car generators will be imported.");
            }

            ISaveData isave = (save as ISaveData);
            if (Options.Replace)
            {
                isave.CarGenerators = newCarGenData;
                Log.Info($"Replaced {MaxCapacity} car generators.");
            }
            else
            {
                int numImported = 0;
                for (int i = 0; i < numCarGens; i++)
                {
                    var cg = newCarGenData[i];
                    if (cg.Model != 0)
                    {
                        isave.CarGenerators[i] = cg;
                        Log.InfoV($"Wrote slot {i}: Enabled = {cg.Enabled}; Model = {cg.Model}; Location = {cg.Position}");
                        numImported++;
                    }
                }
                Log.Info($"Imported {numImported} car generator{Pluralize(numImported)}.");
            }

            UpdateCarGenMetadata(isave.CarGenerators);
            if (SetTitle)
            {
                save.Name = Options.Title;
                Log.Info($"Title set to: {save.Name}");
            }
            save.TimeLastSaved = DateTime.Now;

            if (!TryWriteSaveData(outFile, save)) return;
            Result = ExitCode.Success;
        }

        private void ExportCarGens<TSaveData>()
            where TSaveData : SaveData, new()
        {
            string outFile = !string.IsNullOrEmpty(Options.OutputFile)
                ? Options.OutputFile
                : Options.CarGenFile;

            if (!TryOpenSaveData(Options.SaveDataFile, out TSaveData save)) return;

            ICarGeneratorData carGens = (save as ISaveData).CarGenerators;
            int numCarGens = carGens.CarGenerators.Count();

            string json = SerializeJson(carGens);
            if (!TryWriteTextFile(outFile, json)) return;

            Log.Info($"Exported {numCarGens} car generator{Pluralize(numCarGens)}.");
            Result = ExitCode.Success;
        }

        private bool TryDeserializeCarGenData(string json, out ICarGeneratorData data)
        {
            bool result;
            data = null;

            switch (Options.Game)
            {
                case Game.GTA3:
                {
                    result = TryDeserializeJson(json, out GTA3CarGenBlock carGens);
                    data = carGens;
                    break;
                }
                case Game.VC:
                {
                    result = TryDeserializeJson(json, out VCCarGenBlock carGens);
                    data = carGens;
                    break;
                }
                default:
                {
                    throw new InvalidOperationException("Game not supported!");
                }
            }

            return result;
        }

        public override void Run()
        {
            if (Options.Export)
            {
                switch (Options.Game)
                {
                    case Game.GTA3: ExportCarGens<GTA3Save>(); break;
                    case Game.VC: ExportCarGens<ViceCitySave>(); break;
                }
            }
            else
            {
                switch (Options.Game)
                {
                    case Game.GTA3: ImportCarGens<GTA3Save>(); break;
                    case Game.VC: ImportCarGens<ViceCitySave>(); break;
                }
            }
        }

        private static readonly Dictionary<Game, int> MaxCapacityMap = new Dictionary<Game, int>()
        {
            { Game.GTA3, GTA3CarGenBlock.Limits.MaxNumCarGenerators },
            { Game.VC, VCCarGenBlock.Limits.MaxNumCarGenerators },
        };
    }
}
