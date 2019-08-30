using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Replenish", "Skrallex & 2CHEVSKII", "0.1.0")]
	[Description("Save and restore items in selected containers")]
	public class Replenish:CovalencePlugin
	{

		private class SerializableItem
		{
			public int ItemID { get; set; }
			public int Amount { get; set; }
			public ulong SkinID { get; set; }
		}

		private class SerializableItemContainer
		{
			public Vector3 PositionVector { get; set; }
			public string PrefabName { get; set; }
			public float AutorestoreTime { get; set; }
			public bool RestoreOnWipe { get; set; }
			public bool RestoreOnDestroy { get; set; }
			public SerializableItem[] Inventory { get; set; }
		}

		private Dictionary<uint, SerializableItemContainer> ReplenishData { get; set; }

		private void Init()
		{
			covalence.RegisterCommand("replenish", this, CmdReplenish);
		}


		private void TryLoadPluginData()
		{
			try
			{
				ReplenishData = Interface.Oxide.DataFileSystem.GetFile("ReplenishData").ReadObject<Dictionary<uint, SerializableItemContainer>>();
				if(ReplenishData == null)
				{
					throw new JsonException("Could not read data.");
				}
			}
			catch
			{
				ReplenishData = new Dictionary<uint, SerializableItemContainer>();
				SavePluginData();
			}
		}

		private void SavePluginData() => Interface.Oxide.DataFileSystem.GetFile("ReplenishData").WriteObject(ReplenishData);

		private SerializableItem MakeSerializable(Item item) => item == null ? null : 
		new SerializableItem
		{
			ItemID = item.info.itemid,
			Amount = item.amount,
			SkinID = item.skin
		};

		private bool CmdReplenish(IPlayer player, string command, string[] args)
		{
			if(args.Length < 1)
			{
				SendMessage(player, mhelp);
			}
			else
			{
				switch(args[0].ToLower())
				{
					case "list":
						ShowSavedList(player);
						break;
					case "inv":
						ShowInventory(player, args);
						break;
					case "del":
						DeleteSavedContainer(player, args);
						break;
				}
			}
			return true;
		}


		#region Show saved containers


		private void ShowSavedList(IPlayer player)
		{
			if(player.HasPermission(PERMISSIONLIST))
			{
				var builder = new StringBuilder();

				builder.AppendLine(GetLocalizedString(player, mlist));

				foreach(var container in ReplenishData)
				{
					builder.AppendLine($"{container.Key} : {container.Value.PositionVector.ToString().Replace("(", string.Empty).Replace(")", string.Empty)}\nRestore on wipe: {container.Value.RestoreOnWipe}\nRestore on timer: {(container.Value.AutorestoreTime > 0f ? container.Value.AutorestoreTime.ToString() : "false")}\nRestore on destroy: {container.Value.RestoreOnDestroy}");
				}

				var _string = builder.ToString();

				player.Message(_string, GetLocalizedString(player, mprefix));
			}
			else
			{
				SendMessage(player, mnoperms);
			}
		}

		private void ShowInventory(IPlayer player, string[] args)
		{
			int result;
			if(!player.HasPermission(PERMISSIONLIST))
			{
				SendMessage(player, mnoperms);
			}
			else if(args.Length < 2|| !int.TryParse(args[1], out result))
			{
				SendMessage(player, mhelp);
			}
			else if(!ReplenishData.ContainsKey((uint)result))
			{
				SendMessage(player, mnocontainer);
			}
			else
			{
				var container = ReplenishData[(uint)result];
				var builder = new StringBuilder();

				builder.AppendLine(GetLocalizedString(player, mcontainerinfo, result));

				foreach(var item in container.Inventory)
				{
					builder.AppendLine($"{ItemManager.FindItemDefinition(item.ItemID)?.displayName.english ?? item.ItemID.ToString()} | {item.Amount}");
				}

				var _string = builder.ToString();

				player.Message(_string, GetLocalizedString(player, mprefix));
			}
		}


		#endregion


		private void DeleteSavedContainer(IPlayer player, string[] args)
		{
			int result;
			if(!player.HasPermission(PERMISSIONSAVE))
			{
				SendMessage(player, mnoperms);
			}
			else if(args.Length < 2 || !int.TryParse(args[1], out result))
			{
				SendMessage(player, mhelp);
			}
			else if(!ReplenishData.ContainsKey((uint)result))
			{
				SendMessage(player, mnocontainer);
			}
			else
			{
				ReplenishData.Remove((uint)result);

				SendMessage(player, mdeleted, result);
			}
		}

		/*
		 *
		 * Functions
		 *
		 * Replenish on request (/replenish <container id>)
		 *
		 * Replenish on timer
		 *
		 * Replenish on wipe
		 *
		 * Replenish when destroyed
		 */


		private const string PERMISSIONLIST = "replenish.list";
		private const string PERMISSIONSAVE = "reprenish.save";
		private const string PERMISSIONRESTORE = "replenish.restore";

		private const string mhelp = "Help message";
		private const string mnoperms = "No permission";
		private const string mprefix = "Prefix";
		private const string mlist = "List of saved crates";
		private const string mcontainerinfo = "Information about specific container";
		private const string mnocontainer = "Wrong container ID";
		private const string mdeleted = "Deleted container";

		private Dictionary<string, string> defaultmessages_en
		{
			get
			{
				return new Dictionary<string, string>
				{
					[mprefix] = "Replenish:",
					[mhelp] = "Help message lul",
					[mnoperms] = "You can't use that command",
					[mlist] = "These are all the crates saved by the plugin",
					[mcontainerinfo] = "Container {0} inventory:",
					[mnocontainer] = "No container saved with that ID!",
					[mdeleted] = "Deleted container with ID: {0}"
				};
			}
		}

		private void SendMessage(IPlayer player, string key,params object[] args)
		{
			if(player != null)
			{
				var message = $"{GetLocalizedString(player, mprefix)} {GetLocalizedString(player, key, args)}";
				player.Message(message);
			}
		}

		private string GetLocalizedString(IPlayer player, string key, params object[] args) => string.Format(lang.GetMessage(key, this, player?.Id), args);
	}
}