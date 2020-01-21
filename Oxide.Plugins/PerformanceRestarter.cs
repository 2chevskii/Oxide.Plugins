using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Oxide.Core.Plugins;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;

namespace Oxide.Plugins
{
	[Info("Performance Restarter", "2CHEVSKII", "0.1.0")]
	[Description("Warns and optionally restarts server while it starts to loose FPS")]
	class PerformanceRestarter : CovalencePlugin
	{
		static PerformanceRestarter instance;

		const string PERMISSION = "performancerestarter.use";

		Settings settings;

		bool restarting;

		float lastCancelledTime = -10f;

		Timer resetTimer;

		[PluginReference] Plugin SmoothRestart;

		FieldInfo rcField;

		class Settings
		{
			public static Settings Default
			{
				get
				{
					return new Settings
					{
						WarnFPS = 25,
						WarnTime = 10,
						RestartFPS = 15,
						RestartTime = 30,
						RestartSeconds = 60
					};
				}
			}

			[JsonProperty("Warning: lowest FPS level")]
			public int WarnFPS;
			[JsonProperty("Warning: time for average")]
			public int WarnTime;
			[JsonProperty("Restart: lowest FPS level")]
			public int RestartFPS;
			[JsonProperty("Restart: time for average")]
			public int RestartTime;
			[JsonProperty("Restart: seconds before restart")]
			public int RestartSeconds;
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				settings = Config.ReadObject<Settings>();

				if(settings == null)
					throw new Exception();
			}
			catch
			{
				PrintWarning("Failed to load configuration, default will be loaded instead");
				LoadDefaultConfig();
			}
		}

		protected override void LoadDefaultConfig()
		{
			settings = Settings.Default;
			SaveConfig();
		}

		protected override void SaveConfig() => Config.WriteObject(settings);

		protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
		{
			["Warning"] = "FPS on the server is less than {0} for the last {1} seconds!",
			["Restart"] = "Due to FPS being lower than {0} for the last {1} seconds, server will be restarted!",
			["Help"] = "Use 'performancerestarter cancel' to cancel restart",
			["Restart cancelled"] = "Restart was cancelled",
			["No restart"] = "No restart is happening right now, nothing to cancel"
		}, this);

		void Init()
		{
			instance = this;
			permission.RegisterPermission(PERMISSION, this);
			rcField = typeof(ServerMgr).GetField("restartCoroutine", BindingFlags.Instance | BindingFlags.NonPublic);
		}

		void OnServerInitialized()
		{
			ServerMgr.Instance.gameObject.AddComponent<Restarter>();
		}

		void Unload()
		{
			UnityEngine.Object.Destroy(ServerMgr.Instance.GetComponent(typeof(Restarter)));
		}

		void TriggerWarning()
		{
			PrintWarning(lang.GetMessage("Warning", this), settings.WarnFPS, settings.WarnTime);
		}

		void TriggerRestart()
		{
			Puts("TriggerRestart");
			return;
			if(restarting)
				return;

			restarting = true;

			if(SmoothRestart)
			{
				SmoothRestart.CallHook("DoSmoothRestart", settings.RestartSeconds, false);
			}
			else
			{
				ServerMgr.RestartServer(string.Format(lang.GetMessage("Restart", this), settings.RestartFPS, settings.RestartTime), settings.RestartSeconds);
			}

			if(resetTimer != null)
			{
				resetTimer.Destroy();
			}

			resetTimer = timer.Once(settings.RestartSeconds, () => restarting = false); // to prevent 'hanging' if restart didn't happen
		}

		void CancelRestart()
		{
			restarting = false;

			if(SmoothRestart)
			{
				SmoothRestart.CallHook("StopRestart");
			}
			else
			{
				IEnumerator restartCoroutine = rcField.GetValue(ServerMgr.Instance) as IEnumerator;

				if(restartCoroutine != null)
				{
					ServerMgr.Instance.StopCoroutine(restartCoroutine);
					rcField.SetValue(ServerMgr.Instance, null);
				}

				server.Broadcast(lang.GetMessage("Restart cancelled", this));
				Puts(lang.GetMessage("Restart cancelled", this));
			}

			if(resetTimer != null)
			{
				resetTimer.Destroy();
			}

			lastCancelledTime = Time.realtimeSinceStartup;
		}

		[Command("performancerestarter"), Permission(PERMISSION)]
		bool CommandHandler(IPlayer player, string command, string[] args)
		{
			if(args.Length < 1 || args[0].ToLower() != "cancel")
			{
				player.Message(lang.GetMessage("Help", this, player.Id));
			}
			else if(!restarting)
			{
				player.Message(lang.GetMessage("No restart", this, player.Id));
			}
			else
			{
				CancelRestart();
			}
			return true;
		}

		class FixedQueue<T> : Queue<T>
		{
			public readonly int FixedSize;

			public FixedQueue(int fixedSize)
			{
				FixedSize = fixedSize;
			}

			new void Enqueue(T item)
			{
				base.Enqueue(item);
				if(Count > FixedSize)
					Dequeue();
			}
		}

		class Restarter : FacepunchBehaviour
		{
			float warnMeasure;
			float restartMeasure;

			FixedQueue<int> q;

			void Awake()
			{
				q = new FixedQueue<int>(25);
				
			}

			void Measure()
			{

			}

			void Update()
			{
				float deltaTime = Time.deltaTime;
				bool aboveWarn = IsAbove(deltaTime, instance.settings.WarnFPS);
				bool aboveRestart = IsAbove(deltaTime, instance.settings.RestartFPS);

				if(aboveWarn)
					warnMeasure = Time.realtimeSinceStartup;
				else if(NeedWarn)
				{
					instance.TriggerWarning();
					warnMeasure = Time.realtimeSinceStartup;
				}

				if(aboveRestart)
					restartMeasure = Time.realtimeSinceStartup;
				else if(NeedRestart)
				{
					instance.TriggerRestart();
					restartMeasure = Time.realtimeSinceStartup;
				}
			}

			bool IsAbove(float delta, int level)
			{
				float targetDelta = 1 / level * 1000;

				return delta < targetDelta;
			}

			bool NeedWarn => Time.realtimeSinceStartup - warnMeasure >= instance.settings.WarnTime;

			bool NeedRestart => Time.realtimeSinceStartup - restartMeasure >= instance.settings.RestartTime && Time.realtimeSinceStartup - 10 > instance.lastCancelledTime;
		}
	}
}
