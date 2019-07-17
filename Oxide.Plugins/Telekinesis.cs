using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Telekinesis", "2CHEVSKII", "0.1.0")]
	internal class Telekinesis : RustPlugin
	{
		private const string PERMISSION = "telekinesis.use";

		private int button = (int)BUTTON.FIRE_THIRD;
		private int rotButton = (int)BUTTON.FIRE_SECONDARY;
		private static Telekinesis instance;
		private HashSet<BaseEntity> HeldItems;

		protected override void LoadDefaultConfig() { }

		private void Init()
		{
			instance = this;
			HeldItems = new HashSet<BaseEntity>();
			permission.RegisterPermission(PERMISSION, this);

			cmd.AddChatCommand("telekinesis", this, delegate (BasePlayer player, string command, string[] args)
			{
				CmdTelekinesis(player, command, args);
			});

			CheckConfig("Button", ref button);
			CheckConfig("Rotation button", ref rotButton);
			SaveConfig();
		}

		private void Unload()
		{
			foreach(BasePlayer player in BasePlayer.activePlayerList)
			{
				player.GetComponent<Telekinesys>()?.RemoveComponent();
			}
		}

		private void CheckConfig<T>(string key, ref T var)
		{
			if(Config[key] is T)
				var = (T)Config[key];
			else
				Config[key] = var;
		}


		#region cmd
		private bool CheckPermission(BasePlayer player)
		{
			if(player == null || player.UserIDString == null)
				return false;
			return permission.UserHasPermission(player.UserIDString, PERMISSION);
		}
		private void CmdTelekinesis(BasePlayer player, string command, string[] args)
		{
			if(!CheckPermission(player))
			{
				Reply(player, mnoperm);
				return;
			}

			Telekinesys comp = player.GetComponent<Telekinesys>();
			if(comp == null)
			{
				player.gameObject.AddComponent<Telekinesys>();
				Reply(player, mtoggledon);
			}
			else
			{
				comp.RemoveComponent();
				Reply(player, mtoggledoff);
			}
		}

		#endregion

		#region msg

		private void Reply(BasePlayer player, string key)
		{
			if(player != null)
			{
				player.ChatMessage($"{lang.GetMessage(mchatprefix, this, player?.UserIDString)} {lang.GetMessage(key, this, player?.UserIDString)}");
			}
		}

		protected override void LoadDefaultMessages() => lang.RegisterMessages(defmessages_en, this);

		private const string mnoperm = "No permission";
		private const string mcantlift = "Can't lift";
		private const string mcanttake = "Can't take";
		private const string mtoggledon = "ON";
		private const string mtoggledoff = "OFF";
		private const string mchatprefix = "Chat prefix";
		private readonly Dictionary<string, string> defmessages_en = new Dictionary<string, string>
		{
			{ mnoperm, "You are not allowed to use telekinesis!" },
			{ mcantlift, "This object is too heavy, you cannot lift it!" },
			{ mcanttake, "Other player holds this item very much, you cannot take it!" },
			{ mtoggledon, "Telekinesis has been toggled ON" },
			{ mtoggledoff, "Telekinesis has been toggled OFF" },
			{ mchatprefix, "Telekinesis:" }
		};

		#endregion



		private class Telekinesys : MonoBehaviour
		{
			private BasePlayer Player { get; set; }
			private BaseEntity CarriedItem { get; set; }

			private bool pressed = false;

			private void Awake()
			{
				Player = GetComponent<BasePlayer>();
				if(Player == null)
				{
					DestroyImmediate(this);
					return;
				}


			}

			private void FixedUpdate()
			{
				if(Player.serverInput.WasJustPressed((BUTTON)instance.button) && !pressed)
				{
					pressed = true;

					if(CarriedItem == null)
					{

						bool flag = TryFindObject(out BaseEntity ent);

						if(!flag)
						{
							Player.ChatMessage("No item");
							return;
						}

						CarriedItem = ent;
						instance.HeldItems.Add(ent);

						CarriedItem.GetComponent<Rigidbody>().isKinematic = true;

						CarriedItem.GetComponent<Rigidbody>().detectCollisions = false;
					}

				}
				if(Player.serverInput.WasJustReleased((BUTTON)instance.button) && pressed)
				{
					pressed = false;
					CarriedItem.GetComponent<Rigidbody>().isKinematic = false;

					CarriedItem.GetComponent<Rigidbody>().detectCollisions = true;

					CarriedItem.GetComponent<Rigidbody>().AddExplosionForce(500f, CarriedItem.transform.position + (Player.eyes.position - CarriedItem.transform.position) * 0.5f, 10f);

					instance.HeldItems.Remove(CarriedItem);

					CarriedItem = null;


				}


				if(CarriedItem != null)
				{
					if(!Player.serverInput.IsDown((BUTTON)instance.rotButton))
					{
						CarriedItem.transform.position = FindBestPosition();
					}
					else
					{
						CarriedItem.transform.rotation = Quaternion.Euler(CarriedItem.transform.rotation.eulerAngles.x, Player.eyes.rotation.eulerAngles.y * 3, CarriedItem.transform.rotation.eulerAngles.z);
					}
					CarriedItem.SendNetworkUpdateImmediate();
				}


			}

			private void OnDestroy()
			{
				CarriedItem = null;
				instance.HeldItems.Remove(CarriedItem);
			}

			private bool TryFindObject(out BaseEntity ent)
			{
				ent = null;
				RaycastHit raycastHit = new RaycastHit();

				if(!Physics.Raycast(ray: Player.eyes.HeadRay(), hitInfo: out raycastHit, maxDistance: 3f, layerMask: LayerMask.GetMask("Debris")))
					return false;
				ent = raycastHit.GetEntity();

				if(ent == null || !(ent is DroppedItem) || instance.HeldItems.Contains(ent))
					return false;

				return true;
			}

			private Vector3 FindBestPosition()
			{
				if(Physics.Raycast(Player.eyes.HeadRay(), hitInfo: out RaycastHit raycastHit, maxDistance: 1f))
				{
					return raycastHit.point + ((Player.eyes.position - raycastHit.point) * 0.2f);
				}
				else
				{
					return Player.eyes.position + /*(*/Player.eyes.HeadForward()/* * 0.4f) - new Vector3(0, 0.5f, 0)*/;
				}
			}

			public void RemoveComponent() => DestroyImmediate(this);
		}

	}
}
