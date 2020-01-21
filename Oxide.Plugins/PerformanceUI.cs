// Requires: ImageLibrary

using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
	/* TODO:
     * Remaster
     */

	[Info("Performance UI", "2CHEVSKII", "1.0.7")]
	[Description("Shows information about server performance in a user-friendly way")]
	internal class PerformanceUI : RustPlugin
	{
		private void TestUI()
		{
			BasePlayer player = BasePlayer.Find("2CHEVSKII");

			CuiElementContainer container = new CuiElementContainer();        //fadeout and fadein work now

			string mainpanel = "pui.testui";

			container.Add(new CuiPanel {
				CursorEnabled = false,
				Image = { Color = "0.6 0.8 0.2 1", Material = "assets/icons/iconmaterial.mat" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -10", OffsetMax = "25 10" }
			}, "Hud", mainpanel);

			CuiHelper.AddUi(player, container);

		}

		private void RemoveTestUI()
		{
			foreach(BasePlayer player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, "pui.testui");
			}
		}


		#region -Configuration-


		protected override void LoadDefaultConfig() { }

		private void LoadCFG()
		{
			CheckConfig("Use permissions", ref usePermissions);
			CheckConfigFloat("UI X position", ref UI_X);
			CheckConfigFloat("UI Y position", ref UI_Y);
			SaveConfig();
		}

		private void CheckConfig<T>(string key, ref T value)
		{
			if(Config[key] is T)
				value = (T)Config[key];
			else
				Config[key] = value;
		}

		private void CheckConfigFloat(string key, ref float value)
		{
			if(Config[key] is double || Config[key] is int)
				value = Convert.ToSingle(Config[key]);
			else
				Config[key] = value;
		}


		#endregion

		#region -Language-


		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string> {
				["Wrong command usage"] = "<color=yellow>Wrong command usage! Try \"/performance help\"!</color>",
				["User has no permission"] = "<color=red>You are not allowed to use this command!</color>",
				["Help command response"] = "Usage:\n/performance - Get a single text message with current server performance and Your ping\n/performance gui - Display a UI with live updated performance information",
				["Performance report"] = "Current server performance:\nTickrate: {0}\nFramerate: {1}\nFrametime: {2}\nYour ping: {3}"
			}, this, "en");
			lang.RegisterMessages(new Dictionary<string, string> {
				["Wrong command usage"] = "<color=yellow>Неверная команда! Попробуйте \"/performance help\"!</color>",
				["User has no permission"] = "<color=red>У Вас недостаточно прав для использования данной команды!</color>",
				["Help command response"] = "Использование:\n/performance - Получить сообщение о текущей производительнсти сервера и Вашем пинге\n/performance gui - Отобразить UI с информацией в реальном времени",
				["Performance report"] = "Текущая производительность:\nТикрейт: {0}\nКадров в секунду: {1}\nВремя кадра: {2}\nВаш пинг: {3}"
			}, this, "ru");
		}


		#endregion

		#region -Variables-


		[PluginReference] private Plugin ImageLibrary; //needed for proper image display

		private Dictionary<BasePlayer, bool> guiUsersOpened, guiUsersCollapsed;

		private int nominalTickrate = ConVar.Server.tickrate; //to determine how much current tickrate differs from original set
		private int nominalFPS = ConVar.FPS.limit;//same but for fps
		private int tickrate, ticks = 0; //actual tickrate

		private bool isTimerRunning;
		private bool usePermissions = true;

		private const string permUse = "performanceui.use";
		private const string permUseGUI = "performanceui.usegui";

		private float UI_X = 0f;
		private float UI_Y = 0.83f;


		#endregion

		#region -Oxide hooks-


		private void OnTick() //counts actual tickrate
		{
			if(!isTimerRunning) //check if timer needs to be restarted and calculates tickrate
			{
				int starttick = ticks = 0;
				timer.Once(1f, () => { tickrate = ticks - starttick; isTimerRunning = false; });
				isTimerRunning = true;
			}
			if(ticks >= 101) //100 is the maximum available tickrate for rust servers (where are my 144Hz?)
				ticks = 0;
			ticks++;
		}

		private void Init()
		{
			LoadCFG();
			permission.RegisterPermission(permUse, this); //registers a permission for commands usage
			permission.RegisterPermission(permUseGUI, this); //permission for gui usage (since it's much more heavy than the chat command)
															 //permission.GrantGroupPermission("default", permUse, this); //grants chat command to all
			IconSize();
			//TestUI();
		}

		private void OnServerInitialized()
		{
			guiUsersOpened = new Dictionary<BasePlayer, bool>();
			guiUsersCollapsed = new Dictionary<BasePlayer, bool>();
			guiUsersOpened.Clear();
			guiUsersCollapsed.Clear();
			foreach(BasePlayer player in BasePlayer.activePlayerList) //add all existing players to dictionaries
			{
				guiUsersOpened.Add(player, false);
				guiUsersCollapsed.Add(player, false);
			}
			DownloadIcons();
			timer.Every(1f, () => RefreshUI()); //value determines frequency of data refresh
		}

		private void Unload() //destroy ui for all the players to prevent duplication and errors
		{
			guiUsersOpened.Clear();
			guiUsersCollapsed.Clear();
			RefreshUI();
			//RemoveTestUI();
		}

		private void OnPlayerInit(BasePlayer player) //add new player to the ui userlist
		{
			if(player != null)
			{
				guiUsersOpened.Add(player, false);
				guiUsersCollapsed.Add(player, false);
			}
		}

		private void OnPlayerDisconnected(BasePlayer player, string reason) //remove player from userlist
		{
			if(player != null)
			{
				guiUsersOpened.Remove(player);
				guiUsersCollapsed.Remove(player);
			}
		}


		#endregion

		#region -Commands-


		[ChatCommand("performance")]
		private void CmdChatPerformance(BasePlayer player, string command, string[] args) //all the chat functions are here
		{
			switch(args.Length)
			{
				case 0:
					if(permission.UserHasPermission(player.UserIDString, permUse) || !usePermissions)
					{
						string subMsg = lang.GetMessage("Performance report", this, player.UserIDString);
						string message = string.Format(subMsg, GetTickrate(), GetFPS(), GetFrametime(), GetUserPing(player));
						SendReply(player, message);
					}
					else
					{
						SendReply(player, lang.GetMessage("User has no permission", this, player.UserIDString));
					}
					//WRONGCOMMANDUSAGE
					break;
				case 1:
					switch(args[0].ToLower())
					{
						case "gui": //OPEN OR CLOSE UI
							if(permission.UserHasPermission(player.UserIDString, permUseGUI) || !usePermissions)
								OpenCloseUI(player);
							else
								SendReply(player, lang.GetMessage("User has no permission", this, player.UserIDString));
							break;
						case "help": //SENDS HELP MESSAGE
							SendReply(player, lang.GetMessage("Help command response", this, player.UserIDString));
							break;
						default:
							SendReply(player, lang.GetMessage("Wrong command usage", this, player.UserIDString));
							break;
					}
					break;
				default:
					SendReply(player, lang.GetMessage("Wrong command usage", this, player.UserIDString));
					break;
			}
		}

		[ConsoleCommand("performance.size")] //changes size of the UI
		private void CmdConsoleSize(ConsoleSystem.Arg argument)
		{
			BasePlayer player = argument?.Player();
			if(player != null)
				ExpandCollapseUI(player);
		}


		#endregion

		#region -Helper methods-


		private void OpenCloseUI(BasePlayer player) //does what it says
		{
			guiUsersOpened.TryGetValue(player, out bool isOpened);
			if(!isOpened)
			{
				guiUsersOpened[player] = true;
				RefreshUI();
			}
			else
			{
				guiUsersOpened[player] = false;
				RefreshUI();
			}
		}

		private void ExpandCollapseUI(BasePlayer player)//invoked when player press a button
		{
			guiUsersCollapsed.TryGetValue(player, out bool isCollapsed);
			if(isCollapsed)
			{
				guiUsersCollapsed[player] = false;
				RefreshUI();
			}
			else
			{
				guiUsersCollapsed[player] = true;
				RefreshUI();
			}
		}

		private void RefreshUI() //re-builds ui with current values if atleast oneplayer has it open
		{
			if(guiUsersOpened.ContainsValue(true))
			{
				BuildUI();
				BuildSmallUI();
				foreach(BasePlayer player in BasePlayer.activePlayerList)
				{
					CuiHelper.DestroyUi(player, mainCanvas);
					guiUsersOpened.TryGetValue(player, out bool isActiveUI);
					if(isActiveUI)
					{
						guiUsersCollapsed.TryGetValue(player, out bool isCollapsedUI);
						if(isCollapsedUI)
							CuiHelper.AddUi(player, collapsedContainer);
						else
							CuiHelper.AddUi(player, expandedContainer);
					}
				}
			}
			else
			{
				foreach(BasePlayer player in BasePlayer.activePlayerList)
				{
					CuiHelper.DestroyUi(player, mainCanvas);
				}
			}
		}

		private int GetFPS() => Performance.report.frameRate;

		private float GetFrametime() => Performance.report.frameTime;

		private int GetTickrate() => tickrate;

		private int GetUserPing(BasePlayer player) => Player.Ping(player.Connection);


		#endregion

		#region -UI-


		private CuiElementContainer expandedContainer = new CuiElementContainer(); // ui containers
		private CuiElementContainer collapsedContainer = new CuiElementContainer();

		#region [UI elements]


		private const string mainCanvas = "mainCanvas";
		private const string ecButton = "ecButton";
		private const string tickrateBG = "tickrateBG";
		private const string tickrateValue = "tickrateValue";
		private const string tickrateIcon = "tickrateIcon";
		private const string tickrateURL = "https://i.imgur.com/mnnficY.png";
		private string tickColor, frameColor;
		private string bigUIAMin;
		private string bigUIAMax;
		private string smallUIAMin;
		private string smallUIAMax;
		private const string fpsBG = "fpsFTBG";
		private const string fpsFTValue = "fpsFTValue";
		private const string fpsFTIcon = "fpsIcon";
		private const string fpsURL = "https://i.imgur.com/KmdsVvW.png";


		#endregion

		#region [Helpers]


		private void IconSize()
		{
			bigUIAMin = $"{UI_X.ToString()} {UI_Y.ToString()}";
			bigUIAMax = $"{(UI_X + 0.1f).ToString()} {(UI_Y + 0.17f).ToString()}";
			smallUIAMin = $"{UI_X.ToString()} {UI_Y.ToString()}";
			smallUIAMax = $"{(UI_X + 0.11f).ToString()} {(UI_Y + 0.17f).ToString()}";
		}

		private void DownloadIcons() //store images used in ui
		{
			ImageLibrary.Call("AddImage", tickrateURL, tickrateIcon);
			ImageLibrary.Call("AddImage", fpsURL, fpsFTIcon);
		}

		private void ColorSwitcher(ref string tickColor, ref string frameColor) //switch color of the ui icons based on current performance
		{
			int localFPS = GetFPS(); //local value to request fps only one time at a cycle instead of 3 
			if(tickrate < nominalTickrate / 2)
			{
				tickColor = "0.99 0.31 0.02 1";//RED
			}
			else if(tickrate < nominalTickrate / 1.5 && tickrate > nominalTickrate / 2)
			{
				tickColor = "1 0.84 0 1";//YELLOW
			}
			else
			{
				tickColor = "1 1 1 1";//DEFAULT
			}
			if(localFPS < nominalFPS / 2)
			{
				frameColor = "0.99 0.31 0.02 1";//RED
			}
			else if(localFPS < nominalFPS / 1.5 && localFPS > nominalFPS / 2)
			{
				frameColor = "1 0.84 0 1";//YELLOW
			}
			else
			{
				frameColor = "1 1 1 1";//DEFAULT
			}
		}


		#endregion

		private void BuildUI() //Builds full-sized (non-collapsed) UI
		{
			ColorSwitcher(ref tickColor, ref frameColor);
			expandedContainer.Clear(); //Clears existing elements to prevent duplication
			expandedContainer.Add(new CuiPanel //Add main canvas to place all the elements
			{
				Image = { Color = "0.4 0.4 0.4 0" },
				RectTransform = { AnchorMin = bigUIAMin, AnchorMax = bigUIAMax },
				CursorEnabled = false,
			}, "Hud", mainCanvas);
			expandedContainer.Add(new CuiElement //Add backgound for tickrate value and icon
			{
				Parent = mainCanvas,
				Name = tickrateBG,
				Components =
				{
					new CuiRawImageComponent{ Color = "0 0 0 1", Material = "assets/content/ui/uibackgroundblur-notice.mat", Sprite = "assets/standard assets/effects/imageeffects/textures/noise.png"},
					new CuiRectTransformComponent{ AnchorMin = "0 0.26", AnchorMax = "1 0.48"}
				}
			});
			expandedContainer.Add(new CuiElement //add tickrate icon with color applied
			{
				Parent = tickrateBG,
				Name = tickrateIcon,
				Components =
				{
					new CuiRawImageComponent{ Color = tickColor, Sprite = "assets/content/textures/generic/fulltransparent.tga", Png = (string)ImageLibrary.Call("GetImage", tickrateIcon) ?? ""},
					new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "0.215 1"}
				}
			});
			expandedContainer.Add(new CuiLabel //Add tickrate value
			{
				Text = { Text = string.Format("Tickrate: {0}", GetTickrate().ToString()), Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
				RectTransform = { AnchorMin = "0.25 0.1", AnchorMax = "1 0.8" }
			}, tickrateBG, tickrateValue);
			expandedContainer.Add(new CuiElement //Add backgound for server fps and frametime
			{
				Parent = mainCanvas,
				Name = fpsBG,
				Components =
				{
					new CuiRawImageComponent{ Color = "0 0 0 1", Material = "assets/content/ui/uibackgroundblur-notice.mat", Sprite = "assets/standard assets/effects/imageeffects/textures/noise.png"},
					new CuiRectTransformComponent{ AnchorMin = "0 0.52", AnchorMax = "1 0.74"}
				}
			});
			expandedContainer.Add(new CuiElement //add fps icon with color applied
			{
				Parent = fpsBG,
				Name = fpsFTIcon,
				Components =
				{
					new CuiRawImageComponent{ Color = tickColor, Sprite = "assets/content/textures/generic/fulltransparent.tga", Png = (string)ImageLibrary.Call("GetImage", fpsFTIcon) ?? ""},
					new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "0.215 1"}
				}
			});
			expandedContainer.Add(new CuiLabel //Add server fps and frametime values
			{
				Text = { Text = string.Format("FPS: {0}", GetFPS().ToString()), Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
				RectTransform = { AnchorMin = "0.25 0.1", AnchorMax = "1 0.8" }
			}, fpsBG, fpsFTValue);
			expandedContainer.Add(new CuiButton //Add button to change size of the UI
			{
				RectTransform = { AnchorMin = "0 0.78", AnchorMax = "1 1" },
				Button = { Command = "performance.size", Color = "0.8 0.2 0.2 1" },
				Text = { Text = "MINIMIZE", Align = TextAnchor.MiddleCenter, FontSize = 20 },
			}, mainCanvas, ecButton);
		}

		private void BuildSmallUI() //Builds minimized UI
		{
			ColorSwitcher(ref tickColor, ref frameColor);
			collapsedContainer.Clear(); //Clears 
			collapsedContainer.Add(new CuiPanel //Canvas
			{
				Image = { Color = "0.4 0.4 0.4 0" },
				RectTransform = { AnchorMin = smallUIAMin, AnchorMax = smallUIAMax },
				CursorEnabled = false,
			}, "Hud", mainCanvas);
			collapsedContainer.Add(new CuiElement //tickrate panel
			{
				Parent = mainCanvas,
				Name = tickrateBG,
				Components =
				{
					new CuiRawImageComponent{ Color = "0 0 0 1", Material = "assets/content/ui/uibackgroundblur-notice.mat", Sprite = "assets/standard assets/effects/imageeffects/textures/noise.png"},
					new CuiRectTransformComponent{ AnchorMin = "0 0.26", AnchorMax = "0.2 0.48"}
				}
			});
			collapsedContainer.Add(new CuiElement //add tickrate icon with color applied
			{
				Parent = tickrateBG,
				Name = tickrateIcon,
				Components =
				{
					new CuiRawImageComponent{ Color = tickColor, Sprite = "assets/content/textures/generic/fulltransparent.tga", Png = (string)ImageLibrary.Call("GetImage", tickrateIcon) ?? ""},
					new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "0.95 1"}
				}
			});
			collapsedContainer.Add(new CuiElement //framerate/frametime panel
			{
				Parent = mainCanvas,
				Name = fpsBG,
				Components =
				{
					new CuiRawImageComponent{ Color = "0 0 0 1", Material = "assets/content/ui/uibackgroundblur-notice.mat", Sprite = "assets/standard assets/effects/imageeffects/textures/noise.png"},
					new CuiRectTransformComponent{ AnchorMin = "0 0.52", AnchorMax = "0.2 0.74"}
				}
			});
			collapsedContainer.Add(new CuiElement //add fps icon with color applied
			{
				Parent = fpsBG,
				Name = fpsFTIcon,
				Components =
				{
					new CuiRawImageComponent{ Color = tickColor, Sprite = "assets/content/textures/generic/fulltransparent.tga", Png = (string)ImageLibrary.Call("GetImage", fpsFTIcon) ?? ""},
					new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "0.95 1"}
				}
			});
			collapsedContainer.Add(new CuiButton //button
			{
				RectTransform = { AnchorMin = "0 0.78", AnchorMax = "0.2 1" },
				Button = { Command = "performance.size", Color = "0.14 0.89 0.31 1" },
				Text = { Text = "+", Align = TextAnchor.MiddleCenter, FontSize = 20 },
			}, mainCanvas, ecButton);

		}


		#endregion

		//UI is WIP

	}
}