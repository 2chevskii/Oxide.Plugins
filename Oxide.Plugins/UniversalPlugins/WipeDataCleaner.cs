using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Wipe Data Cleaner", "2CHEVSKII", "1.1.0")]
    [Description("Cleans specified data files on new wipe.")]
    class WipeDataCleaner : CovalencePlugin
    {
        private OxideMod Mod = Interface.Oxide;

        private PluginSettings Settings { get; set; }

        private class PluginSettings
        {
            [JsonProperty(PropertyName = "Filenames, without .json")]
            public List<string> FileNames { get; set; }
        }
        
        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Settings = new PluginSettings
            {
                FileNames = new List<string>
                {
                    "somefile",
                    "AnotherFile"
                }
            };
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(Settings);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<PluginSettings>();
                if(Settings == null || Settings.FileNames == null)
                    throw new JsonException();
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        private void OnNewSave(string filename) => Wipe(null);

        [Command("wipe"), Permission(nameof(WipeDataCleaner) + ".wipe")]
        private void Wipe(IPlayer executer)
        {
            Mod.UnloadAllPlugins(new List<string>
            {
                nameof(WipeDataCleaner)
            });
            foreach(var file in Settings.FileNames)
            {
                if(Interface.Oxide.DataFileSystem.ExistsDatafile(file))
                {
                    Interface.Oxide.DataFileSystem.GetFile(file).Clear();
                    Interface.Oxide.DataFileSystem.GetFile(file).Save();
                    executer?.Message($"Wiped \"{file}.json\"");
                }
            }
            Mod.LoadAllPlugins(false);
        }
    }
}
