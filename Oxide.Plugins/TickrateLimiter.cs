//#define DEBUG //Uncomment this line to get debug output
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
	[Info("Tickrate Limiter", "2CHEVSKII", "0.1.1")]
	[Description("Limits current maximum tickrate based on player count to improve overall performance")]
	internal class TickrateLimiter : CovalencePlugin
	{

		#region -Fields-


		private int _defaultClValue;
		private int _defaultSvValue;
		private Dictionary<int, TickrateValues> TickrateCurve { get; set; }


		#endregion

		#region -Configuration-


		private Dictionary<int, TickrateValues> GetDefaultCurve => new Dictionary<int, TickrateValues>
		{
			{0, new TickrateValues(20, 30)},
			{50, new TickrateValues(16, 20)},
			{150, new TickrateValues(5, 10)}
		};

		protected override void LoadDefaultConfig()
		{
			TickrateCurve = GetDefaultCurve;

			PrintWarning("Creating default configuration file...");

			SaveConfig();
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				TickrateCurve = Config.ReadObject<Dictionary<int, TickrateValues>>();

				if (TickrateCurve == null)
					throw new JsonException("Could not load configuration...");

				Puts("Configuration loaded...");
			}
			catch
			{
				LoadDefaultConfig();
			}
		}

		protected override void SaveConfig()
		{
			Config.WriteObject(TickrateCurve, true);
		}


		#endregion

		#region -Hooks-


		private void OnServerInitialized()
		{
			_defaultClValue = Player.tickrate_cl;
			_defaultSvValue = Player.tickrate_sv;

			OnPlayerCountUpdate();
		}

		private void OnPlayerInit()
		{
			NextTick(() => OnPlayerCountUpdate());
		}

		private void OnPlayerDisconnected()
		{
			NextTick(() => OnPlayerCountUpdate());
		}

		private void Unload()
		{
			Player.tickrate_sv = _defaultSvValue;
			Player.tickrate_cl = _defaultClValue;
		}


		#endregion

		#region -Functions-


		private void OnPlayerCountUpdate()
		{
			var closest = TickrateCurve.OrderByDescending(x => x.Key)
				.First(z => z.Key <= BasePlayer.activePlayerList.Count).Value;

			Player.tickrate_sv = closest.Server;
			Player.tickrate_cl = closest.Client;

#if(DEBUG)

			PrintWarning(
				$"Tickrate values adjusted for the playercount {BasePlayer.activePlayerList.Count}:\n Client rate: {Player.tickrate_cl} Server rate: " +
				$"{Player.tickrate_sv}");
#endif
		}


		#endregion

		#region -Component-


		private class TickrateValues
		{
			public readonly int Client;
			public readonly int Server;

			public TickrateValues(int server, int client)
			{
				this.Server = server;
				this.Client = client;
			}
		}	


		#endregion

	}
}