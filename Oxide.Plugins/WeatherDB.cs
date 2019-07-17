﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;

//07.03.2019
//Plugin is unfinished, need to learn SQL basics first

namespace Oxide.Plugins
{
	[Info("Weather to Database", "2CHEVSKII", 0.1)]
	internal class WeatherDB : RustPlugin
	{

		#region [Config and fields]


		//Storage
		private DynamicConfigFile fileManager;
		private List<Report> reportCollection;

		//Config vars
		private int saveInterval = 300;
		private int port = 3306;
		private bool timestamps = true;
		private bool unixTime = false;
		private bool history = true;
		private bool saveLocal = true;
		private bool saveMySQL = false;
		private bool report_clouds = true;
		private bool report_fog = true;
		private bool report_rain = true;
		private bool report_wind = true;
		private bool report_gametime = true;
		private string db_filename = "weatherTDB.db";
		private string db_hostname = "localhost";
		private string db_name = "rust";
		private string db_username = "root";
		private string db_password = "toor";

		private void Init() => LoadCfgVars();

		protected override void LoadDefaultConfig() { }

		private void LoadCfgVars()
		{
			CheckConfig("Save interval:", ref saveInterval);
			CheckConfig("Save history:", ref history);
			CheckConfig("Save locally:", ref saveLocal);
			CheckConfig("Save to remote MySQL:", ref saveMySQL);
			CheckConfig("Make timestamps:", ref timestamps);
			CheckConfig("Timestamps in unix format:", ref unixTime);
			CheckConfig("Report in-game time:", ref report_gametime);
			CheckConfig("Report clouds:", ref report_clouds);
			CheckConfig("Report fog:", ref report_fog);
			CheckConfig("Report rain:", ref report_rain);
			CheckConfig("Report wind:", ref report_wind);
			CheckConfig("MySQL hostname:", ref db_hostname);
			CheckConfig("MySQL port:", ref port);
			CheckConfig("MySQL DB name:", ref db_name);
			CheckConfig("MySQL Username:", ref db_username);
			CheckConfig("MySQL Password:", ref db_password);
			if(saveInterval > 0)
				timer.Every((float)saveInterval, () => SaveDB(null));
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

		#region [Core]


		private void SaveDB(ConsoleSystem.Arg arg = null)
		{
			if(!saveLocal && !saveMySQL)
			{
				arg?.ReplyWith("Saving is disabled in config - enable it and reload the plugin");
				return;
			}
			if(!history)
				ClearDB(null);
			fileManager = Interface.Oxide.DataFileSystem.GetFile("weatherDB");
			reportCollection = fileManager.ReadObject<List<Report>>();
			string clouds_state = string.Format("{0}%", Mathf.Round(Climate.GetClouds(TerrainMeta.Center) * 100));
			string rain_state = string.Format("{0}%", Mathf.Round(Climate.GetRain(TerrainMeta.Center) * 100));
			string fog_state = string.Format("{0}%", Mathf.Round(Climate.GetFog(TerrainMeta.Center) * 100));
			string wind_state = string.Format("{0}%", Mathf.Round(Climate.GetWind(TerrainMeta.Center) * 100));
			Report currentreport = new Report {
				timestamp = timestamps ? unixTime ? $"{UnixTime(DateTime.Now)}" : $"{DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()}" : "Disabled",
				clouds = report_clouds ? clouds_state : "Disabled",
				rain = report_rain ? rain_state : "Disabled",
				fog = report_fog ? fog_state : "Disabled",
				wind = report_wind ? wind_state : "Disabled",
				gametime = report_gametime ? TOD_Sky.Instance != null ? TOD_Sky.Instance.Cycle.Hour.ToString() : "Undefined" : "Disabled"
			};
			reportCollection.Add(currentreport);
			fileManager.WriteObject(reportCollection);
			arg?.ReplyWith($"Weather report saved \nTime: {currentreport.timestamp}, \nClouds: {currentreport.clouds}, \nRain: {currentreport.rain}, \nFog: {currentreport.fog}, \nWind: {currentreport.wind}");
		}

		private void ClearDB(ConsoleSystem.Arg arg = null)
		{
			fileManager = Interface.Oxide.DataFileSystem.GetFile("weatherDB");
			reportCollection = fileManager.ReadObject<List<Report>>();
			reportCollection?.Clear();
			fileManager.WriteObject(reportCollection);
			arg?.ReplyWith("Weather database cleared successfully!");
		}

		private void WriteToRemoteDB(Report report)
		{

		}

		private long UnixTime(DateTime current) => Convert.ToInt64((current - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);


		#endregion

		#region [Data]


		private class Report
		{
			[JsonProperty(PropertyName = "Time")]
			public string timestamp;
			[JsonProperty(PropertyName = "In-game time")]
			public string gametime;
			[JsonProperty(PropertyName = "Clouds")]
			public string clouds;
			[JsonProperty(PropertyName = "Rain")]
			public string rain;
			[JsonProperty(PropertyName = "Fog")]
			public string fog;
			[JsonProperty(PropertyName = "Wind")]
			public string wind;
		}


		#endregion

		#region [Commands]


		[ConsoleCommand("weatherdb.save")]
		private void CmdSaveDB(ConsoleSystem.Arg arg) => SaveDB(arg ?? null);
		[ConsoleCommand("weatherdb.clear")]
		private void CmdClearDB(ConsoleSystem.Arg arg) => ClearDB(arg ?? null);


		#endregion

	}
}
