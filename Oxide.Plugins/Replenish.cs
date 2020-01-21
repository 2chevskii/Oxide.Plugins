using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Replenish", "Skrallex & 2CHEVSKII", "0.1.0")]
	[Description("Save and restore items in selected containers")]
	public class Replenish : CovalencePlugin
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
			public bool RestoreOnRestart { get; set; }
			public SerializableItem[] Inventory { get; set; }

			public void Restore()
			{
				StorageContainer container;
				if(!BaseNetworkable.serverEntities.Any(ent => ent is StorageContainer && ent.transform.position == this.PositionVector))
				{
					container = GameManager.server.CreateEntity(this.PrefabName, this.PositionVector) as StorageContainer;
					container.Spawn();
					container.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
				}
				else
				{
					container = BaseNetworkable.serverEntities.First(x => x.transform.position == this.PositionVector) as StorageContainer;
				}

				container.inventory.Clear();
				foreach(var item in this.Inventory)
				{
					var citem = ItemManager.CreateByItemID(item.ItemID, item.Amount, item.SkinID);
					if(!citem.MoveToContainer(container.inventory))
					{
						citem.Remove();
					}
				}
			}
		}

		private List<SerializableItemContainer> ReplenishData { get; set; }

		int GetUNIXTime() => (int)(DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

		private void Init()
		{
			TryLoadPluginData();
			covalence.RegisterCommand("replenish", this, CmdReplenish);
			permission.RegisterPermission(PERMISSIONLIST, this);
			permission.RegisterPermission(PERMISSIONRESTORE, this);
			permission.RegisterPermission(PERMISSIONSAVE, this);
		}

		protected override void LoadDefaultMessages() => lang.RegisterMessages(defaultmessages_en, this);

		void OnNewSave()
		{
			foreach(var container in ReplenishData)
			{
				if(container.RestoreOnWipe)
				{
					RestoreQueue.Enqueue(container);
				}
			}
		}

		void OnServerInitialized()
		{
			foreach(var container in ReplenishData)
			{
				if((container.RestoreOnRestart || (container.RestoreOnDestroy && !BaseNetworkable.serverEntities.Any(ent => ent.transform.position == container.PositionVector && ent is StorageContainer && !ent.IsDestroyed))) && !RestoreQueue.Contains(container))
				{
					RestoreQueue.Enqueue(container);
				}
			}

			while(RestoreQueue.Count > 0)
			{
				RestoreQueue.Dequeue().Restore();
			}
		}

		void OnEntityKill(BaseNetworkable entity)
		{
			if(entity is StorageContainer && ReplenishData.Any(x => x.PositionVector == entity.transform.position && x.RestoreOnDestroy))
			{
				ReplenishData.Find(x => x.PositionVector == entity.transform.position).Restore();
			}
		}

		Queue<SerializableItemContainer> RestoreQueue = new Queue<SerializableItemContainer>();

		private void TryLoadPluginData()
		{
			try
			{
				ReplenishData = Interface.Oxide.DataFileSystem.GetFile("ReplenishData").ReadObject<List<SerializableItemContainer>>();
				if(ReplenishData == null)
				{
					throw new JsonException("Could not read data.");
				}
			}
			catch
			{
				ReplenishData = new List<SerializableItemContainer>();
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

		private SerializableItemContainer MakeSerializable(StorageContainer container) => container == null ? null :
		new SerializableItemContainer
		{
			PositionVector = container.transform.position,
			PrefabName = container.PrefabName,
			Inventory = (from i in container.inventory.itemList
						 select new SerializableItem
						 {
							 Amount = i.amount,
							 ItemID = i.info.itemid,
							 SkinID = i.skin
						 }).ToArray()
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
					case "save":
						SaveContainer(player, args);
						break;
					case "restore":
						RestoreContainer(player, args);
						break;
					default:
						SendMessage(player, mhelp);
						break;
				}
			}
			return true;
		}

		void RestoreContainer(IPlayer player, string[] args)
		{
			if(!player.HasPermission(PERMISSIONRESTORE))
			{
				SendMessage(player, mnoperms);
				return;
			}

			int num;
			if(args.Length < 2 || (!int.TryParse(args[1], out num) && args[1].ToLower() != "all"))
			{
				SendMessage(player, mhelp);
				return;
			}

			if(args[1].ToLower() == "all")
			{
				foreach(var container in ReplenishData)
				{
					container.Restore();

				}
				SendMessage(player, mrestoredall);
				return;
			}

			if(ReplenishData.Count < num)
			{
				SendMessage(player, mnocontainer);
				return;
			}

			ReplenishData[num - 1].Restore();
			SendMessage(player, mrestored, num, ReplenishData[num - 1].PositionVector);
		}

		void SaveContainer(IPlayer player, string[] args)
		{
			var hit = default(RaycastHit);

			if(!player.HasPermission(PERMISSIONSAVE))
			{
				SendMessage(player, mnoperms);
				return;
			}

			Physics.Raycast((player.Object as BasePlayer).eyes.HeadRay(), out hit, 10f);
			var ent = hit.GetEntity();
			if(ent == null || ent.GetComponent<StorageContainer>() == null)
			{
				SendMessage(player, mnoentity);
				return;
			}

			var scont = MakeSerializable(ent.GetEntity() as StorageContainer);

			if(args.Length >= 2)
			{
				switch(args[1].ToLower())
				{
					case "wipe":
						scont.RestoreOnWipe = true;
						break;
					case "restart":
						scont.RestoreOnRestart = true;
						break;
					case "timer":
						int time;
						if(args.Length < 3 || !int.TryParse(args[2], out time))
						{
							SendMessage(player, mhelp);
							return;
						}
						scont.AutorestoreTime = time;
						break;
					case "destroy":
						scont.RestoreOnDestroy = true;
						break;
					default:
						SendMessage(player, mhelp);
						return;
				}
			}

			ReplenishData.Add(scont);
			SavePluginData();
			SendMessage(player, msaved, ReplenishData.IndexOf(scont), scont.PositionVector.ToString());
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
					builder.AppendLine($"{ReplenishData.IndexOf(container) + 1} : {container.PositionVector.ToString().Replace("(", string.Empty).Replace(")", string.Empty)}\nRestore on {(container.RestoreOnWipe ? "wipe" : container.AutorestoreTime > 0 ? $"timer ({container.AutorestoreTime.ToString()}s)" : container.RestoreOnDestroy ? "destroy" : "request")}");
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
			else if(args.Length < 2 || !int.TryParse(args[1], out result))
			{
				SendMessage(player, mhelp);
			}
			else if(ReplenishData.Count < result)
			{
				SendMessage(player, mnocontainer);
			}
			else
			{
				var container = ReplenishData[result - 1];
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
			else if(ReplenishData.Count < result)
			{
				SendMessage(player, mnocontainer);
			}
			else
			{
				ReplenishData.RemoveAt(result - 1);

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
		private const string PERMISSIONSAVE = "replenish.save";
		private const string PERMISSIONRESTORE = "replenish.restore";

		private const string mhelp = "Help message";
		private const string mnoperms = "No permission";
		private const string mprefix = "Prefix";
		private const string mlist = "List of saved crates";
		private const string mcontainerinfo = "Information about specific container";
		private const string mnocontainer = "Wrong container ID";
		private const string mdeleted = "Deleted container";
		private const string msaved = "Saved container";
		private const string mrestored = "Restored container";
		private const string mrestoredall = "Restored all containers";

		private const string mnoentity = "No entity";

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
					[mdeleted] = "Deleted container with ID: {0}",
					[mnoentity] = "No suitable entity found, you must look directly at container",
					[msaved] = "Saved container {0} at {1}",
					[mrestored] = "Restored container {0} at {1}",
					[mrestoredall] = "All containers were restored"
				};
			}
		}

		private void SendMessage(IPlayer player, string key, params object[] args)
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