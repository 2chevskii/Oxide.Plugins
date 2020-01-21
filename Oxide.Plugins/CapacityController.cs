using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Capacity Controller", "2CHEVSKII", "2.0.0")]
	[Description("Allows you to modify the sizes of certain containers")]
	class CapacityController : CovalencePlugin
	{
		#region -Fields-


		readonly Dictionary<string, string> shortnameToDisplayname = new Dictionary<string, string>();
		Configuration settings;


		#endregion

		#region -Core-


		string ShortnameToDisplayName(string shortname)
		{
			if(shortnameToDisplayname.ContainsKey(shortname))
			{
				return shortnameToDisplayname[shortname];
			}

			return string.Empty;
		}

		void Initialize()
		{
			ServerMgr.Instance.StartCoroutine(GetContainerItems(ItemManager.GetItemDefinitions(), (weapons, deployables) =>
			{
				Action<Dictionary<string, int>, ContainerItem> insertFunc = (dict, item) =>
				{
					var sntdn = ShortnameToDisplayName(item.shortname);

					if(string.IsNullOrEmpty(sntdn))
					{ 
						return;
					}

					if(!dict.ContainsKey(sntdn))
					{
						dict.Add(sntdn, item.capacity);
					}
				};

				weapons.ForEach(weapon => insertFunc(settings.Weapons, weapon));
				deployables.ForEach(deployable => insertFunc(settings.DeployableContainers, deployable));

				SaveConfig();

				//Puts(JsonConvert.SerializeObject(new { weapons, deployables }, Formatting.Indented)); // debug
			}));
		}

		IEnumerator GetContainerItems(IList<ItemDefinition> itemDefinitions, Action<List<ContainerItem>, List<ContainerItem>> callback)
		{
			var weapons = new List<ContainerItem>();
			var deployables = new List<ContainerItem>();
			for(int i = 0; i < itemDefinitions.Count; i++)
			{
				var definition = itemDefinitions[i];

				if(!definition)
				{
					continue;
				}

				var deployable = definition.GetComponent<ItemModDeployable>();
				var container = definition.GetComponent<ItemModContainer>();

				if(deployable || container)
				{
					if(deployable)
					{
						var entity = GameManager.server.CreateEntity(deployable.entityPrefab.resourcePath);

						if(entity is StorageContainer)
						{
							entity.Spawn();
							if(!shortnameToDisplayname.ContainsKey(entity.ShortPrefabName))
							{
								shortnameToDisplayname.Add(entity.ShortPrefabName, definition.displayName?.english);
							}

							deployables.Add(new ContainerItem().SetShortName(entity.ShortPrefabName).SetCapacity((entity as StorageContainer).inventory.capacity));

						}

						entity?.Kill();
					}
					else
					{
						var item = ItemManager.Create(definition);

						if(item?.GetHeldEntity() is BaseProjectile && item.contents?.capacity > 0)
						{
							if(!shortnameToDisplayname.ContainsKey(definition.shortname))
							{
								shortnameToDisplayname.Add(definition.shortname, definition.displayName?.english);
							}
							weapons.Add(new ContainerItem().SetShortName(definition.shortname).SetCapacity(container.capacity));
						}

						item?.Remove();
					}
				}
				yield return new WaitForEndOfFrame();
			}

			callback(weapons, deployables);
		}


		#endregion

		#region -uMod Hooks-


		void OnServerInitialized()
		{
			Initialize();
		}

		void OnEntitySpawned(StorageContainer entity)
		{
			if(!entity || entity.inventory == null)
			{
				return;
			}
			var sntdn = ShortnameToDisplayName(entity.ShortPrefabName);
			if(settings.DeployableContainers.ContainsKey(sntdn))
			{
				entity.inventory.capacity = settings.DeployableContainers[sntdn];
				entity.SendNetworkUpdate();
			}
		}

		void OnItemAddedToContainer(ItemContainer itemContainer, Item item)
		{
			if(item == null || item.info == null)
			{
				return;
			}
			var weapon = item.GetHeldEntity() as AttackEntity;
			var sntdn = ShortnameToDisplayName(item.info.shortname);
			if(weapon && settings.Weapons.ContainsKey(sntdn))
			{
				item.contents.capacity = settings.Weapons[sntdn];
				item.MarkDirty();
			}
		}


		#endregion

		#region -Nested Types-


		struct ContainerItem
		{
			public string shortname;
			public int capacity;

			public ContainerItem SetCapacity(int capacity)
			{
				this.capacity = capacity;
				return this;
			}

			public ContainerItem SetShortName(string shortname)
			{
				this.shortname = shortname;
				return this;
			}
		}


		#endregion

		#region -Configuration-

		#region [Types]


		class Configuration
		{
			public static Configuration Default => new Configuration
			{
				Weapons = new Dictionary<string, int>(),
				DeployableContainers = new Dictionary<string, int>()
			};

			[JsonProperty("Weapon attachment capacity")]
			public Dictionary<string, int> Weapons { get; set; }
			[JsonProperty("Deployable containers capacity")]
			public Dictionary<string, int> DeployableContainers { get; set; }

			public static bool operator !(Configuration settings)
			{
				return settings == null;
			}
		}


		#endregion

		#region [Methods]


		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				settings = Config.ReadObject<Configuration>();

				if(!settings)
				{
					throw new Exception();
				}
			}
			catch
			{
				LoadDefaultConfig();
			}
		}

		protected override void LoadDefaultConfig()
		{
			settings = Configuration.Default;
			SaveConfig();
		}

		protected override void SaveConfig() => Config.WriteObject(settings);


		#endregion

		#endregion
	}
}