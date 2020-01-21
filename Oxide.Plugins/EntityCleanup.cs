//#define DEBUG //This line enables debug output
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Arg = ConsoleSystem.Arg;

namespace Oxide.Plugins
{
	[Info("Entity Cleanup", "2CHEVSKII", "3.0.3")]
	[Description("Easy way to cleanup your server from unnecessary entities")]
	public class EntityCleanup : RustPlugin
	{
		#region Fields


		private PluginSettings Settings { get; set; }
		private const string PERMISSION = "entitycleanup.use";

		private HashSet<string> Deployables { get; } = new HashSet<string>();
		private Timer ScheduleTimer { get; set; }


		#endregion

		#region Config


		private class PluginSettings
		{
			[JsonProperty(PropertyName = "Scheduled cleanup seconds (x <= 0 to disable)")]
			internal int ScheduledCleanup { get; set; }
			[JsonProperty(PropertyName = "Scheduled cleanup building blocks")]
			internal bool ScheduledCleanupBuildings { get; set; }
			[JsonProperty(PropertyName = "Scheduled cleanup deployables")]
			internal bool ScheduledCleanupDeployables { get; set; }
			[JsonProperty(PropertyName = "Scheduled cleanup outside cupboard range")]
			internal bool ScheduledCleanupOutsideCupRange { get; set; }
			[JsonProperty(PropertyName = "Scheduled cleanup entities with hp less than specified (x = [0.0-1.0])")]
			internal float ScheduledCleanupDamaged { get; set; }
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				Settings = Config.ReadObject<PluginSettings>();
				if(Settings == null)
					throw new JsonException("Can't read config...");
				else
					Puts("Configuration loaded...");
			}
			catch
			{
				LoadDefaultConfig();
			}
		}

		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			Settings = GetDefaultSettings();
			SaveConfig();
			PrintWarning("Default configuration created...");
		}

		protected override void SaveConfig() => Config.WriteObject(Settings, true);

		private PluginSettings GetDefaultSettings() => new PluginSettings {
			ScheduledCleanup = 3600,
			ScheduledCleanupBuildings = true,
			ScheduledCleanupDeployables = true,
			ScheduledCleanupOutsideCupRange = true,
			ScheduledCleanupDamaged = 0f
		};


		#endregion

		#region LangAPI


		private const string mhelp = "Help message";
		private const string mnoperm = "No permissions";
		private const string mannounce = "Announcement";

		private Dictionary<string, string> DefaultMessages_EN { get; } = new Dictionary<string, string>
		{
			{ mhelp, "Usage: cleanup (<all/buildings/deployables/deployable partial name>) (all)" },
			{ mnoperm, "You have no access to that command" },
			{ mannounce, "Server is cleaning up <color=#00FF5D>{0}</color> entities..." }
		};

		protected override void LoadDefaultMessages() => lang.RegisterMessages(DefaultMessages_EN, this, "en");

		private string GetReply(BasePlayer player, string message, params object[] args) => string.Format(lang.GetMessage(message, this, player?.UserIDString), args);


		#endregion

		#region Hooks


		private void Init()
		{
			permission.RegisterPermission(PERMISSION, this);
			permission.GrantGroupPermission("admin", PERMISSION, this);
			cmd.AddConsoleCommand("cleanup", this, delegate (Arg arg)
			{
				return CommandHandler(arg);
			});
			InitDeployables();
		}

		private void OnServerInitialized()
		{
			if(Settings.ScheduledCleanup > 0)
				StartScheduleTimer();
		}


		#endregion

		#region Commands


		private bool CommandHandler(Arg arg)
		{
			BasePlayer player = arg.Player();

			if(player && !permission.UserHasPermission(player.UserIDString, PERMISSION))
				arg.ReplyWith(GetReply(player, mnoperm));
			else if(!arg.HasArgs())
				ScheduledCleanup();
			else
			{
				switch(arg.Args.Length)
				{
					case 1:
						switch(arg.Args[0].ToLower())
						{
							case "all":
								ServerMgr.Instance.StartCoroutine(CollectData(new List<BaseEntity>()));
								break;
							default:
								ServerMgr.Instance.StartCoroutine(CollectData(new List<BaseEntity>(), false, arg.Args[0]));
								break;
						}
						break;
					case 2:
						switch(arg.Args[0].ToLower())
						{
							case "all":
								if(arg.Args[1].ToLower() == "all")
									ServerMgr.Instance.StartCoroutine(CollectData(new List<BaseEntity>(), true));
								else
									arg.ReplyWith(GetReply(player, mhelp));
								break;
							default:
								if(arg.Args[1].ToLower() == "all")
									ServerMgr.Instance.StartCoroutine(CollectData(new List<BaseEntity>(), true, arg.Args[1]));
								else
									arg.ReplyWith(GetReply(player, mhelp));
								break;
						}
						break;
					default:
						arg.ReplyWith(GetReply(player, mhelp));
						break;
				}
			}
			return true;
		}


		#endregion

		#region Core


		private IEnumerator CollectData(List<BaseEntity> entities, bool all = false, string name = null)
		{
			IEnumerator<BaseNetworkable> enumerator = BaseNetworkable.serverEntities.GetEnumerator();
#if DEBUG
			Puts("Started data collect");
#endif
			while(enumerator.MoveNext())
			{
				BaseEntity baseEntity = enumerator.Current as BaseEntity;

				var parentEntity = baseEntity?.GetParentEntity();

				while(parentEntity != null && !parentEntity.IsDestroyed)
				{
					baseEntity = parentEntity;
					parentEntity = baseEntity?.GetParentEntity();
				}

				if(baseEntity == null)
				{
#if DEBUG
					Puts("Skipped not a baseEntity");
#endif
					yield return new WaitForEndOfFrame();
					continue;
				}

				if(baseEntity.OwnerID == 0)
				{
#if DEBUG
					Puts("Skipped baseEntity without ownerid");
					#endif
					yield return new WaitForEndOfFrame();

					continue;
				}

				if(baseEntity.GetBuildingPrivilege() != null && !all && (baseEntity.Health() / baseEntity.MaxHealth()) > Settings.ScheduledCleanupDamaged)
				{
#if DEBUG
					Puts("Skipped BE with BP or HP");
#endif
					yield return new WaitForEndOfFrame();
					continue;
				}

				if((name == null || name.ToLower() == "buildings") && baseEntity is StabilityEntity)
				{
#if DEBUG
					Puts("Added building block");
#endif
					entities.Add(baseEntity);
					yield return new WaitForEndOfFrame();
					continue;
				}

				if(((name == null || name.ToLower() == "deployables") && Deployables.Contains(baseEntity.gameObject.name))
					|| (name != null && baseEntity.gameObject.name.Contains(name, CompareOptions.IgnoreCase)))
				{
#if DEBUG
					Puts("Added deployable");
#endif
					entities.Add(baseEntity);
					yield return new WaitForEndOfFrame();
					continue;
				}
			}

			if(entities.Count < 1)
			{
#if DEBUG
				Puts("Attempting to clean, but nothing to be cleaned");
#endif
				yield break;
			}

			ServerMgr.Instance.StartCoroutine(Cleanup(entities));
		}

		private IEnumerator Cleanup(List<BaseEntity> entities)
		{
			Server.Broadcast(GetReply(null, mannounce, entities.Count));

			for(int i = 0; i < entities.Count; i++)
			{
				if(!entities[i].IsDestroyed)
				{
					entities[i].Kill(BaseNetworkable.DestroyMode.None);
					yield return new WaitForSeconds(0.05f);
				}
			}
#if DEBUG
			Puts($"Cleanup finished, {entities.Count} entities cleaned.");
#endif
		}


		#endregion

		#region Utility


		private void InitDeployables()
		{
			IEnumerable<ItemModDeployable> deps = from def in ItemManager.GetItemDefinitions()
												  where def.GetComponent<ItemModDeployable>() != null
												  select def.GetComponent<ItemModDeployable>();

			Puts($"Found {deps.Count()} deployables definitions");

			foreach(ItemModDeployable dep in deps)
				if(!Deployables.Contains(dep.entityPrefab.resourcePath))
					Deployables.Add(dep.entityPrefab.resourcePath);
		}

		private void StartScheduleTimer()
		{
			if(ScheduleTimer != null && !ScheduleTimer.Destroyed)
				ScheduleTimer.Destroy();
			ScheduleTimer = timer.Once(Settings.ScheduledCleanup, () => ScheduledCleanup());
		}

		private void ScheduledCleanup()
		{
#if DEBUG
			Puts("Scheduled CU triggered");
#endif
			if(Settings.ScheduledCleanupBuildings && Settings.ScheduledCleanupDeployables)
			{
#if DEBUG
				Puts("Scheduled CU all");
#endif
				ServerMgr.Instance.StartCoroutine(CollectData(new List<BaseEntity>()));
			}
			else if(Settings.ScheduledCleanupBuildings)
			{
#if DEBUG
				Puts("Scheduled CU building");
#endif
				ServerMgr.Instance.StartCoroutine(CollectData(new List<BaseEntity>(), false, "buildings"));
			}
			else if(Settings.ScheduledCleanupDeployables)
			{
#if DEBUG
				Puts("Scheduled CU deployable");
#endif
				ServerMgr.Instance.StartCoroutine(CollectData(new List<BaseEntity>(), false, "deployables"));
				if(Settings.ScheduledCleanup > 0)
					StartScheduleTimer();
			}
		}


		#endregion
	}
}