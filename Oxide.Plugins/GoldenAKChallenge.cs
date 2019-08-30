using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Game.Rust;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
	[Info("Golden AK Challenge", "2CHEVSKII", "0.1.0")]
	internal class GoldenAKChallenge : RustPlugin
	{
		private const ulong SKIN = 1362212220;

		private const string PERMISSION = "goldenakchallenge.admin";

		private const string VENDINGPREFAB = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
		private const string GENERICPREFAB = "assets/prefabs/tools/map/genericradiusmarker.prefab";

		private static GoldenAKChallenge Plugin { get; set; }

		private AKEvent CurrentEvent { get; set; }








		#region API


		public bool StartGoldenAKChallenge() => StartGoldenAKChallenge(FindPointForEventSpawn(), Settings.MaxEventTime);

		public bool StartGoldenAKChallenge(float time) => StartGoldenAKChallenge(FindPointForEventSpawn(), time);

		public bool StartGoldenAKChallenge(Vector3 point) => StartGoldenAKChallenge(point, Settings.MaxEventTime);

		public bool StartGoldenAKChallenge(Vector3 point, float time)
		{
			if(CurrentEvent != null) return false;

			InitializeAkEvent(point, time);

			return true;
		}

		public bool StopGoldenAKChallenge()
		{
			if(CurrentEvent == null) return false;

			FinishAkEvent();

			return true;
		}

		public bool IsAkEventRunning() => CurrentEvent != null;


		#endregion










		private void InitializeAkEvent(Vector3 point, float time)
		{
			SpawnEventTrigger(point);
			CurrentEvent.FinishTime = Time.realtimeSinceStartup + Settings.MaxEventTime;
		}

		private void FinishAkEvent()
		{
			DeleteEventTrigger();
			
		}







		private void SpawnEventTrigger(Vector3 point) => ItemManager.CreateByName("rifle.ak", 1, SKIN).CreateWorldObject(point).gameObject.AddComponent<AKEvent>();

		private void DeleteEventTrigger() => CurrentEvent.Entity.Kill();














		private class AKEvent : MonoBehaviour
		{

			public BaseEntity Entity { get; private set; }

			public float FinishTime { get; set; }

			private Dictionary<BasePlayer, float> Capturers { get; } = new Dictionary<BasePlayer, float>();

			private VendingMachineMapMarker textMarker;
			private MapMarkerGenericRadius marker;

			private void Awake()
			{
				Entity = GetComponent<BaseEntity>();
				Entity.gameObject.layer = (int)Layer.Reserved1;

				Plugin.CurrentEvent = this;

				SetupPosition();
				SetupMarkers();
				SetupTrigger();
			}

			void Start()
			{
				Plugin.AnnounceInChat(MSTARTED);

				InvokeRepeating("DoRotation", 0.1f, 0.1f);
				InvokeRepeating("PrintStats", 1f, 1f);
			}

			private void OnTriggerEnter(Collider col)
			{
				if(col.gameObject.layer != 17) return;

				BasePlayer player = col.gameObject.GetComponent<BasePlayer>();

				if(player == null || Capturers.ContainsKey(player)) return;

				Capturers.Add(player, 0f);
				Plugin.Puts($"Player {player.displayName} entered capture area");

				//draw ui
			}

			private void OnTriggerExit(Collider col)
			{
				if(col.gameObject.layer != 17) return;

				BasePlayer player = col.gameObject.GetComponent<BasePlayer>();

				if(player == null || !Capturers.ContainsKey(player)) return;

				Capturers.Remove(player);
				Plugin.Puts($"Player {player.displayName} leaved the area");

				//destroy ui
			}

			private List<BasePlayer> listToRemove = new List<BasePlayer>();
			private List<BasePlayer> listToIncrease = new List<BasePlayer>();

			void FixedUpdate()
			{
				if(FinishTime <= Time.realtimeSinceStartup)
				{
					Plugin.FinishAkEvent();
				}

				var winner = Capturers.FirstOrDefault(x => x.Value >= Plugin.Settings.TimeToCapture).Key;

				if(winner != null)
				{
					Plugin.AnnounceInChat(MCAPTURED, winner.displayName); //гдеяэ FORMAT EXCEPTION
					Plugin.FinishAkEvent();
				}
			}

			void Update()
			{
				listToRemove.Clear();
				listToIncrease.Clear();

				foreach(var key in Capturers.Keys)
				{
					if(key == null || key.IsDead() || !key.IsConnected)
					{
						listToRemove.Add(key);
					}
					else
					{
						listToIncrease.Add(key);
					}
				}
			}

			void LateUpdate()
			{
				foreach(var player in listToRemove)
				{
					Capturers.Remove(player);
					var vector = Entity.transform.position + new Vector3(Random.Range(2f, 5f), 0f, Random.Range(2f, 5f));
					vector.y = TerrainMeta.HeightMap.GetHeight(vector);
					player?.MovePosition(vector);
				}

				foreach(var player in listToIncrease)
				{
					Capturers[player] += Time.deltaTime;
				}
			}


			void PrintStats()
			{
				Plugin.Puts(JsonConvert.SerializeObject(Capturers.Select(x => new KeyValuePair<string, float>(x.Key.displayName, x.Value)), Formatting.Indented));
			}


			private void OnDestroy()
			{
				CancelInvoke();

				DestroyMarkers();

				Plugin.AnnounceInChat(MFINISHED);
			}


			#region Pos setup


			private void SetupPosition()
			{
				Entity.transform.position = Entity.transform.position + new Vector3(0, 1.5f, 0);
				Entity.transform.rotation = Quaternion.Euler(90, 0, 0);
				Entity.transform.hasChanged = true;
				Entity.SendNetworkUpdateImmediate();
			}

			private void SetupTrigger()
			{
				Rigidbody rbody = Entity.GetComponent<Rigidbody>();

				rbody.isKinematic = true;
				rbody.useGravity = false;
				rbody.detectCollisions = true;
				rbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

				SphereCollider collider = gameObject.AddComponent<SphereCollider>();
				collider.isTrigger = true;
				collider.radius = 1.5f;
			}

			private void DoRotation() => Entity.transform.Rotate(new Vector3(0, 0, 2f));


			#endregion

			#region Markers


			void SetupMarkers()
			{
				textMarker = GameManager.server.CreateEntity(VENDINGPREFAB, Entity.transform.position) as VendingMachineMapMarker;
				marker = GameManager.server.CreateEntity(GENERICPREFAB, Entity.transform.position) as MapMarkerGenericRadius;

				textMarker.markerShopName = $"Golden AK";
				textMarker.Spawn();

				marker.radius = 0.4f;
				marker.color1 = Color.yellow;
				marker.color2 = Color.black;
				marker.alpha = 1f;
				marker.Spawn();
				marker.SendUpdate();
			}

			void DestroyMarkers()
			{
				foreach(var player in BasePlayer.activePlayerList)
				{
					textMarker.DestroyOnClient(player.Connection);
					marker.DestroyOnClient(player.Connection);
				}

				textMarker.Kill();
				marker.Kill();

				textMarker = null;
				marker = null;
			}


			#endregion

			#region UI


			//void BuildCurrentUI()
			//{
			//	UI.Clear();

			//	if(captureState == CaptureState.NoCapturer)
			//		return;

			//	UI.Add(new CuiPanel {
			//		CursorEnabled = false,
			//		Image = { Color = "0.5 0.5 0.5 0.8", FadeIn = 0.3f, Material = "assets/content/ui/namefontmaterial.mat" },
			//		FadeOut = 0.3f,
			//		RectTransform = { AnchorMax = "0.5 0.8", AnchorMin = "0.5 0.8", OffsetMax = "100 30", OffsetMin = "-100 -30" }
			//	}, "Hud", "gakchallenge.main");

			//	if(captureState == CaptureState.Capturing)
			//	{
			//		UI.Add(new CuiPanel {
			//			CursorEnabled = false,
			//			Image = { Color = "0.4 0.8 0.5 0.5", FadeIn = 0.3f, Material = "assets/content/ui/namefontmaterial.mat" },
			//			FadeOut = 0.3f,
			//			RectTransform = {
			//				AnchorMax = "0.5 0.5",
			//				AnchorMin = "0.5 0.5",
			//				OffsetMax = $"{(captureState == CaptureState.Concurrent ? 0f : lastCaptureTime / Plugin.Settings.TimeToCapture * 10f).ToString("0")}",
			//				OffsetMin = $"{(captureState == CaptureState.Concurrent ? 0f : -(lastCaptureTime / Plugin.Settings.TimeToCapture * 10f)).ToString("0")}"
			//			}
			//		}, "gakchallenge.main", "gakchallenge.capturepanel");
			//	}

			//	UI.Add(new CuiElement {
			//		Parent = "gakchallenge.main",
			//		Name = "gakchallenge.text",
			//		Components = {
			//			new CuiTextComponent {
			//				Text = captureState == CaptureState.Concurrent ? "Concurrent" : "Capturing..."
			//			},
			//			new CuiOutlineComponent {
			//				Color = "0 0 0 1",
			//				Distance = "-1.0 1.0",
			//				UseGraphicAlpha = true
			//			},
			//			new CuiRectTransformComponent {
			//				AnchorMax = "0.5 0.5",
			//				AnchorMin = "0.5 0.5",
			//				OffsetMax = "100 30",
			//				OffsetMin = "-100 -30"
			//			}
			//		}
			//	});
			//}


			#endregion
		}

		void GiveRewards(BasePlayer player)
		{
			var item = ItemManager.CreateByName(Settings.Shortname, Settings.Amount, Settings.Skin);

			if(item != null)
			{
				player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
				//announce
			}
		}



		#region Command


		private void CommandHandler(BasePlayer player, string command, string[] args)
		{
			if(!CheckPermission(player))
			{
				MessagePlayer(player, MNOPERMISSION);

				return;
			}

			if(args.Length != 1)
			{
				MessagePlayer(player, MINVALIDARGS);

				return;
			}

			switch(args[0].ToLower())
			{
				case "start":
					if(!StartGoldenAKChallenge()) MessagePlayer(player, MALREADYSTARTED);

					break;

				case "stop":
					if(!StopGoldenAKChallenge()) MessagePlayer(player, MNOTSTARTED);

					break;
			}
		}

		private bool CommandHandler(ConsoleSystem.Arg arg)
		{
			BasePlayer player = arg.Player();

			if(player != null && !CheckPermission(player))
			{
				MessageConsole(arg, MNOPERMISSION);

				return false;
			}

			if(arg.Args == null || arg.Args.Length != 1)
			{
				MessageConsole(arg, MINVALIDARGS);

				return false;
			}

			switch(arg.Args[0].ToLower())
			{
				case "start":
					if(!StartGoldenAKChallenge()) MessageConsole(arg, MALREADYSTARTED);

					break;

				case "stop":
					if(!StopGoldenAKChallenge()) MessageConsole(arg, MNOTSTARTED);

					break;
			}

			return true;
		}


		#endregion

		#region Config


		private PluginSettings Settings { get; set; }

		protected override void LoadConfig()
		{
			base.LoadConfig();

			try
			{
				Puts("Loading configuration...");
				Settings = Config.ReadObject<PluginSettings>();

				if(Settings == null) throw new JsonException("Error occured while loading configuration!");
			}
			catch
			{
				LoadDefaultConfig();
			}
		}

		protected override void LoadDefaultConfig()
		{
			Settings = DefaultSettings;
			PrintWarning("Default configuration created.");
			SaveConfig();
		}

		protected override void SaveConfig()
		{
			Config.WriteObject(Settings, true);

			Puts("Configuration saved.");
		}

		private PluginSettings DefaultSettings => new PluginSettings {
			TimeToCapture = 60f,
			AutoStartEventInterval = 0f,
			MaxEventTime = 100f,
			Shortname = "",
			Amount = 1,
			Skin = 0
		};

		private class PluginSettings
		{
			[JsonProperty("Time needed to capture the objective (seconds)")]
			public float TimeToCapture { get; set; }

			[JsonProperty("Event auto-start interval (seconds)")]
			public float AutoStartEventInterval { get; set; }

			[JsonProperty("Event auto-finish time (seconds)")]
			public float MaxEventTime { get; set; }

			[JsonProperty("Shortname of reward item")]
			public string Shortname { get; set; }

			[JsonProperty("Amount of reward item")]
			public int Amount { get; set; }

			[JsonProperty("Skin of reward item")]
			public ulong Skin { get; set; }

			[JsonProperty("Broadcast changes in chat")]
			public bool Broadcast { get; set; }
		}


		#endregion

		#region Helpers


		private bool CheckPermission(BasePlayer player) => permission.UserHasPermission(player.UserIDString, PERMISSION);

		private Vector3 FindPointForEventSpawn()
		{
			List<Vector3> list = FindPotentialPointsForEventSpawn();

			Dictionary<Vector3, float> distancesToPlayers = new Dictionary<Vector3, float>();

			foreach(Vector3 vector in list)
			{
				BasePlayer closestPlayer = BasePlayer.activePlayerList.OrderByDescending(player => Vector3.Distance(player.transform.position, vector)).First();

				distancesToPlayers.Add(vector, Vector3.Distance(vector, closestPlayer.transform.position));
			}

			Vector3 bestpoint = distancesToPlayers.OrderByDescending(x => x.Value).First().Key;

			return bestpoint;
		}

		private List<Vector3> FindPotentialPointsForEventSpawn(int iterations = 50)
		{
			List<Vector3> list = new List<Vector3>();

			for(int i = 0; i < iterations; i++)
			{
				Vector3 vector = new Vector3(Random.Range(-World.Size * 0.5f, World.Size * 0.5f), 0, Random.Range(-World.Size * 0.5f, World.Size * 0.5f));
				vector.y = TerrainMeta.HeightMap.GetHeight(vector);

				if(vector.y <= 0f)
				{
					i--;

					continue;
				}

				list.Add(vector);
			}

			return list;
		}


		#endregion

		#region Oxide Hooks


		private void Init()
		{
			Plugin = this;

			permission.RegisterPermission(PERMISSION, this);

			cmd.AddChatCommand("gac", this, CommandHandler);
			cmd.AddConsoleCommand("gac", this, CommandHandler);
		}

		private object OnItemPickup(Item item, BasePlayer player) => item?.GetWorldEntity()?.GetComponent<AKEvent>() != null ? (object)true : null;

		private void Unload() => BaseNetworkable.serverEntities.OfType<DroppedItem>().ToList().ForEach(x => x.Kill());


		#endregion

		#region Lang


		private const string MPREFIX         = "Prefix",
		                     MSTARTED        = "Event started",
		                     MFINISHED       = "Event finished",
		                     MSTARTEDCAPTURE = "Player started capture",
		                     MSTOPPEDCAPTURE = "Player stopped capture",
		                     MCAPTURED       = "Player has captured objective",
		                     MKILLED         = "Player was killed while capturing",
		                     MCANNOTPICKUP   = "Can't pickup",
		                     MNOPERMISSION   = "No permission",
		                     MCANNOTCAPTURE  = "Can't capture",
		                     MINVALIDARGS    = "Wrong command usage",
		                     MALREADYSTARTED = "Event already started",
		                     MNOTSTARTED     = "No active event";

		private Dictionary<string, string> DefMessagesEn => new Dictionary<string, string> {
			[MPREFIX] = "Golden AK Challenge",
			[MSTARTED] = "Event Started! Check your map to find position of the objective.",
			[MFINISHED] = "Event finished!",
			[MSTARTEDCAPTURE] = "Player {0} is capturing the AK!",
			[MSTOPPEDCAPTURE] = "Player {0} has stopped capturing the AK.",
			[MCAPTURED] = "Player {0} has captured the AK and received!",
			[MKILLED] = "Player {0} was killed while capturing the AK!",
			[MCANNOTPICKUP] = "You cannot pickup the event item.",
			[MNOPERMISSION] = "You are not allowed to do this.",
			[MCANNOTCAPTURE] = "You cannot capture the objective, player {0} is already capturing it!",
			[MINVALIDARGS] = "Wrong arguments provided!"
			                 + "\n/gac <start/stop>",
			[MALREADYSTARTED] = "There is an active event already, finish this before starting a new one!",
			[MNOTSTARTED] = "There are not active events yet!"
		};

		protected override void LoadDefaultMessages() => lang.RegisterMessages(DefMessagesEn, this);

		private void AnnounceInChat(string msg, params object[] args) =>
			Server.Broadcast(string.Format(lang
				                               .GetMessage(MPREFIX, this, lang.GetServerLanguage()) + " " + lang
				                               .GetMessage(msg, this, lang.GetServerLanguage()), args));

		private void MessagePlayer(BasePlayer player, string key, params object[] args) =>
			player.ChatMessage(string.Format(lang
				                                 .GetMessage(MPREFIX, this, player.UserIDString) + " " + lang
				                                 .GetMessage(key, this, player.UserIDString), args));

		private void MessageConsole(ConsoleSystem.Arg arg, string key, params object[] args) =>
			arg.ReplyWith(string.Format(lang
				                            .GetMessage(MPREFIX, this, arg.Player()?.UserIDString) + " " + lang
				                            .GetMessage(key, this, arg.Player()?.UserIDString), args));


		#endregion

		// TODO: MAke API
	}
}