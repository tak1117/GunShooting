using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace Utilities
{
    /// <summary>
    /// Thumbstick walking for the SteamVR Interaction System player.
    /// Left stick moves relative to where the head is looking; the right stick keeps
    /// the default teleport / snap turn. Keeps the player's feet on the ground and
    /// stops at walls.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class SmoothLocomotion : MonoBehaviour
    {
        [SerializeField] private SteamVR_Input_Sources _moveHand = SteamVR_Input_Sources.LeftHand;
        [SerializeField] private float _speed = 2f;
        [SerializeField] private float _deadZone = 0.15f;

        [Header("Collision")]
        [Tooltip("Layers treated as floor and walls.")]
        [SerializeField] private LayerMask _environmentMask = 1 << 6; // "Baked"
        [SerializeField] private float _bodyRadius = 0.2f;
        [Tooltip("Highest step the player can walk onto.")]
        [SerializeField] private float _stepHeight = 0.35f;
        [Tooltip("How far below the feet to look for ground before giving up.")]
        [SerializeField] private float _maxDrop = 3f;

        private Player _player;
        private SteamVR_Action_Vector2 _moveAction;

        private void Awake()
        {
            _player = GetComponent<Player>();
            _moveAction = SteamVR_Actions.platformer_Move;
        }

        private void Start()
        {
            SnapToGround();
        }

        [Header("Debug")]
        [SerializeField] private bool _logDiagnostics = false;
        private float _nextLogTime;

        public string Diagnostics()
        {
            var head = _player != null ? _player.hmdTransform : null;
            return "setActive=" + SteamVR_Actions.platformer.IsActive(_moveHand) +
                   " actionActive=" + _moveAction.GetActive(_moveHand) +
                   " bound=" + _moveAction.GetActiveBinding(_moveHand) +
                   " axis=" + _moveAction.GetAxis(_moveHand) +
                   " anyAxis=" + _moveAction.GetAxis(SteamVR_Input_Sources.Any) +
                   " head=" + (head != null ? head.name : "null") +
                   " pos=" + transform.position;
        }

        private void Update()
        {
            EnsureMoveActionSetActive();

            if (_logDiagnostics && Time.time >= _nextLogTime)
            {
                _nextLogTime = Time.time + 2f;
                Debug.Log("[SmoothLocomotion] " + Diagnostics());
            }

            var head = _player.hmdTransform;
            if (head == null) return;

            var input = _moveAction.GetAxis(_moveHand);
            if (input.magnitude > _deadZone)
            {
                var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                var right = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
                var move = (forward * input.y + right * input.x) * (_speed * Time.deltaTime);
                if (move.sqrMagnitude > 1f * 1f) move = move.normalized; // safety for frame spikes

                transform.position += BlockByWalls(head.position, move);
            }

            SnapToGround();
        }

        private void EnsureMoveActionSetActive()
        {
            // Higher priority than the default set, so on the move hand the stick walks
            // instead of triggering teleport / snap turn. The other hand keeps those.
            if (!SteamVR_Actions.platformer.IsActive(_moveHand))
                SteamVR_Actions.platformer.Activate(_moveHand, 1);
        }

        private Vector3 BlockByWalls(Vector3 headPosition, Vector3 move)
        {
            var feetY = transform.position.y;
            var bottom = new Vector3(headPosition.x, feetY + _stepHeight + _bodyRadius, headPosition.z);
            var top = new Vector3(headPosition.x, Mathf.Max(bottom.y, headPosition.y - _bodyRadius), headPosition.z);
            var distance = move.magnitude;
            var direction = move / distance;

            if (!Physics.CapsuleCast(bottom, top, _bodyRadius, direction, out var hit, distance + 0.01f,
                    _environmentMask, QueryTriggerInteraction.Ignore))
                return move;

            // Slide along the wall instead of stopping dead.
            var allowed = direction * Mathf.Max(0f, hit.distance - 0.01f);
            var remaining = Vector3.ProjectOnPlane(move - allowed, hit.normal);
            remaining.y = 0f;
            if (remaining.sqrMagnitude > 0f &&
                Physics.CapsuleCast(bottom + allowed, top + allowed, _bodyRadius, remaining.normalized,
                    remaining.magnitude + 0.01f, _environmentMask, QueryTriggerInteraction.Ignore))
                remaining = Vector3.zero;

            return allowed + remaining;
        }

        private void SnapToGround()
        {
            var head = _player.hmdTransform;
            var xz = head != null ? head.position : transform.position;
            var origin = new Vector3(xz.x, transform.position.y + _stepHeight, xz.z);

            if (Physics.Raycast(origin, Vector3.down, out var hit, _stepHeight + _maxDrop, _environmentMask,
                    QueryTriggerInteraction.Ignore))
            {
                var position = transform.position;
                position.y = hit.point.y;
                transform.position = position;
            }
        }
    }
}
