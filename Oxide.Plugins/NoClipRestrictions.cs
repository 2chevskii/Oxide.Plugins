using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
	[Info("NoClip Restrictions", "2CHEVSKII", "0.1.1")]
	[Description("Blocks usage of certain commands and items as well as dealing and receiving damage while in noclip mode")]
	internal class NoClipRestrictions : CovalencePlugin
	{
		#region Fields


		private const string PERMISSIONBYPASS = "nocliprestrictions.bypass";

		private bool check_running;

		private PluginSettings Settings { get; set; }


		#endregion

		#region Configuration


		private class PluginSettings
		{
			[JsonProperty("Blocked items (player cannot set them active while nocliping)")]
			public List<string> BlockedItems { get; set; }

			[JsonProperty("Blocked commands (player cannot execute them while nocliping)")]
			public List<string> BlockedCommands { get; set; }

			[JsonProperty("Block outgoing damage")]
			public bool BlockOutDamage { get; set; }

			[JsonProperty("Block incoming damage")]
			public bool BlockInDamage { get; set; }
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				Settings = Config.ReadObject<PluginSettings>();
				if(Settings?.BlockedItems == null || Settings.BlockedCommands == null) throw new Exception();
			}
			catch
			{
				LoadDefaultConfig();
			}
		}

		protected override void LoadDefaultConfig()
		{
			Settings = new PluginSettings
			{
				BlockedItems = new List<string>
				{
					"rifle.ak"
				},
				BlockedCommands = new List<string>
				{
					"tpa"
				},
				BlockOutDamage = false,
				BlockInDamage = false
			};

			SaveConfig();
		}

		protected override void SaveConfig() => Config.WriteObject(Settings);


		#endregion

		#region LangAPI


		private readonly Dictionary<string, string> defaultmessages_en = new Dictionary<string, string>
		{
			[mprefix] = "NoClip restrictions: ",
			[mblockeditem] = "Item <color=#32a885>{0}</color> cannot be used while nocliping!",
			[mblockedcommand] = "Command '<color=#32a885>{0}</color>' cannot be used while nocliping!",
			[mblockedoutdmg] = "<color=#f7663e>You can't hurt anyone while nocliping!</color>",
			[mblockedindmg] = "<color=#f78b3e>You can't hurt person who is nocliping!</color>"
		};

		private const string mprefix = "Prefix",
							 mblockeditem = "Item blocked",
							 mblockedcommand = "Command blocked",
							 mblockedoutdmg = "Outgoing damage blocked",
							 mblockedindmg = "Incoming damage blocked",
							 mblockedusageitem = "Item usage blocked",
							 mblockedusageentity = "Entity usage blocked";

		protected override void LoadDefaultMessages() => lang.RegisterMessages(defaultmessages_en, this);

		private string GetLocalizedString(IPlayer player, string key, params object[] args) => string.Format(lang.GetMessage(key, this, player?.Id), args);

		private void SendMessageRaw(IPlayer player, string message) => player?.Message(message);

		private void SendMessage(IPlayer player, string key, params object[] args) => SendMessageRaw(player, GetLocalizedString(player, mprefix) + GetLocalizedString(player, key, args));

		private void SendMessage(BasePlayer player, string key, params object[] args) => SendMessage(player.IPlayer, key, args);


		#endregion

		#region Hooks


		private object OnPlayerAttack(BasePlayer attacker, HitInfo info)
		{
			if(attacker.IPlayer.HasPermission(PERMISSIONBYPASS)) return null;

			if(attacker.IsFlying && Settings.BlockOutDamage)
			{
				SendMessage(attacker, mblockedoutdmg);
				return true;
			}

			BasePlayer target = info?.HitEntity as BasePlayer;

			if(target == null) return null;

			if(!target.IsFlying || !Settings.BlockInDamage) return null;

			SendMessage(attacker, mblockedindmg);
			return true;

		}

		private void OnPlayerActiveItemChanged(BasePlayer player) => CheckHeld(player, true);

		private void OnServerInitialized() => timer.Every(10f, () =>
		{
			if(!check_running) ServerMgr.Instance.StartCoroutine(CheckRoutine());
		});

		

		//private object OnUserCommand(IPlayer player, string command)
		//{
		//	Puts($"OnUserCommand: {player.Name}\nCommand: {command}");
		//	BasePlayer basePlayer = player.Object as BasePlayer;

		//	if(basePlayer == null || !basePlayer.IsFlying || player.HasPermission(PERMISSIONBYPASS)) return null;

		//	if(Settings.BlockedCommands.Contains(command))
		//	{
		//		SendMessage(player, mblockedcommand, command);
		//		return true;
		//	}

		//	return null;
		//}

		object OnPlayerCommand(ConsoleSystem.Arg arg)
		{
			if(arg.cmd.Name != "say")
			{
				return null;
			}

			var player = arg.Player();
			if(player == null || !player.IsFlying || permission.UserHasPermission(player.UserIDString, PERMISSIONBYPASS))
			{
				return null;
			}
			var command = arg.Args?[0].TrimStart('/');
			if(!Settings.BlockedCommands.Contains(command))
			{
				return null;
			}

			SendMessage(player, mblockedcommand, command);
			return true;
		}


		#endregion

		#region Helpers


		private IEnumerator CheckRoutine()
		{
			check_running = true;
			foreach(BasePlayer player in BasePlayer.activePlayerList.Where(p => p.Connection.authLevel > 0))
			{
				CheckHeld(player);
				yield return new WaitForEndOfFrame();
			}

			check_running = false;
		}

		private void CheckHeld(BasePlayer player, bool message = false)
		{
			if(player == null || !player.IsFlying || player.IPlayer == null || player.IPlayer.HasPermission(PERMISSIONBYPASS)) return;

			Item item = player.GetActiveItem();
			HeldEntity ent = item?.GetHeldEntity() as HeldEntity;

			if(ent == null || !Settings.BlockedItems.Contains(item.info.shortname)) return;

			ent.SetHeld(false);
			if(message)
				SendMessage(player, mblockeditem, item.info.shortname);
		}


		#endregion

	}
}