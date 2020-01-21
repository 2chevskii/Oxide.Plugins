using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Where is My Horse", "2CHEVSKII", "0.2.2")]
	[Description("Here is your horse, sir!")]
	internal class WhereIsMyHorse : RustPlugin
	{
		#region -uMod Hooks-


		private void Init()
		{
			permission.RegisterPermission("whereismyhorse.use", this);
			permission.RegisterPermission("whereismyhorse.nocooldown", this);
			cmd.AddChatCommand("horse", this, "SpawnHorse");
		}


		#endregion

		#region -Core-


		private void SpawnHorse(BasePlayer player, string command, string[] args)
		{
			object escape = !useNoEscape ? null : Interface.CallHook("IsEscapeBlocked", player.UserIDString);
			if(!Cooldowns.ContainsKey(player.userID)) Cooldowns.Add(player.userID, -1f);
			RaycastHit hitPoint = default(RaycastHit);

			if(!permission.UserHasPermission(player.UserIDString, "whereismyhorse.use"))
			{
				MessagePlayer(player, "No permission");
			}
			else if(!player.IsOutside() && !allowInside)
			{
				MessagePlayer(player, "Can't spawn indoors");
			}
			else if(!Physics.Raycast(player.eyes.HeadRay(), out hitPoint, 20f))
			{
				MessagePlayer(player, "No point for spawn");
			}
			else if((int)(Time.realtimeSinceStartup - Cooldowns[player.userID]) < cooldown && !permission.UserHasPermission(player.UserIDString, "whereismyhorse.nocooldown"))
			{
				MessagePlayer(player, "Cooldown");
			}
			else if(escape != null && (bool)escape == true)
			{
				MessagePlayer(player, "NoEscape");
			}
			else
			{
				List<RidableHorse> list = new List<RidableHorse>();
				Vis.Entities(hitPoint.point, 3f, list);

				if(list.Count > 0)
				{
					MessagePlayer(player, "Horse nearby");

					return;
				}

				Vector3 vector = new Vector3();

				vector = hitPoint.point;
				vector.y = TerrainMeta.HeightMap.GetHeight(vector);

				BaseEntity horse = GameManager.server.CreateEntity("assets/rust.ai/nextai/testridablehorse.prefab", vector);

				if(horse)
				{
					horse.Spawn();
					MessagePlayer(player, "Spawned");
					Cooldowns[player.userID] = Time.realtimeSinceStartup;
				}
				else
				{
					MessagePlayer(player, "NRE");
				}
			}
		}


		#endregion

		#region -Constants and global variables-


		private const string PERMISSIONUSE  = "whereismyhorse.use";
		private const string PERMISSIONNOCD = "whereismyhorse.nocooldown";

		private bool allowInside = false;
		private int  cooldown    = 300;
		private bool useNoEscape = false;

		private Dictionary<ulong, float> Cooldowns { get; } = new Dictionary<ulong, float>();


		#endregion

		#region -Configuration-


		protected override void LoadDefaultConfig() => PrintWarning("Default configuration has been loaded...");

		protected override void LoadConfig()
		{
			base.LoadConfig();
			CheckCFG("Allow spawning horses inside the building", ref allowInside);
			CheckCFG("Cooldown on usage", ref cooldown);
			CheckCFG("Use NoEscape API", ref useNoEscape);
			SaveConfig();
		}

		private void CheckCFG<T>(string key, ref T var)
		{
			if(Config[key] is T) var = (T)Config[key];
			else Config[key] = var;
		}


		#endregion

		#region -LangAPI-


		private Dictionary<string, string> DefaultMessages_EN { get; } = new Dictionary<string, string> {
			{ "No permission", "You have no access to that command." },
			{ "No point for spawn", "No raycast hit! Look at something." },
			{ "Can't spawn indoors", "You can use that command only when outside!" },
			{ "Spawned", "Your horse has been spawned, sir! Don't forget to feed it!" },
			{ "Cooldown", "You have called you horse recently, wait a bit, please." },
			{ "Prefix", "[WHERE IS MY HORSE]" },
			{ "NoEscape", "Can't use command while escape blocked!" },
			{ "NRE", "Could not spawn a horse, it's null. Maybe next time?" },
			{ "Horse nearby", "There is a horse very close, consider using it instead." }
		};

		private void MessagePlayer(BasePlayer player, string message) => player?.ChatMessage($"{lang.GetMessage("Prefix", this, player.UserIDString)} {lang.GetMessage(message, this, player.UserIDString)}");

		protected override void LoadDefaultMessages() => lang.RegisterMessages(DefaultMessages_EN, this, "en");


		#endregion
	}
}