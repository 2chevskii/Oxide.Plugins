using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Rust;
using Newtonsoft.Json;
using System.Collections;
using System.Reflection;


/* TODO:
 * optimize the plugin using coroutines
 */ 

/* TODO:
 * [x] Make option for other barricades
 * [x] Make option to check for authorization of player who placed barricade
 * [-] Make option to upkeep the wall as other building blocks
 */

// Original idea and plugin version => Guilty Spark

namespace Oxide.Plugins
{
    [Info("HighWallBarricades", "2CHEVSKII", "2.2.3")]
    [Description("Makes High External Walls decay.")]
    internal class HighWallBarricades : RustPlugin
    {

        #region -Fields-


        //Config
        private PluginSettings Settings { get; set; }

        //Storages

        private static HighWallBarricades Singleton { get; set; }

        private HashSet<DecayBarricade> ActiveComponents { get; set; }


        #endregion

        #region -Configuration-


        private class PluginSettings
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

        private PluginSettings GetDefaultConfig()
        {
            return new PluginSettings
            {
                DecayTime = 600,
                DecayDamage = 0.2f,
                InitialHealth = 1.0f,
                DecayInCupRange = false,
                CheckBarricadeOwner = true,
                DisableStandartDecay = false,
                EnabledEntities = new Dictionary<string, bool>
                    {
                        { "assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab", true },
                        { "assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab", true },
                        { "assets/prefabs/deployable/barricades/barricade.concrete.prefab", true },
                        { "assets/prefabs/deployable/barricades/barricade.metal.prefab", true },
                        { "assets/prefabs/deployable/barricades/barricade.sandbags.prefab", true },
                        { "assets/prefabs/deployable/barricades/barricade.stone.prefab", true },
                        { "assets/prefabs/deployable/barricades/barricade.wood.prefab", true },
                        { "assets/prefabs/deployable/barricades/barricade.woodwire.prefab", true },
                        { "assets/prefabs/building/gates.external.high/gates.external.high.stone/gates.external.high.stone.prefab", true },
                        { "assets/prefabs/building/gates.external.high/gates.external.high.wood/gates.external.high.wood.prefab", true }
                    },
                ConfigVersion = Version
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<PluginSettings>();
                if(Settings == null)
                    throw new JsonException("Configuration failed to load, creating new one!");
            }
            catch
            {
                base.Config.Clear();
                Settings = GetDefaultConfig();
                SaveConfig();
            }
            if(Settings.ConfigVersion == null)
            {
                base.Config.Clear();
                Settings = GetDefaultConfig();
                SaveConfig();
                Puts("Configuration has been updated!");
            }
            if(Settings.ConfigVersion < Version)
            {
                if(Settings.ConfigVersion == new VersionNumber(2, 1, 0))
                {
                    var tempconfig = new PluginSettings
                    {
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
                Settings.EnabledEntities = new Dictionary<string, bool>
                {
                    { "assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab", true },
                    { "assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab", true },
                    { "assets/prefabs/deployable/barricades/barricade.concrete.prefab", true },
                    { "assets/prefabs/deployable/barricades/barricade.metal.prefab", true },
                    { "assets/prefabs/deployable/barricades/barricade.sandbags.prefab", true },
                    { "assets/prefabs/deployable/barricades/barricade.stone.prefab", true },
                    { "assets/prefabs/deployable/barricades/barricade.wood.prefab", true },
                    { "assets/prefabs/deployable/barricades/barricade.woodwire.prefab", true },
                    { "assets/prefabs/building/gates.external.high/gates.external.high.stone/gates.external.high.stone.prefab", true },
                    { "assets/prefabs/building/gates.external.high/gates.external.high.wood/gates.external.high.wood.prefab", true }
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
            ActiveComponents = new HashSet<DecayBarricade>();
        }

        private void OnServerInitialized()
        {
            foreach(var dEntity in UnityEngine.Object.FindObjectsOfType<DecayEntity>()) InitEntity(dEntity.gameObject);
        }
        
        private void OnEntityBuilt(Planner planner, GameObject gameObject) => InitEntity(gameObject);

        private void InitEntity(GameObject gameObject)
        {
            var dEntity = gameObject?.GetComponent<DecayEntity>();
            if(dEntity != null
                && Settings.EnabledEntities.Keys.Contains(dEntity.PrefabName)
                && Settings.EnabledEntities[dEntity.PrefabName])
            {
                if(Settings.InitialHealth <= 0f)
                    dEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                else
                {
                    dEntity.healthFraction = Settings.InitialHealth;
                    gameObject.AddComponent<DecayBarricade>();
                }
            }
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            var comp = entity?.GetComponent<DecayBarricade>();
            if(comp != null && ActiveComponents.Contains(comp))
            {
                comp.RemoveComponent();
                ActiveComponents.Remove(comp);
            }
        }

        private void Unload()
        {
            foreach(var comp in ActiveComponents)
            {
                comp.RemoveComponent();
            }
            //typeof(DecayBarricade).GetField("DecayPool", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);

            DecayBarricade.DecayPool = null;
            Singleton = null;
        }


        #endregion

        //NRE (update?)
        private class DecayBarricade : MonoBehaviour
        {
            private DecayEntity Entity { get; set; }
            private float LastDecayTick { get; set; }
            private float StandartDecayProtection { get; set; }
            public static List<DecayEntity> DecayPool { get; set; } = new List<DecayEntity>();

            private static bool DoChecks(DecayEntity entity)
            {
                if(entity.GetBuildingPrivilege() == null) return true;
                if(Singleton.Settings.DecayInCupRange)
                {
                    return !Singleton.Settings.CheckBarricadeOwner ? true : entity.GetBuildingPrivilege().authorizedPlayers.Any(player => player.userid == entity.OwnerID) ? false : true;
                }
                return false;
            }

            private void Awake()
            {
                Entity = GetComponent<DecayEntity>();
                LastDecayTick = Time.realtimeSinceStartup;
                StandartDecayProtection = Entity.baseProtection.amounts[(int)DamageType.Decay];
                if(Singleton.Settings.DisableStandartDecay) Entity.baseProtection.amounts[(int)DamageType.Decay] = 100f;
                Entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                Singleton.ActiveComponents.Add(this);
            }
            
            private void Update()
            {
                //if(Entity.healthFraction <= 0) Entity.Kill(BaseNetworkable.DestroyMode.Gib);
                if((Time.realtimeSinceStartup - LastDecayTick) > Singleton.Settings.DecayTime)
                {
                    if(DoChecks(Entity)) DecayPool.Add(Entity);
                    LastDecayTick = Time.realtimeSinceStartup;
                }
            }

            private void LateUpdate()
            {
                if(DecayPool.Count > 0) ServerMgr.Instance.StartCoroutine(DecayCycle());
            }

            private static IEnumerator DecayCycle()
            {
                for(int i = 0; i < DecayPool.Count; i++)
                {
                    yield return new WaitUntil(new System.Func<bool>(() =>
                    {
                        return DecayTick(DecayPool[i]);
                    }));
                }
                DecayPool.Clear();
            }

            private static bool DecayTick(DecayEntity entity)
            {
                entity.Hurt(Singleton.Settings.DecayDamage, DamageType.Decay, null, false);
                if(entity.healthFraction <= 0) entity.Kill(BaseNetworkable.DestroyMode.Gib);
                entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                return true;
            }

            private void OnDestroy()=> Entity.baseProtection.amounts[(int)DamageType.Decay] = StandartDecayProtection;

            public void RemoveComponent() => DestroyImmediate(this);
        }

    }
}