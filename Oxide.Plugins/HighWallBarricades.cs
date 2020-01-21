using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using Oxide.Core;

using Rust;

using UnityEngine;

using Random = UnityEngine.Random;

namespace Oxide.Plugins
{

	[Info("HighWallBarricades", "Guilty Spark & 2CHEVSKII", "2.3.0")]
	[Description("Makes High External Walls decay.")]
	internal class HighWallBarricades : RustPlugin
	{

		#region -Component-


		private class BarricadeDecay : FacepunchBehaviour
		{

			private float defaultProtection;
			private float lastTickTime;

			private DecayEntity Barricade { get; set; }

			void Start()
			{
				Barricade = GetComponent<DecayEntity>();

				if(Singleton.Settings.InitialHealth <= 0f)
				{
					Invoke(() => Barricade.Kill(BaseNetworkable.DestroyMode.Gib), 0.5f);

					return;

				}

				UpdateTickTime();

				defaultProtection = Barricade.baseProtection.Get(DamageType.Decay);

				if(Singleton.Settings.DisableStandartDecay)
					Barricade.baseProtection.amounts[(int)DamageType.Decay] = 100;

				Singleton.Barricades.Add(this);

				InvokeRandomized(CheckDecay, 1f, 0f, Random.Range(15f, 45f));
			}

			private void CheckDecay()
			{

				if(Barricade.GetBuildingPrivilege() == null)
				{
					TickDecay();

					return;
				}

				if(!Singleton.Settings.DecayInCupRange)
				{
					UpdateTickTime();

					return;
				}

				if(!Singleton.Settings.CheckBarricadeOwner)
				{
					TickDecay();

					return;
				}

				if(!Barricade.GetBuildingPrivilege().authorizedPlayers.Any(x => x.userid == Barricade.OwnerID))
				{
					TickDecay();

					return;
				}

				UpdateTickTime();

			}

			public void RemoveComponent() => DestroyImmediate(this);

			private void UpdateTickTime() => lastTickTime = Time.realtimeSinceStartup;

			private void TickDecay()
			{

				if(Time.realtimeSinceStartup - lastTickTime < Singleton.Settings.DecayTime) return;

				Barricade.healthFraction -= Singleton.Settings.DecayDamage * Mathf.FloorToInt((Time.realtimeSinceStartup - lastTickTime) / Singleton.Settings.DecayTime);

				if(Barricade.healthFraction <= 0) Invoke(() => Barricade.Kill(BaseNetworkable.DestroyMode.Gib), 0.5f);

				Barricade.SendNetworkUpdate();
				UpdateTickTime();
			}

			private void OnDestroy()
			{
				ServerMgr.Instance.Invoke(() =>
				{
					if(Singleton.Barricades.Contains(this))
					{
						Singleton.Barricades.Remove(this);
					}
				}, 1f);
				Barricade.baseProtection.amounts[(int)DamageType.Decay] = defaultProtection;
			}

		}


		#endregion

		#region -Fields-


		private Configuration Settings { get; set; }

		private static HighWallBarricades Singleton { get; set; }

		private HashSet<BarricadeDecay> Barricades { get; set; }


		#endregion

		#region -Configuration-


		private class Configuration
		{

			[JsonProperty(PropertyName = "Time between decay ticks")]
			internal int DecayTime { get; set; }

			[JsonProperty(PropertyName = "Decay tick damage")]
			internal float DecayDamage { get; set; }

			[JsonProperty(PropertyName = "Barricade initial health")]
			internal float InitialHealth { get; set; }

			[JsonProperty(PropertyName = "Should barricades decay while inside the cupboard range")]
			internal bool DecayInCupRange { get; set; }

			[JsonProperty(PropertyName = "Check if owner of barricade is authorized")]
			internal bool CheckBarricadeOwner { get; set; }

			[JsonProperty(PropertyName = "Disable standart decay for barricades")]
			internal bool DisableStandartDecay { get; set; }

			[JsonProperty(PropertyName = "Enabled types of barricades")]
			internal Dictionary<string, bool> EnabledEntities { get; set; }

			[JsonProperty(PropertyName = "Configuration version (Needed for auto-update, don't modify)")]
			internal VersionNumber ConfigVersion { get; set; }

		}

