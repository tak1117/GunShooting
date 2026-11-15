using System;
using System.Collections;
using StructureComponents;
using UnityEngine;

namespace Ammunition.GrenadeStructure
{
    public class GrenadeFuseExploder : MonoBehaviour
    {
        public event Action OnDetonate;

        private GrenadeFuseStriker _fuseStriker;

        public void Init(GrenadeFuseStriker striker)
        {
            _fuseStriker = striker;
            _fuseStriker.OnStrike += Strike;
        }

        private void Strike(float delay)
        {
            StartCoroutine(ExplosionDelay(delay));
            _fuseStriker.OnStrike -= Strike;
        }

        private IEnumerator ExplosionDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            // Applying the explosion force here too would double it: GrenadeExplosion.Detonate()
            // (invoked via OnDetonate below) is the single place that applies blast physics.
            OnDetonate?.Invoke();

            TryGetComponent<Attachment>(out var attachment);
            Destroy(attachment);
            Destroy(gameObject);
        }
    }
}