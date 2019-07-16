using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("IgnoreCollision", "2CHEVSKII", "1.1.1")]
    [Description("This plugin removes collisions between dropped items")]
    internal class IgnoreCollision : RustPlugin
    {

        #region [Fields]


        private bool ignore = true;
        private bool dynamicIgnore = true;

        private bool logs = true;
        private int ignoreStartAmount = 300;
        private int droppedItemCount = 0;
        private bool nowDisabled = false;


        #endregion

        #region [Config]


        protected override void LoadDefaultConfig() { }

        private void LoadConfigVariables()
        {
            CheckConfig("1.Disable collision", ref ignore);
            CheckConfig("2.Dynamic collision disabling", ref dynamicIgnore);
            CheckConfig("3.Amount to disable collision", ref ignoreStartAmount);
            CheckConfig("5.Log plugin activity", ref logs);
            SaveConfig();
        }

        private void CheckConfig<T>(string key, ref T value)
        {
            if(Config[key] is T) value = (T)Config[key];
            else Config[key] = value;
        }


        #endregion

        #region [Hooks]


        private void Init() => LoadConfigVariables();

        private void Loaded()
        {
            if(ignore)
            {
                if(!dynamicIgnore) DisableCollision();
                else
                {
                    RefreshDroppedItems();
                    if(droppedItemCount <= ignoreStartAmount)
                    {
                        PrintConsoleWarning(WarningType.MoreThan);
                        DisableCollision();
                    }
                }
            }
            PrintConsoleWarning(WarningType.Load);
        }

        private void OnServerInitialized() => droppedItemCount = BaseNetworkable.serverEntities.OfType<DroppedItem>().Count();

        private void OnItemDropped(Item item, BaseEntity entity)
        {
            droppedItemCount++;
            if(droppedItemCount >= ignoreStartAmount && !nowDisabled)
            {
                PrintConsoleWarning(WarningType.MoreThan);
                DisableCollision();
            }
        }

        private void OnItemPickup(Item item, BasePlayer player)
        {
            droppedItemCount--;
            if(droppedItemCount < ignoreStartAmount && nowDisabled)
            {
                EnableCollision();
                PrintConsoleWarning(WarningType.LessThan);
            }
        }

        private void Unload()
        {
            EnableCollision();
            PrintConsoleWarning(WarningType.Unload);
        }


        #endregion

        #region [Core]


        private void DisableCollision() { Physics.IgnoreLayerCollision(26, 26, true); nowDisabled = true; }

        private void EnableCollision() { Physics.IgnoreLayerCollision(26, 26, false); nowDisabled = false; }

        private void RefreshDroppedItems() => droppedItemCount = BaseNetworkable.serverEntities.OfType<DroppedItem>().Count();


        #endregion

        #region [Logs]


        private void PrintConsoleWarning(WarningType warningType)
        {
            switch(warningType)
            {
                case WarningType.Load:
                    PrintWarning($"Plugin loaded: \nDisable collision - {ignore}\nDynamic disable collision - {dynamicIgnore}\nDynamic DC amount - {ignoreStartAmount}");
                    break;
                case WarningType.Unload:
                    PrintWarning($"Plugin is being unloaded, all items collision enabled!");
                    break;
                case WarningType.MoreThan:
                    if(logs) PrintWarning($"Dropped item limit exceed ({ignoreStartAmount}) - collision disabled!");
                    break;
                case WarningType.LessThan:
                    if(logs) PrintWarning($"Dropped items less than limit ({ignoreStartAmount}) - collision enabled!");
                    break;
                default:
                    break;
            }
        }

        private enum WarningType { Load, Unload, MoreThan, LessThan }


        #endregion

    }
}