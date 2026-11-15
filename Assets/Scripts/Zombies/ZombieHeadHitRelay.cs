using Interfaces;
using Resources;
using UnityEngine;

namespace Zombies
{
    // Lets a bullet hitting the head collider (a separate child collider from the
    // body's) still register as a hit on the zombie, since IDamageable is only
    // looked up on the exact GameObject the raycast hit.
    public class ZombieHeadHitRelay : MonoBehaviour, IDamageable
    {
        [SerializeField] private Zombie _zombie;

        public void Damage(DamageData damageData) => _zombie.Damage(damageData);
    }
}
