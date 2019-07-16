// Requires: ImageLibrary

using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using static Oxide.Game.Rust.Cui.CuiHelper;

// Original idea and plugin version => Gimax

namespace Oxide.Plugins
{
    [Info("Godmode Indicator", "2CHEVSKII", "2.0.3")]
    [Description("Displays an indicator on screen if a player is in godmode")]
    class GodmodeIndicator : RustPlugin
    {

        #region -Configuration and fields-


        private static GodmodeIndicator Instance { get; set; }

        [PluginReference]
        private Plugin ImageLibrary;

        [PluginReference]
        private Plugin Godmode;

        private List<BasePlayer> ActiveIndicator { get; set; }

        private PluginSettings Settings { get; set; }

        private class PluginSettings
        {
            [JsonProperty(PropertyName = "UI X position")]
            internal float UI_X { get; set; }
            [JsonProperty(PropertyName = "UI Y position")]
            internal float UI_Y { get; set; }
            [JsonProperty(PropertyName = "UI URL")]
            internal string UI_URL { get; set; }
            [JsonProperty(PropertyName = "UI Color")]
            internal string UI_Color { get; set; }
        }

        private PluginSettings GetDefaultSettings() => new PluginSettings
        {
            UI_X = 0.01f,
            UI_Y = 0.86f,
            UI_Color = "1 1 1 1",
            UI_URL = "https://i.imgur.com/SF6lN2N.png"
        };

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Settings = GetDefaultSettings();
            Puts("Creating new configuration file...");
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<PluginSettings>();
                if(Settings == null)
                    throw new JsonException("Unable to load configuration file...");
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(Settings, true);


        #endregion

        #region -Hooks-


        private void Init()
        {
            ActiveIndicator = new List<BasePlayer>();
            Instance = this;
        }

        private void OnServerInitialized()
        {
            timer.Once(1f, () => BuildUI());
            timer.Once(3f, () =>
            {
                foreach(BasePlayer player in BasePlayer.activePlayerList)
                    player.gameObject.AddComponent<GodmodeComponent>();
            });
        }

        private void OnPlayerInit(BasePlayer player) => player.gameObject.AddComponent<GodmodeComponent>().IsWaiting = true;

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            var component = player.gameObject.GetComponent<GodmodeComponent>();
            if(component != null && component.IsWaiting) timer.Once(2f, () => component.IsWaiting = false);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if(ActiveIndicator.Contains(player)) ActiveIndicator.Remove(player);
            player.gameObject.GetComponent<GodmodeComponent>()?.DetachComponent();
        }

        private void Unload() { foreach(var player in BasePlayer.activePlayerList) player.gameObject.GetComponent<GodmodeComponent>()?.DetachComponent(); }


        #endregion

        #region -UI-


        private CuiElementContainer MainUI { get; set; }

        private const string mainPanel = "godmodeindicator.mainui";

        private void BuildUI()
        {
            string image = mainPanel + ".image";
            ImageLibrary?.Call("AddImage", Settings.UI_URL, image);
            MainUI = new CuiElementContainer();

            timer.Once(1f, () =>
            {
                MainUI.Add(new CuiElement
                {
                    Name = mainPanel,
                    Parent = "Hud",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Color = Settings.UI_Color,
                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                            Png = (string)ImageLibrary?.Call("GetImage", image),
                            FadeIn = 0.4f
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{Settings.UI_X.ToString()} {Settings.UI_Y.ToString()}",
                            AnchorMax = $"{(Settings.UI_X + 0.07f).ToString()} {(Settings.UI_Y + 0.1f).ToString()}"
                        }
                    },
                    FadeOut = 0.4f
                });
                UIBuilt = true;
            });
        }


        #endregion

        #region -Component-


        private bool UIBuilt { get; set; } = false;

        private class GodmodeComponent : MonoBehaviour
        {
            private BasePlayer Player { get; set; }
            private bool IsInGodMode { get; set; }
            internal bool IsWaiting { get; set; }
            private float LastUpdate { get; set; }

            private void Awake() => Player = GetComponent<BasePlayer>();

            private void Update()
            {
                if(Time.realtimeSinceStartup - LastUpdate > 1f)
                {
                    if(Player == null || !Player.IsConnected) DetachComponent();
                    else
                    {
                        if(Player.IsImmortal()) IsInGodMode = true;
                        else
                        {
                            if(Instance.Godmode != null && Instance.Godmode.IsLoaded && Instance.Godmode.Call<bool>("IsGod", Player.UserIDString)) IsInGodMode = true;
                            else IsInGodMode = false;
                        }
                    }
                    if(IsInGodMode && !Instance.ActiveIndicator.Contains(Player) && Instance.UIBuilt && !IsWaiting)
                    {
                        Instance.ActiveIndicator.Add(Player);
                        AddUi(Player, Instance.MainUI);
                    }
                    else if(!IsInGodMode && Instance.ActiveIndicator.Contains(Player))
                    {
                        Instance.ActiveIndicator.Remove(Player);
                        DestroyUi(Player, mainPanel);
                    }
                    LastUpdate = Time.realtimeSinceStartup;
                }
            }

            internal void DetachComponent() => Destroy(this);

            private void OnDestroy() => DestroyUi(Player, mainPanel);
        }


        #endregion

    }
}