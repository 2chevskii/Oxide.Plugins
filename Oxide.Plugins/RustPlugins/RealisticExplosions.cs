using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Realistic Explosions", "2CHEVSKII", "0.1.1")]
    [Description("Pushes back dropped items when they are near the explosion")]
    internal class RealisticExplosions : RustPlugin
    {
        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity, ThrownWeapon item) => entity.gameObject.AddComponent<ExplosionComponent>();

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item) => entity.gameObject.AddComponent<ExplosionComponent>();

        private class ExplosionComponent : MonoBehaviour
        {
            private BaseEntity Entity { get; set; }

            private void Awake()
            {
                Entity = GetComponent<BaseEntity>();
            }

            private void OnDestroy()
            {
                List<DroppedItem> list = new List<DroppedItem>();
                Vis.Entities<DroppedItem>(Entity.transform.position, 15f, list);

                list.RemoveAll(item => item == null || item.IsDestroyed || !item.IsVisible(Entity.transform.position));

                foreach(var item in list)
                    item?.GetComponent<Rigidbody>()?.AddExplosionForce(500f, Entity.transform.position, 15f);
            }
        }
    }
}
