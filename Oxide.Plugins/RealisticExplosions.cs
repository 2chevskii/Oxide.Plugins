using System.Collections.Generic;
using UnityEngine;

/* TODO:
 - Attach to rockets ✔
 - Detect non-exploded grenades/satchels
 */

namespace Oxide.Plugins
{
	[Info("Realistic Explosions", "2CHEVSKII", "0.2.1")]
	[Description("Pushes back dropped items when they are near the explosion")]
	internal class RealisticExplosions : RustPlugin
	{

		#region -Hooks-


		private void OnExplosiveDropped(BasePlayer player, BaseEntity entity, ThrownWeapon item) => entity.gameObject.AddComponent<ExplosionComponent>();

		private void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item) => entity.gameObject.AddComponent<ExplosionComponent>();

		private void OnRocketLaunched(BasePlayer player, BaseEntity entity) => entity.gameObject.AddComponent<ExplosionComponent>();


		#endregion

		#region -Component-


		private class ExplosionComponent : MonoBehaviour
		{
			private BaseEntity Entity { get; set; }

			private void Awake() => Entity = GetComponent<BaseEntity>();

			private void OnDestroy()
			{
				List<DroppedItem> list = new List<DroppedItem>();
				Vis.Entities<DroppedItem>(Entity.transform.position, 15f, list);

				list.RemoveAll(item => item == null || item.IsDestroyed || !item.IsVisible(Entity.transform.position));

				foreach(DroppedItem item in list)
					item?.GetComponent<Rigidbody>()?.AddExplosionForce(500f, Entity.transform.position, 15f);
			}
		}


		#endregion

	}
}
