using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("Newbie", "2CHEVSKII", "1.0.0")]
	[Description("Say 'Hello' to new players!")]
	internal class Newbie : RustPlugin
	{

		#region -Fields-


		private List<ulong> allplayers;
		private List<BasePlayer> unawake;
		private bool announce = true;
		private bool welcome = true;

		#region [Message keys]


		private const string mannounce = "Announce to all";
		private const string mwelcome = "Welcome to newplayer";
		private const string mprefix = "Prefix";


		#endregion

		#endregion

		#region -Configuration-


		protected override void LoadDefaultConfig() { }

		private void LoadConfiguration()
		{
			CheckConfig("Announce to all players", ref announce);
			CheckConfig("Display a welcome msg for newbie", ref welcome);
			SaveConfig();
		}

		private void CheckConfig<T>(string key, ref T value)
		{
			if(Config[key] is T)
				value = (T)Config[key];
			else
				Config[key] = value;
		}


		#endregion

		#region -Localization-


		protected override void LoadDefaultMessages() => lang.RegisterMessages(messages, this);

		private Dictionary<string, string> messages = new Dictionary<string, string>
		{
			{ mannounce, "The player {0} is new to the server! Try 2 b friendly with him!"},
			{ mwelcome, "Welcome to the server, {0}! Have a good time!" },
			{ mprefix, "[Welcome announcer]" }
		};

		private void Announcer(string message, params string[] args) => Server.Broadcast($"{lang.GetMessage(mprefix, this)} {string.Format(lang.GetMessage(message, this), args)}");

		private void Replier(BasePlayer player, string message, bool prefix = true, params string[] args) => player.ChatMessage($"{(prefix ? (lang.GetMessage(mprefix, this) + " ") : string.Empty)}{string.Format(lang.GetMessage(message, this), args)}");


		#endregion

		#region -Hooks-


		private void Init() => LoadConfiguration();

		private void Loaded()
		{
			try
			{
				allplayers = Interface.Oxide.DataFileSystem.GetFile("Newbie").ReadObject<List<ulong>>();
			}
			catch
			{
				allplayers = new List<ulong>();
			}
			foreach(BasePlayer player in BasePlayer.activePlayerList)
			{
				if(!allplayers.Contains(player.userID))
					allplayers.Add(player.userID);
			}
			unawake = new List<BasePlayer>();
		}

		private void OnPlayerInit(BasePlayer player)
		{
			if(!allplayers.Contains(player.userID))
			{
				allplayers.Add(player.userID);
				if(announce)
					Announcer(mannounce, player.displayName);
				if(welcome)
					unawake.Add(player);
			}
		}

		private void OnPlayerSleepEnded(BasePlayer player)
		{
			if(unawake.Contains(player))
			{
				Replier(player, mwelcome, true, player.displayName);
				unawake.Remove(player);
			}
		}

		private void OnPlayerDisconnected(BasePlayer player, string reason) { if(unawake.Contains(player)) unawake.Remove(player); }

		private void Unload() => Interface.Oxide.DataFileSystem.GetFile("Newbie").WriteObject(allplayers);


		#endregion

	}
}
