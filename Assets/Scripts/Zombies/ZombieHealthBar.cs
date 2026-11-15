using UnityEngine;

namespace Zombies
{
    public class ZombieHealthBar : MonoBehaviour
    {
        [SerializeField] private RectTransform _fill;
        [SerializeField] private float _fullWidth = 60f;

        private Camera _camera;

        private void Awake()
        {
            _camera = Camera.main;
        }

        public void SetHealth(float normalized)
        {
            if (_fill == null) return;
            var size = _fill.sizeDelta;
            size.x = _fullWidth * Mathf.Clamp01(normalized);
            _fill.sizeDelta = size;
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }

            transform.rotation = Quaternion.LookRotation(transform.position - _camera.transform.position, Vector3.up);
        }
    }
}
