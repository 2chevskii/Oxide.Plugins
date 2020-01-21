﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

/* TODO:
* Attachments // not finished
*/

namespace Oxide.Plugins
{
	[Info("NPC Drop Gun", "2CHEVSKII", "1.7.2")]
	[Description("Forces NPC to drop used gun and some ammo after death")]
	internal class NPCDropGun : RustPlugin
	{

		#region -Configuration-


		/// <summary>
		/// Handles all the variables
		/// </summary>
		private void LoadConfiguration()
		{
			CheckConfig("1.Types of loot to spawn", "Guns", ref dropGuns);
			CheckConfig("1.Types of loot to spawn", "Ammo", ref dropAmmo);
			CheckConfig("1.Types of loot to spawn", "Medicine", ref dropMeds);
			CheckConfig("3.Amounts", "Ammo Max", ref maxAmmo);
			CheckConfig("3.Amounts", "Ammo Min", ref minAmmo);
			CheckConfigFloat("3.Amounts", "Condition Max", ref maxCondition);
			CheckConfigFloat("3.Amounts", "Condition Min", ref minCondition);
			CheckConfig("3.Amounts", "Meds Max", ref maxMeds);
			CheckConfig("3.Amounts", "Meds Min", ref minMeds);
			CheckConfig("3.Amounts", "Gun Ammo Max", ref maxGunAmmo);
			CheckConfig("3.Amounts", "Gun Ammo Min", ref minGunAmmo);
			CheckConfig("2.Chances of drop", "Guns", ref chanceToDropGun);
			CheckConfig("2.Chances of drop", "Ammo", ref chanceToDropAmmo);
			CheckConfig("2.Chances of drop", "Meds", ref chanceToDropMeds);
			CheckConfig("4.Utility", "Remove default loot", ref removeDefLoot);
			CheckConfig("4.Utility", "Put held items into inventory", ref putGunIntoInv);
			CheckConfig("4.Utility", "Drop items near the corpse if it's full", ref dropnearcorpse);
			CheckConfig("4.Utility", "Assign random skin to item", ref assignRandomSkin);

			//Condition of weapons needs to be normalized

			Mathf.Clamp(minCondition, 0.0f, 1.0f);
			Mathf.Clamp(maxCondition, minCondition, 1.0f);
			Mathf.Clamp(maxAttachments, 0, 3);

			SaveConfig();
		}

		/// <summary>
		/// Generic var handling allows escape mistakes with types
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="category"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		private void CheckConfig<T>(string category, string key, ref T value)
		{
			if(Config[category, key] is T)
				value = (T)Config[category, key];
			else
				Config[category, key] = value;
		}

		/// <summary>
		/// Needed because config returns either double or integer
		/// </summary>
		/// <param name="category"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		private void CheckConfigFloat(string category, string key, ref float value)
		{
			if(Config[category, key] is double || Config[category, key] is int)
				value = Convert.ToSingle(Config[category, key]);
			else
				Config[category, key] = value;
		}

		/// <summary>
		/// Just calls new config file generation
		/// </summary>
		protected override void LoadDefaultConfig() { }


		#endregion

		#region -Fields-


		private int ammoType { get; set; }

		private string heldWeapon { get; set; }

		/// <summary>
		/// Needed if weapon get put into corpse along with other loot
		/// </summary>
		private Queue<Item> delayedWeapons = new Queue<Item>();

		private bool dropGuns = true;
		private bool dropAmmo = true;
		private bool dropMeds = true;

		private int minAmmo = 20;
		private int maxAmmo = 120;

		private int minGunAmmo = 0;
		private int maxGunAmmo = 30;

		private float attachmentChance = 0.3f;
		private int maxAttachments = 3;

		private float minCondition = 0.1f;
		private float maxCondition = 1.0f;

		private int minMeds = 0;
		private int maxMeds = 5;

		private double chanceToDropGun = 1;
		private double chanceToDropAmmo = 1;
		private double chanceToDropMeds = 1;

		private bool removeDefLoot = false;
		private bool putGunIntoInv = false;
		private bool dropnearcorpse = true;
		private bool assignRandomSkin = true;

		private int[] attachments = new[] { 952603248, -132516482, 442289265, 567235583, -1405508498, 1478091698, -855748505 };


		#endregion

		#region -Hooks-


		private void Init() => LoadConfiguration();

		/// <summary>
		/// Checks for BotSpawn plugin as it may cause errors =(
		/// </summary>
		private void Loaded() { if((bool)Manager.GetPlugin("BotSpawn")) PrintWarning($"BotSpawn plugin found! Some ammo and loot might not be handled correctly!"); }

