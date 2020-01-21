using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("DVACHEVSKIIDebugPlugin", "2CHEVSKII", 0.1)]
	[Description("Plugin which contains stuff to help me develop other plugins")]
	internal class DVACHEVSKIIDebugPlugin : RustPlugin
	{
		private void OnTick()
		{
			if(TOD_Sky.Instance != null)
			{
				TOD_Sky.Instance.Cycle.Hour = 9;
			}
		}

		private object Check2chevskii(BasePlayer player)
		{
			if(player.userID == 76561198049067915)
				return null;
			else
			{
				SendReply(player, "<color=red>You are not allowed to use debug commands!</color>");
				return false;
			}
		}

		[ChatCommand("condition")]
		private void CmdGetCondition(BasePlayer player)
		{
			if(Check2chevskii(player) != null)
				return;
			Item item = player?.GetActiveItem();
			if(item != null)
				SendReply(player, $"{item.info.displayName.english} condition: {item.conditionNormalized * 100}%");
			else
				SendReply(player, "You do not hold any item right now!");
		}

		[ConsoleCommand("rpclist")]
		private void PrintRPCList(ConsoleSystem.Arg arg)
		{
			IEnumerable<KeyValuePair<string, uint>> dic = StringPool.toNumber.Where(x => x.Key.Contains("rpc"));
			arg.ReplyWith(JsonConvert.SerializeObject(dic, Formatting.Indented));
		}

		[ChatCommand("healme")]
		private void CmdHealMe(BasePlayer player, string command, string[] args)
		{
			if(Check2chevskii(player) != null)
				return;
			player.health = player.MaxHealth();
			player.metabolism.calories.value = player.metabolism.calories.max;
			player.metabolism.hydration.value = player.metabolism.hydration.max;
			player.metabolism.bleeding.value = 0f;
			player.metabolism.radiation_poison.value = player.metabolism.radiation_poison.min;
			player.metabolism.radiation_level.value = player.metabolism.radiation_level.min;
		}

		private string GetInheritanceTree(Type type)
		{
			List<string> list = new List<string>();
			while(type.BaseType != null)
			{
<<<<<<< HEAD
				list.Add(type.Name);
				type = type.BaseType;
=======
                RaycastHit raycastHit;
                if(Physics.Raycast(player.eyes.HeadRay(), out raycastHit))
                    SendReply(player, $"Prefab: {raycastHit.GetEntity()?.PrefabName}\nType: {raycastHit.GetEntity()?.GetType().ToString()}" +
                        $"\nLayer: {raycastHit.collider.gameObject.layer}");
>>>>>>> master
			}
			return string.Join(" : ", list);
		}

		private string GetAllComponents(Component obj)
		{
			if(obj == null)
			{
				return "No components";
			}
			IEnumerable<string> list = from c in obj.GetComponents<Component>() select c.GetType().Name;
			return string.Join(", ", list);
		}

		private bool GetLookingObject(BasePlayer player, out UnityEngine.Object obj)
		{
			if(Physics.Raycast(ray: player.eyes.HeadRay(), maxDistance: 5f, layerMask: -1, hitInfo: out RaycastHit hit))
			{
				obj = hit.GetEntity();
				return true;
			}
			else
			{
				obj = null;
				return false;
			}
		}



		[ChatCommand("obj")]
		private void GetObjectInfo(BasePlayer player, string command, string[] args)
		{
			if(!GetLookingObject(player, out UnityEngine.Object obj))
			{
				player.ChatMessage("No object");
			}
			else
			{
				string inheritance = GetInheritanceTree(obj.GetType());
				string components = GetAllComponents(obj as Component);

				player.ChatMessage($@"
Name: {obj.name};
---------------------------
Inheritance: {inheritance};
---------------------------
Components: {components};
---------------------------
Owner: {(obj as BaseEntity)?.OwnerID}
");
			}
		}

		private void GetObjectInfo(UnityEngine.Object obj)
		{

			string inheritance = GetInheritanceTree(obj.GetType());
			string components = GetAllComponents(obj as Component);

			BasePlayer.Find("2CHEVSKII").ChatMessage($@"
Name: {obj.name};
---------------------------
Inheritance: {inheritance};
---------------------------
Components: {components};
---------------------------
Owner: {(obj as BaseEntity)?.OwnerID}
");
		}
	}

}
/* FONTS:
 * assets/content/ui/fonts/droidsansmono.ttf
 * assets/content/ui/fonts/permanentmarker.ttf
 * assets/content/ui/fonts/robotocondensed-bold.ttf
 * assets/content/ui/fonts/robotocondensed-regular.ttf */

/* MATERIALS:
 * assets/content/ui/uibackgroundblur-ingamemenu.mat
 * assets/content/ui/uibackgroundblur-notice.mat
 * assets/content/ui/uibackgroundblur.mat */
