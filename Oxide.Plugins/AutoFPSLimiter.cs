using System.Linq;
using static UnityEngine.Application;

namespace Oxide.Plugins
{
	[Info("Auto FPS Limiter", "2CHEVSKII", "1.0.0")]
	[Description("Automatically changes FPS limit of the server based on it being empty")]
	internal class AutoFPSLimiter : CovalencePlugin
	{

		#region -Configuration and fields-


		private int EmptyFPS = 30;
		private int NonEmptyFPS = 120;
		private int DefaultFPS { get; set; }
		private bool LogChanges = true;

		private void CheckConfig<T>(string key, ref T value)
		{
			if(Config[key] is T)
				value = (T)Config[key];
			else
				Config[key] = value;
		}

		private void LoadConfiguration()
		{
			CheckConfig<int>("Empty FPS limit", ref EmptyFPS);
			CheckConfig<int>("Non-empty FPS limit", ref NonEmptyFPS);
			CheckConfig<bool>("Log FPS limit changes", ref LogChanges);
			SaveConfig();
		}

		protected override void LoadDefaultConfig() { }


		#endregion

		#region -Hooks-


		private void Init() => LoadConfiguration();

		private void OnServerInitialized()
		{
			DefaultFPS = targetFrameRate;
			if(players.Connected.Any())
			{
				NotEmptyFpsLimit(false);
				Puts("Server is not empty, FPS limited to " + NonEmptyFPS);
			}
			else
			{
				EmptyFpsLimit(false);
				Puts("Server is empty, FPS limited to " + EmptyFPS);
			}
		}

		private void OnUserConnected()
		{
			if(players.Connected.Count() <= 1)
				NotEmptyFpsLimit(LogChanges);
		}

		private void OnUserDisconnected()
		{
			if(players.Connected.Count() <= 1)
				EmptyFpsLimit(LogChanges);
		}

		private void Unload()
		{
			targetFrameRate = DefaultFPS;
			Puts("Plugin is being unloaded, setting default FPS limit");
		}


		#endregion

		#region -Functions-


		private void EmptyFpsLimit(bool log)
		{
			if(log)
				Puts("Server is empty, FPS limit set to " + EmptyFPS);
			targetFrameRate = EmptyFPS;
		}

		private void NotEmptyFpsLimit(bool log)
		{
			if(log)
				Puts("Server is no longer empty, FPS limit set to " + NonEmptyFPS);
			targetFrameRate = NonEmptyFPS;
		}


		#endregion

	}
}