		/// <summary>
		/// Called when bot dies
		/// </summary>
		/// <param name="entity"></param>
		/// <param name="info"></param>
		private void OnEntityDeath(BaseCombatEntity entity)
		{
			if(entity != null)
			{
				if(entity is HTNPlayer || entity is NPCPlayer)
				{
					BasePlayer basePlayer = entity as BasePlayer;
					if(basePlayer != null)
						ItemSpawner(basePlayer.GetHeldEntity(), basePlayer.transform.position + new Vector3(0, 1.5f, 0), basePlayer.eyes.bodyRotation);
				}
			}
		}

		/// <summary>
		/// Called when bot corpse spawned
		/// </summary>
		/// <param name="entity"></param>
		private void OnEntitySpawned(BaseNetworkable entity)
		{
			if(entity is NPCPlayerCorpse)
				MethodThatPutsLootIntoCorpse(entity);
		}


		#endregion

		#region -Core-


		/// <summary>
		/// Chooses the ammo type for weapon
		/// </summary>
		/// <param name="ammoString"></param>
		private void AmmoAssigner(string ammoString)
		{
			switch(ammoString)
			{
				case "lr300.entity":
					ammoType = -1211166256;
					break;
				case "ak47u.entity":
					ammoType = -1211166256;
					break;
				case "semi_auto_rifle.entity":
					ammoType = -1211166256;
					break;
				case "m249.entity":
					ammoType = -1211166256;
					break;
				case "spas12.entity":
					ammoType = -1685290200;
					break;
				case "m92.entity":
					ammoType = 785728077;
					break;
				case "mp5.entity":
					ammoType = 785728077;
					break;
				default:
					ammoType = 0;
					break;
			}
		}

		/// <summary>
		/// Simulates "weapon dropped from hand"
		/// </summary>
		/// <param name="heldEntity"></param>
		/// <param name="dropPosition"></param>
		/// <param name="dropRotation"></param>
		private void ItemSpawner(BaseEntity heldEntity, Vector3 dropPosition, Quaternion dropRotation) //Drops gun at npc's pos
		{
			if(dropPosition != null && heldEntity != null && dropGuns && Random.Range(0f, 1f) <= chanceToDropGun && dropRotation != null)
			{
				Item weapon = null;
				if(heldEntity.GetItem() != null)
				{
					ItemDefinition definition = heldEntity.GetItem().info;
					weapon = ItemManager.Create(heldEntity.GetItem().info, 1, assignRandomSkin ? GetRandomSkin(definition) : 0uL);
				}
				if(weapon != null)
				{
					if(weapon.hasCondition)
						weapon.conditionNormalized = Random.Range(minCondition, maxCondition);

					BaseProjectile projectile = weapon.GetHeldEntity()?.GetComponent<BaseProjectile>();
					if(projectile != null)
						projectile.primaryMagazine.contents = Random.Range(minGunAmmo, Mathf.Min(maxGunAmmo, projectile.primaryMagazine.capacity));

					if(attachmentChance > 0)
					{
						var count = 0;

						for(var i = 0; i < maxAttachments; i++)
						{
							if(Random.Range(0f, 1f) <= attachmentChance)
							{
								count++;
							}
						}
						SetRandomAttachments(weapon, count);
					}

					if(putGunIntoInv)
						delayedWeapons.Enqueue(weapon);
					else
					{
						weapon.CreateWorldObject(dropPosition, dropRotation);
						if(weapon.GetWorldEntity() != null)
						{
							weapon.GetWorldEntity().SetVelocity(new Vector3(Random.Range(-1f, 1f), Random.Range(1f, 2.5f), Random.Range(-1f, 1f)) * Random.Range(0, 2f));
							weapon.GetWorldEntity().SetAngularVelocity(new Vector3(Random.Range(-10f, 10f) * Random.Range(0, 5f), Random.Range(-10f, 10f) * Random.Range(0, 5f), Random.Range(-10f, 10f)) * Random.Range(0, 5f));
						}
					}
				}
			}

			heldWeapon = heldEntity?.ShortPrefabName;
			if(heldWeapon != null)
				AmmoAssigner(heldWeapon);

		}

