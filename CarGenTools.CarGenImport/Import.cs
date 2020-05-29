using GTASaveData;
using GTASaveData.GTA3;
using GTASaveData.Types.Interfaces;
using GTASaveData.VC;
using System;
using System.Linq;
using GTA3CarGens = GTASaveData.GTA3.CarGeneratorData;
using VCCarGens = GTASaveData.VC.CarGeneratorData;

namespace CarGenTools.CarGenImport
{
    public class Import : Tool<Options>
    {
        private bool SetTitle { get; }

        public Import(Options options)
            : base(options)
        {
            SetTitle = !string.IsNullOrEmpty(options.Title);
        }

        private void UpdateCarGenMetadata(ICarGeneratorData carGens)
        {
            carGens.NumberOfValidCarGenerators = 0;
            carGens.NumberOfEnabledCarGenerators = 0;
            foreach (ICarGenerator g in carGens.CarGenerators)
            {
                if (g.Model != 0) carGens.NumberOfValidCarGenerators++;
                if (g.Model != 0 && g.Enabled) carGens.NumberOfEnabledCarGenerators++;
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
            if (!TryDeserializeCarGenData(json, out ICarGeneratorData newCarGenData, out int maxCount)) return;

            int numCarGens = newCarGenData.CarGenerators.Count();
            if (numCarGens > maxCount)
            {
                Log.Info($"Warning: Car generator limit exceeded! A maximum of {maxCount} car generators will be imported.");
            }

            ISaveData isave = (save as ISaveData);
            if (Options.Replace)
            {
                isave.CarGenerators = newCarGenData;
                Log.Info($"Replaced {maxCount} car generators.");
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

        private bool TryDeserializeCarGenData(string json, out ICarGeneratorData data, out int maxCount)
        {
            bool result;
            data = null;
            maxCount = 0;

            switch (Options.Game)
            {
                case Game.GTA3:
                {
                    result = TryDeserializeJson(json, out GTA3CarGens carGens);
                    data = carGens;
                    maxCount = GTA3CarGens.Limits.MaxNumCarGenerators;
                    break;
                }
                case Game.VC:
                {
                    result = TryDeserializeJson(json, out VCCarGens carGens);
                    data = carGens;
                    maxCount = VCCarGens.Limits.MaxNumCarGenerators;
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
    }
}
