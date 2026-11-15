using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace Utilities
{
    /// <summary>
    /// Continuous, analog view turning for the SteamVR Interaction System player.
    /// Rotation speed scales with how far the stick is tilted left/right. Pitch
    /// (looking up/down) is left to the headset itself, as is normal for room-scale
    /// VR: artificially tilting the whole play space fights the floor/locomotion
    /// logic and is uncomfortable, unlike a flat-screen FPS camera.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class SmoothTurn : MonoBehaviour
    {
        [SerializeField] private SteamVR_Input_Sources _turnHand = SteamVR_Input_Sources.RightHand;
        [SerializeField] private float _degreesPerSecond = 180f;
        [SerializeField] private float _deadZone = 0.15f;

        private Player _player;
        private SteamVR_Action_Vector2 _turnAction;

        private void Awake()
        {
            _player = GetComponent<Player>();
            _turnAction = SteamVR_Actions.turning_Turn;
        }

        private void Update()
        {
            if (_player.hmdTransform == null) return;

            // Same priority as the default set, so it adds a reading of the right stick
            // instead of masking the existing teleport binding on that same control.
            if (!SteamVR_Actions.turning.IsActive(_turnHand))
                SteamVR_Actions.turning.Activate(_turnHand, 0);

            var input = _turnAction.GetAxis(_turnHand);
            if (Mathf.Abs(input.x) <= _deadZone) return;

            RotateAroundFeet(input.x * _degreesPerSecond * Time.deltaTime);
        }

        // Rotates the play space around the player's feet so turning in place
        // doesn't move the player, matching SteamVR's own SnapTurn technique.
        private void RotateAroundFeet(float angle)
        {
            var feetOffset = _player.trackingOriginTransform.position - _player.feetPositionGuess;
            _player.trackingOriginTransform.position -= feetOffset;
            _player.transform.Rotate(Vector3.up, angle);
            feetOffset = Quaternion.Euler(0f, angle, 0f) * feetOffset;
            _player.trackingOriginTransform.position += feetOffset;
        }
    }
}
