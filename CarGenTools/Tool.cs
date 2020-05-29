using GTASaveData;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Security;

namespace CarGenTools
{
    public abstract class Tool<O>
        where O : ToolOptions
    {
        public ExitCode Result { get; protected set; }
        public O Options { get; private set; }

        public Tool(O options)
        {
            Options = options;
        }

        public abstract void Run();

        protected TSaveData OpenSaveData<TSaveData>(string path)
            where TSaveData : SaveData, new()
        {
            Log.InfoV($"Reading {path}...");
            TSaveData save = SaveData.Load<TSaveData>(path);
            if (save == null)
            {
                throw new InvalidDataException($"Not a valid {Options.Game} savedata file.");
            }

            return save;
        }

        protected bool TryOpenSaveData<TSaveData>(string path, out TSaveData data)
            where TSaveData : SaveData, new()
        {
            data = null;
            try
            {
                data = OpenSaveData<TSaveData>(path);
                return true;
            }
            catch (Exception e)
            {
                if (e is InvalidDataException)
                {
                    Result = ExitCode.BadSaveData;
                    Log.Exception(e);
                    return false;
                }
                if (e is IOException || e is UnauthorizedAccessException || e is SecurityException)
                {
                    Result = ExitCode.BadIO;
                    Log.Exception(e);
                    return false;
                }
                throw;
            }
        }

        protected bool TryWriteSaveData<TSaveData>(string path, TSaveData data)
            where TSaveData : SaveData, new()
        {
            try
            {
                Log.InfoV($"Writing {path}...");
                data.Save(path);
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

        protected bool TryReadTextFile(string path, out string content)
        {
            content = null;
            try
            {
                Log.InfoV($"Reading {path}...");
                content = File.ReadAllText(path);
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

        protected bool TryWriteTextFile(string path, string content)
        {
            try
            {
                Log.InfoV($"Writing {path}...");
                File.WriteAllText(path, content);
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

        protected bool TryDeserializeJson<T>(string json, out T obj)
        {
            obj = default;
            try
            {
                obj = JsonConvert.DeserializeObject<T>(json);
                return true;
            }
            catch (Exception e)
            {
                if (e is JsonException)
                {
                    Result = ExitCode.BadJson;
                    Log.Exception(e);
                    return false;
                }
                throw;
            }
        }

        protected string SerializeJson(object o)
        {
            return JsonConvert.SerializeObject(o, Formatting.Indented);
        }

        protected string Pluralize(int count)
        {
            return (count != 1) ? "s" : "";
        }
    }
}
