using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
	[Info("Teleport Gun", "2CHEVSKII", "0.1.2")]
	[Description("Shoot something to teleport to it!")]
	internal class TeleportGun : RustPlugin
	{

		#region -Hooks-


		private void Init()
		{
			permission.RegisterPermission(perm, this);
			LoadConfigVariables();
		}

		protected override void LoadDefaultConfig() { }

		protected override void LoadDefaultMessages() => lang.RegisterMessages(defaultMessages, this, "en");

		private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
		{
			if(enabledplayers.Keys.Contains(attacker) && info.Weapon.GetItem() == enabledplayers[attacker])
			{
				attacker.MovePosition(info.HitPositionWorld);
			}
		}

		private void OnPlayerActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
		{
			if(enabledplayers.ContainsKey(player) && autodisable)
			{
				enabledplayers.Remove(player);
				Replier(player, disabled);
			}
		}
		

		#endregion

		#region -Fields-


		private bool autodisable = true;
		private int autodisabletimer = 0;
		private const string perm = "teleportgun.use";
		private const string prefix = "Prefix";
		private const string enabled = "Enabled";
		private const string disabled = "Disabled";
		private const string wrongitem = "Wrong item";
		private const string nopermission = "No permission";
		private Dictionary<string, string> defaultMessages = new Dictionary<string, string> {
			[prefix] = "<color=yellow>[</color>TELEPORT GUN<color=yellow>]</color>",
			[enabled] = "<color=#47FF11>E</color>nabled teleport gun<color=#47FF11>.</color>",
			[disabled] = "<color=red>D</color>isabled teleport gun<color=red>.</color>",
			[nopermission] = "<color=red>Y</color>ou have no permission to use teleport gun<color=red>!</color>",
			[wrongitem] = "<color=yellow>A</color>ctive item must be a gun<color=yellow>!</color>"
		};
		private Dictionary<BasePlayer, Item> enabledplayers = new Dictionary<BasePlayer, Item>();


		#endregion

		#region -Command-


		[ChatCommand("tpgun")]
		private void CmdSwitchGun(BasePlayer player, string command, string[] args)
		{
			if(permission.UserHasPermission(player.UserIDString, perm))
			{
				if(!enabledplayers.ContainsKey(player) && player.GetHeldEntity() is BaseProjectile)
				{
					enabledplayers.Add(player, player.GetHeldEntity().GetItem());
					if(autodisabletimer > 0)
						timer.Once(Convert.ToSingle(autodisabletimer), () =>
						{
							if(enabledplayers.ContainsKey(player))
							{
								enabledplayers.Remove(player);
								Replier(player, disabled);
							}
						});
					Replier(player, enabled);
				}
				else if(!enabledplayers.ContainsKey(player) && !(player.GetHeldEntity() is BaseProjectile))
				{
					Replier(player, wrongitem);
				}
				else
				{
					enabledplayers.Remove(player);
					Replier(player, disabled);
				}
			}
			else
			{
				Replier(player, nopermission);
			}
		}


		#endregion

		#region -Helpers-


		private void LoadConfigVariables()
		{
			CheckConfig("Auto disable when active item changed", ref autodisable);
			CheckConfig("Auto disable after timer (seconds)", ref autodisabletimer);
			SaveConfig();
		}

		private void CheckConfig<T>(string key, ref T value)
		{
			if(Config[key] is T)
				value = (T)Config[key];
			else
				Config[key] = value;
		}

		private void Replier(BasePlayer player, string message) => SendReply(player, $"{lang.GetMessage(prefix, this, player.UserIDString)} {lang.GetMessage(message, this, player.UserIDString)}");


		#endregion

	}
}
