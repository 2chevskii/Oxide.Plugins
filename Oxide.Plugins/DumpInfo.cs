using System;
using System.Collections.Generic;
using Rust;
using System.Linq;
using UnityEngine;
using Facepunch.Extend;
using Newtonsoft.Json;
using System.Text;
using Facepunch;
using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("Dump Info", "2CHEVSKII", "0.1.0")]
	[Description("Dumps up to date game information for developers")]
	class DumpInfo:CovalencePlugin
	{
		private List<RustLayer> Layers = new List<RustLayer>();

		private List<RustItem> Items = new List<RustItem>();

		private List<string> Textures = new List<string>();

		private List<string> Prefabs = new List<string>();

		struct RustInfo
		{
			public List<RustLayer> Layers { get; set; }

			public List<RustItem> Items { get; set; }

			public List<string> Materials { get; set; }

			public List<string> Textures { get; set; }

			public List<string> Prefabs { get; set; }
		}

		struct RustItem
		{
			public string Name { get; set; }
			public string ShortName { get; set; }
			public int ItemID { get; set; }
			public bool Craftable { get; set; }
			public Dictionary<ulong, string> Skins { get; set; }
		}

		struct RustLayer
		{
			public int LayerNumber { get; set; }
			public string LayerNameEnum { get; set; }
			public string LayerNameMask { get; set; }
		}

		void OnServerInitialized()
		{
			try
			{
				GetLayers();
				GetItems();
				GetTextures();
				GetPrefabs();
				Dump();
				server.Command("quit");
			}
			catch
			{
				timer.Once(10f, OnServerInitialized);
			}
		}

		void Dump()
		{
			var info = new RustInfo()
			{
				Items = Items,
				Layers = Layers,
				Prefabs = Prefabs,
				Textures = Textures
			};

			var json = JsonConvert.SerializeObject(info, Formatting.Indented);

			var layersMD = ConvertToMarkdown("layers", Layers);
			var itemsMD = ConvertToMarkdown("items", Items);
			var texturesMD = ConvertToMarkdown("tex", Textures);
			var prefabsMD = ConvertToMarkdown("pref", Prefabs);
			var skinsMD = ConvertToMarkdown("skin", Items);

			LogToFile($"DumpInfoJson_{Protocol.printable}_{BuildInfo.Current.Scm.Branch}", json, this, false);

			LogToFile($"DumpInfoMD_Layers_{Protocol.printable}_{BuildInfo.Current.Scm.Branch}", layersMD, this, false);
			LogToFile($"DumpInfoMD_Items_{Protocol.printable}_{BuildInfo.Current.Scm.Branch}", itemsMD, this, false);
			LogToFile($"DumpInfoMD_Textures_{Protocol.printable}_{BuildInfo.Current.Scm.Branch}", texturesMD, this, false);
			LogToFile($"DumpInfoMD_Prefabs_{Protocol.printable}_{BuildInfo.Current.Scm.Branch}", prefabsMD, this, false);
			LogToFile($"DumpInfoMD_Skins_{Protocol.printable}_{BuildInfo.Current.Scm.Branch}", skinsMD, this, false);
		}

		string ConvertToMarkdown(string name, object obj)
		{

			string format = string.Empty;

			var builder = new StringBuilder();

			switch(name)
			{
				case "layers":
					var layerlist = obj as List<RustLayer>;
					builder.AppendLine("`Enum name` | `LayerMask name` | `ID`");
					builder.AppendLine("--- | --- | ---");
					foreach(var layer in layerlist)
					{
						builder.AppendLine($"{layer.LayerNameEnum} | {layer.LayerNameMask} | {layer.LayerNumber}");
					}
					break;

				case "items":
					var itemlist = obj as List<RustItem>;
					builder.AppendLine("`Name` | `ShortName` | `Item ID` | `Craftable`");
					builder.AppendLine("--- | --- | --- | ---");
					foreach(var item in itemlist)
					{
						builder.AppendLine($"{item.Name} | {item.ShortName} | {item.ItemID} | {item.Craftable}");
					}
					break;

				case "skin":
					var list = obj as List<RustItem>;
					foreach(var item in list.Where(x => x.Skins.Count > 0))
					{
						builder.AppendLine($"**{item.Name}**");
						builder.AppendLine("`Name` | `ID`");
						builder.AppendLine("--- | ---");
						foreach(var skin in item.Skins)
						{
							builder.AppendLine($"{skin.Value} | {skin.Key}");
						}
					}
					break;

				case "tex":
					builder.AppendLine("`Path` |");
					builder.AppendLine("--- |");
					foreach(var mat in obj as List<string>)
					{
						builder.AppendLine($"{mat} |");
					}
					break;

				case "pref":
					builder.AppendLine("`Path` |");
					builder.AppendLine("--- |");
					foreach(var mat in obj as List<string>)
					{
						builder.AppendLine($"{mat} |");
					}
					break;
			}

			format = builder.ToString();
			return format;
		}

		void GetPrefabs()
		{
			Prefabs.AddRange(AssetBundleBackend.files.Keys.Where(x => x.EndsWith(".prefab")));
		}

		void GetTextures()
		{
			Textures.AddRange(AssetBundleBackend.files.Keys.Where(x => x.EndsWith(".psd") || x.EndsWith(".tga") || x.EndsWith(".png") || x.EndsWith(".jpg")));
		}

		void GetLayers()
		{
			foreach(var layer in Enum.GetValues(typeof(Layer)))
			{
				if(Layers.Any(x => x.LayerNumber == (int)layer)) continue;

				var lmname = LayerMask.LayerToName((int)layer);
				if(string.IsNullOrEmpty(lmname) || string.IsNullOrWhiteSpace(lmname)) lmname = layer.ToString();

				Layers.Add(new RustLayer
				{
					LayerNumber = (int)layer,
					LayerNameEnum = layer.ToString(),
					LayerNameMask = lmname
				});
			}
		}

		void GetItems()
		{
			foreach(var definition in ItemManager.GetItemDefinitions())
			{
				if(Items.Any(x => x.ItemID == definition.itemid)) continue;

				var list = new Dictionary<ulong, string>();
				
				if(definition.HasSkins)
				{
					foreach(var skin in definition.skins)
					{
						var id = ItemDefinition.FindSkin(definition.itemid, skin.id);

						if(!list.ContainsKey(id))
						{
							list.Add(id, skin.invItem.displayName.english);
						}
					}

					foreach(var skin in definition.skins2)
					{
						var id = ItemDefinition.FindSkin(definition.itemid, skin.Id);

						if(!list.ContainsKey(id))
						{
							list.Add(id, skin.Name);
						}
					}
				}

				var item = new RustItem
				{
					ItemID = definition.itemid,
					ShortName = definition.shortname,
					Name = definition.displayName.english,
					Craftable = definition.Blueprint?.userCraftable ?? false,
					Skins = list
				};

				Items.Add(item);
			}
		}

	}
}