		/// <summary>
		/// Spawns loot inside the corpse
		/// </summary>
		/// <param name="entity"></param>
		private void MethodThatPutsLootIntoCorpse(BaseNetworkable entity)
		{
			if(entity != null)
			{
				Item weapon = null;

				if(delayedWeapons.Count > 0 && putGunIntoInv)
					weapon = delayedWeapons.Dequeue();

				Item ammo = null;

				if(dropAmmo && ammoType != 0 && Random.Range(0f, 1f) <= chanceToDropAmmo) //check if it needs to spawn ammunition
				{
					int tempAmount = Random.Range(minAmmo, maxAmmo); //amount
					if(tempAmount > 0)
						ammo = ItemManager.CreateByItemID(ammoType, tempAmount);
				}

				Item meds = null;

				if(dropMeds && Random.Range(0f, 1f) <= chanceToDropMeds) //check if it needs to spawn meds
				{
					int tempAmount = Random.Range(minMeds, maxMeds);
					if(tempAmount > 0)
						meds = ItemManager.CreateByItemID(1079279582, tempAmount);
				}

				PlayerCorpse corpse = entity.GetComponent<PlayerCorpse>();

				if(corpse != null && corpse.containers != null && corpse.containers[0] != null && corpse.containers[1] != null && corpse.containers[2] != null)
				{
					NextTick(() => //one tick delay is necessary to let the corpse fully spawn
					{
						if(removeDefLoot)
							corpse.containers[0].Clear();

						if(ammo != null && corpse != null && !corpse.containers[0].IsFull())
							ammo.MoveToContainer(corpse.containers[0]);

						else if(ammo != null && corpse != null && corpse.containers[0].IsFull() && dropnearcorpse)
						{
							ammo.CreateWorldObject(entity.transform.position + new Vector3(Random.Range(0f, 0.5f), Random.Range(0f, 0.5f), Random.Range(0f, 0.5f)));
							if((bool)ammo.GetWorldEntity())
							{
								ammo.GetWorldEntity().SetVelocity(new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 1f), Random.Range(-1f, 1f)) * Random.Range(0, 2f));
								ammo.GetWorldEntity().SetAngularVelocity(new Vector3(Random.Range(-10f, 10f), Random.Range(-10f, 10f), Random.Range(-10f, 10f)) * Random.Range(0, 2f));
							}
						}

						if(meds != null && corpse != null && !corpse.containers[0].IsFull())
							meds.MoveToContainer(corpse.containers[0]);

						else if(meds != null && corpse != null && corpse.containers[0].IsFull() && dropnearcorpse)
						{
							meds.CreateWorldObject(entity.transform.position + new Vector3(Random.Range(0f, 0.5f), Random.Range(0f, 0.5f), Random.Range(0f, 0.5f)));
							if((bool)meds.GetWorldEntity())
							{
								meds.GetWorldEntity().SetVelocity(new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 1f), Random.Range(-1f, 1f)) * Random.Range(0, 2f));
								meds.GetWorldEntity().SetAngularVelocity(new Vector3(Random.Range(-10f, 10f), Random.Range(-10f, 10f), Random.Range(-10f, 10f)) * Random.Range(0, 2f));
							}
						}

						if(weapon != null && corpse != null && !corpse.containers[2].IsFull())
							weapon.MoveToContainer(corpse.containers[2]);

						else if(weapon != null && corpse != null && corpse.containers[2].IsFull() && !corpse.containers[0].IsFull())
							weapon.MoveToContainer(corpse.containers[0]);

						else if(weapon != null && corpse != null && corpse.containers[0].IsFull() && dropnearcorpse && corpse.containers[2].IsFull())
						{
							weapon.CreateWorldObject(entity.transform.position + new Vector3(Random.Range(0f, 0.5f), Random.Range(0f, 0.5f), Random.Range(0f, 0.5f)));
							if((bool)weapon.GetWorldEntity())
							{
								weapon.GetWorldEntity().SetVelocity(new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 1f), Random.Range(-1f, 1f)) * Random.Range(0, 2f));
								weapon.GetWorldEntity().SetAngularVelocity(new Vector3(Random.Range(-10f, 10f), Random.Range(-10f, 10f), Random.Range(-10f, 10f)) * Random.Range(0, 2f));
							}
						}
					});
				}
			}
		}


		private ulong GetRandomSkin(ItemDefinition idef)
		{
			if(!idef || (idef.skins.Length < 1 && idef.skins2.Length < 1))
				return 0;
			List<int> skins = new List<int>();

			skins.AddRange(from sk in idef.skins select sk.id);
			skins.AddRange(from sk in idef.skins2 select sk.Id);

			return ItemDefinition.FindSkin(idef.itemid, skins.GetRandom());
		}

		private void SetRandomAttachments(Item weapon, int count)
		{
			if(weapon != null && weapon.contents != null)
			{
				for(var i = 0; i < count; i++)
				{
					var attachment = ItemManager.CreateByItemID(attachments.GetRandom());
					if(attachment != null && weapon.contents.CanTake(attachment))
					{
						weapon.contents.Insert(attachment);
					}
				}
			}
		}


		#endregion

	}
}
