using UnityEngine;

namespace WeaponStructure
{
    [RequireComponent(typeof(LineRenderer))]
    public class LaserSight : MonoBehaviour
    {
        [SerializeField] private Transform _origin;
        [SerializeField] private float _maxDistance = 50f;

        private LineRenderer _line;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.enabled = false;
            _line.useWorldSpace = true;
        }

        public void SetVisible(bool visible)
        {
            _line.enabled = visible;
        }

        private void Update()
        {
            if (!_line.enabled) return;

            var start = _origin.position;
            var direction = _origin.forward;
            var end = Physics.Raycast(start, direction, out var hit, _maxDistance)
                ? hit.point
                : start + direction * _maxDistance;

            _line.SetPosition(0, start);
            _line.SetPosition(1, end);
        }
    }
}
