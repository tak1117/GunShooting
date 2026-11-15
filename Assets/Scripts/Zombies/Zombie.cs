using System;
using Interfaces;
using Resources;
using UnityEngine;

namespace Zombies
{
    public class Zombie : MonoBehaviour, IDamageable
    {
        public event Action OnDied;

        [SerializeField] private int _maxHealth = 100;
        [SerializeField] private int _damagePerHit = 20;
        [SerializeField] private float _moveSpeed = 1.2f;
        [SerializeField] private float _arriveDistance = 0.4f;
        [SerializeField] private ZombieHealthBar _healthBar;
        [SerializeField] private Renderer[] _hitFlashRenderers;

        private int _currentHealth;
        private bool _isActive;
        private bool _isDead;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        [SerializeField] private Vector3 _wanderMin;
        [SerializeField] private Vector3 _wanderMax;
        private Vector3 _wanderTarget;
        private Color[] _originalColors;
        private Collider _collider;

        private void Awake()
        {
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _collider = GetComponent<Collider>();

            if (_hitFlashRenderers != null)
            {
                _originalColors = new Color[_hitFlashRenderers.Length];
                for (var i = 0; i < _hitFlashRenderers.Length; i++)
                    _originalColors[i] = _hitFlashRenderers[i].material.color;
            }
        }

        private void Start()
        {
            _currentHealth = _maxHealth;
            _healthBar?.gameObject.SetActive(false);
        }

        // The area (in world space, XZ) the zombie is allowed to wander within.
        public void SetWanderBounds(Vector3 min, Vector3 max)
        {
            _wanderMin = min;
            _wanderMax = max;
        }

        public void Activate()
        {
            if (_isDead) return;
            _isActive = true;
            PickNewWanderTarget();
        }

        public void ResetZombie()
        {
            StopAllCoroutines();
            _isDead = false;
            _isActive = false;
            _currentHealth = _maxHealth;
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            gameObject.SetActive(true);
            if (_collider != null) _collider.enabled = true;
            _healthBar?.SetHealth(1f);
            _healthBar?.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_isDead || !_isActive) return;

            var toTarget = _wanderTarget - transform.position;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;

            if (distance <= _arriveDistance)
            {
                PickNewWanderTarget();
                return;
            }

            var step = Mathf.Min(_moveSpeed * Time.deltaTime, distance);
            transform.position += toTarget.normalized * step;
            transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        }

        private void PickNewWanderTarget()
        {
            var x = UnityEngine.Random.Range(_wanderMin.x, _wanderMax.x);
            var z = UnityEngine.Random.Range(_wanderMin.z, _wanderMax.z);
            _wanderTarget = new Vector3(x, transform.position.y, z);
        }

        public void Damage(DamageData damageData)
        {
            if (_isDead) return;

            _currentHealth = Mathf.Max(0, _currentHealth - _damagePerHit);

            if (_healthBar != null)
            {
                _healthBar.gameObject.SetActive(true);
                _healthBar.SetHealth(_currentHealth / (float)_maxHealth);
            }

            FlashHit();

            if (_currentHealth == 0) Die();
        }

        private void FlashHit()
        {
            if (_hitFlashRenderers == null) return;
            foreach (var rend in _hitFlashRenderers)
                rend.material.color = Color.red;

            CancelInvoke(nameof(RestoreColors));
            Invoke(nameof(RestoreColors), 0.1f);
        }

        private void RestoreColors()
        {
            if (_hitFlashRenderers == null) return;
            for (var i = 0; i < _hitFlashRenderers.Length; i++)
                _hitFlashRenderers[i].material.color = _originalColors[i];
        }

        private void Die()
        {
            _isDead = true;
            _isActive = false;
            if (_collider != null) _collider.enabled = false;
            _healthBar?.gameObject.SetActive(false);
            OnDied?.Invoke();
            StartCoroutine(SinkAndDisable());
        }

        private System.Collections.IEnumerator SinkAndDisable()
        {
            const float duration = 1f;
            var t = 0f;
            var startScale = transform.localScale;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t / duration);
                yield return null;
            }

            gameObject.SetActive(false);
            transform.localScale = startScale;
        }
    }
}
