using System;
using Interfaces;
using Resources;
using UnityEngine;

namespace Ammunition.CartridgeStructure
{
    public class CartridgeBulletImpact : MonoBehaviour
    {
        public Action Hit;

        [SerializeField] private float _impactMultiplier;
        [SerializeField] private float _rayLength;

        private Rigidbody _rb;

        public void Init(Rigidbody rb)
        {
            enabled = false;
            _rb = rb;
        }

        // Checked every physics step along the bullet's velocity, looking ahead slightly further than it will
        // travel this step, so hits register before the rigidbody bounces off the collider (independent of FPS).
        private void FixedUpdate()
        {
            var velocity = _rb.velocity;
            var speed = velocity.magnitude;
            var direction = speed > 0.1f ? velocity / speed : transform.forward;
            var distance = Mathf.Max(speed * Time.fixedDeltaTime * 1.5f, _rayLength, 0.05f);

            if (!Physics.Raycast(transform.position, direction, out var hit, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;

            var obj = hit.transform.gameObject;

            if (!obj.TryGetComponent<IDamageable>(out var component)) return;

            var contactPoint = hit.point;
            var normal = hit.normal;
            var normalPoint = Quaternion.LookRotation(normal);

            var data = new DamageData
            {
                contactPoint = contactPoint,
                normalPoint = normalPoint,
                force = transform.forward * (_rb.velocity.magnitude * _rb.mass) / _impactMultiplier
            };

            component.Damage(data);
            Debug.Log("[Hit] " + obj.name + " at " + contactPoint.ToString("F2"));

            Hit?.Invoke();
        }
    }
}