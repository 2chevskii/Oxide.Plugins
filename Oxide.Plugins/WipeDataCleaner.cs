using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("Wipe Data Cleaner", "2CHEVSKII", "1.3.4")]
	[Description("Cleans specified data files on new wipe.")]
	internal class WipeDataCleaner : CovalencePlugin
	{

		const string PERMISSIONUSE = "wipedatacleaner.wipe";

		#region -Hooks-


		private void Init()
		{
			permission.RegisterPermission(PERMISSIONUSE, this);

			AddCovalenceCommand(Settings.Command ?? "wipe", "Wipe", PERMISSIONUSE);
		}

		private void OnNewSave(string filename) => Wipe(null);


		#endregion

		#region -Fields-


		private OxideMod Mod = Interface.Oxide;
		private PluginSettings Settings { get; set; }
		private List<string> FilesToWipe { get; set; }


		#endregion

		#region -Configuration-


		private class PluginSettings
		{
			[JsonProperty("Filenames, without .json")]
			public List<string> FileNames { get; set; }

			[JsonProperty("Command (default: 'wipe')")]
			public string Command { get; set; }
		}

		protected override void LoadDefaultConfig()
		{
			Config.Clear();

			Settings = new PluginSettings
			{
				FileNames = new List<string> {
					"somefile",
					"AnotherFile"
				},
				Command = "wipe"
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

		#region -Core-


		private void Wipe(IPlayer executer)
		{
			FilesToWipe = DetermineFilesToWipe().ToList<string>();

			Mod.UnloadAllPlugins(new List<string> {
				nameof(WipeDataCleaner)
			});

			foreach(string file in FilesToWipe)
			{
				var message = WipeFile(file);
				executer?.Message(message);
			}

			Mod.LoadAllPlugins(false);
		}

		private IEnumerable<string> DetermineFilesToWipe()
		{
			Dictionary<string, DynamicConfigFile> allDataFiles = typeof(DataFileSystem)
				.GetField("_datafiles", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(Interface.Oxide.DataFileSystem) as Dictionary<string, DynamicConfigFile>;

			List<string> list = new List<string>();

			foreach(string fileName in Settings.FileNames)
			{
				if(string.IsNullOrEmpty(fileName) || IsNullOrWhiteSpace(fileName))
					continue;

				if(!fileName.Contains("*"))
				{
					list.Add(fileName);

					continue;
				}

				if(fileName.Length == 1)
					list.AddRange(allDataFiles.Keys.Where(x => !x.StartsWith("oxide.")));
				else
					list.AddRange(allDataFiles.Keys.Where(x => x.StartsWith(fileName.TrimEnd('*'))));
			}

			return list.Distinct();
		}

		private string WipeFile(string file)
		{
			if(Interface.Oxide.DataFileSystem.ExistsDatafile(file))
			{
				Interface.Oxide.DataFileSystem.GetFile(file).Clear();
				Interface.Oxide.DataFileSystem.GetFile(file).Save();

				return $"Wiped '{file}.json'";
			}

			return $"Could not find '{file}.json'";
		}

		private bool IsNullOrWhiteSpace(string str) => str == null ? true : str.All(c => c == ' ');


		#endregion
	}
}