		private Configuration GetDefaultConfig() =>
			new Configuration {
				DecayTime = 600,
				DecayDamage = 0.2f,
				InitialHealth = 1.0f,
				DecayInCupRange = false,
				CheckBarricadeOwner = true,
				DisableStandartDecay = false,
				EnabledEntities = new Dictionary<string, bool> {
					{"assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab", true},
					{"assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab", true},
					{"assets/prefabs/deployable/barricades/barricade.concrete.prefab", true},
					{"assets/prefabs/deployable/barricades/barricade.metal.prefab", true},
					{"assets/prefabs/deployable/barricades/barricade.sandbags.prefab", true},
					{"assets/prefabs/deployable/barricades/barricade.stone.prefab", true},
					{"assets/prefabs/deployable/barricades/barricade.wood.prefab", true},
					{"assets/prefabs/deployable/barricades/barricade.woodwire.prefab", true},
					{"assets/prefabs/building/gates.external.high/gates.external.high.stone/gates.external.high.stone.prefab", true},
					{"assets/prefabs/building/gates.external.high/gates.external.high.wood/gates.external.high.wood.prefab", true}
				},
				ConfigVersion = Version
			};

		protected override void LoadConfig()
		{
			base.LoadConfig();

			try
			{
				Settings = Config.ReadObject<Configuration>();

				if(Settings == null)
					throw new JsonException("Configuration failed to load, creating new one!");
			}
			catch
			{
				Config.Clear();
				Settings = GetDefaultConfig();
				SaveConfig();
			}

			if(Settings.ConfigVersion < Version)
			{
				if(Settings.ConfigVersion == new VersionNumber(2, 1, 0))
				{
					Configuration tempconfig = new Configuration {
						DecayTime = Settings.DecayTime,
						DecayDamage = Settings.DecayDamage,
						InitialHealth = Settings.InitialHealth,
						DecayInCupRange = Settings.DecayInCupRange,
						CheckBarricadeOwner = true,
						DisableStandartDecay = Settings.DisableStandartDecay,
						EnabledEntities = Settings.EnabledEntities
					};
					Settings = tempconfig;
				}

				Settings.ConfigVersion = Version;
				SaveConfig();
				Puts("Configuration has been updated!");
			}

			if(Settings.EnabledEntities == null)
			{
				Settings.EnabledEntities = new Dictionary<string, bool> {
					{"assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab", true},
					{"assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab", true},
					{"assets/prefabs/deployable/barricades/barricade.concrete.prefab", true},
					{"assets/prefabs/deployable/barricades/barricade.metal.prefab", true},
					{"assets/prefabs/deployable/barricades/barricade.sandbags.prefab", true},
					{"assets/prefabs/deployable/barricades/barricade.stone.prefab", true},
					{"assets/prefabs/deployable/barricades/barricade.wood.prefab", true},
					{"assets/prefabs/deployable/barricades/barricade.woodwire.prefab", true},
					{"assets/prefabs/building/gates.external.high/gates.external.high.stone/gates.external.high.stone.prefab", true},
					{"assets/prefabs/building/gates.external.high/gates.external.high.wood/gates.external.high.wood.prefab", true}
				};
				SaveConfig();
			}
		}

		protected override void LoadDefaultConfig()
		{
			Settings = GetDefaultConfig();
			SaveConfig();
			Puts("New configuration file created...");
		}

		protected override void SaveConfig() => Config.WriteObject(Settings);


		#endregion

		#region -Oxide hooks-


		private void Init()
		{
			Singleton = this;
			Barricades = new HashSet<BarricadeDecay>();
		}

		private void OnServerInitialized() => ServerMgr.Instance.StartCoroutine(FindAllBarricades());

		private IEnumerator FindAllBarricades()
		{
			IEnumerator<BaseNetworkable> enumerator = BaseNetworkable.serverEntities.GetEnumerator();

			while(enumerator.MoveNext())
			{
				if(enumerator.Current == null || enumerator.Current.IsDestroyed) continue;

				if(NeedToDecay(enumerator.Current.gameObject)) enumerator.Current.gameObject.AddComponent<BarricadeDecay>();

				yield return new WaitForFixedUpdate();
			}
		}

		private bool NeedToDecay(GameObject obj) => obj != null && obj.GetComponent<DecayEntity>() != null && Settings.EnabledEntities.ContainsKey(obj.GetComponent<DecayEntity>().PrefabName) && Settings.EnabledEntities[obj.GetComponent<DecayEntity>().PrefabName] && obj.GetComponent<BarricadeDecay>() == null;

		private void OnEntityBuilt(Planner planner, GameObject gameObject)
		{
			if(NeedToDecay(gameObject)) gameObject.AddComponent<BarricadeDecay>();
		}

		private void Unload()
		{
			foreach(BarricadeDecay barricadeDecay in Barricades) barricadeDecay.RemoveComponent();
		}


		#endregion

	}

}