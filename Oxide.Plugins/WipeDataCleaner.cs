using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("Wipe Data Cleaner", "2CHEVSKII", "1.1.0")]
	[Description("Cleans specified data files on new wipe.")]
	internal class WipeDataCleaner : CovalencePlugin
	{

		#region -Fields-


		private OxideMod Mod = Interface.Oxide;
		private PluginSettings Settings { get; set; }


		#endregion

		#region -Configuration-


		private class PluginSettings
		{
			[JsonProperty(PropertyName = "Filenames, without .json")]
			public List<string> FileNames { get; set; }
		}

		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			Settings = new PluginSettings {
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


		#endregion

		#region -Hooks-


		private void OnNewSave(string filename) => Wipe(null);


		#endregion

		#region -Core-


		[Command("wipe"), Permission(nameof(WipeDataCleaner) + ".wipe")]
		private void Wipe(IPlayer executer)
		{
			Mod.UnloadAllPlugins(new List<string>
			{
				nameof(WipeDataCleaner)
			});
			foreach(string file in Settings.FileNames)
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


		#endregion



	}
}
