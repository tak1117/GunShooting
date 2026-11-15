using Ammunition.CartridgeStructure;
using StructureComponents;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;
using WeaponStructure;

namespace WeaponsRealization
{
    // A simplified break-action double-barrel with two fixed grip points (rear/trigger
    // and fore/barrel) instead of a single free-form hold. Either point can be grabbed
    // on its own to carry and aim the gun, but the trigger stays disabled until both
    // points are held at once, like bracing a real shotgun with both hands.
    [RequireComponent(typeof(WeaponAudio))]
    [RequireComponent(typeof(WeaponBarrel))]
    [RequireComponent(typeof(CollisionIgnoring))]
    [RequireComponent(typeof(Rigidbody))]
    public class DoubleBarrelGun : MonoBehaviour
    {
        [SerializeField] private Cartridge _cartridgePrefab;
        [SerializeField] private int _barrelCount = 2;
        [SerializeField] private float _reloadDelay = 1.2f;

        [SerializeField] private Transform _rearGrip;
        [SerializeField] private Transform _foreGrip;
        [SerializeField] private float _grabRadius = 0.12f;
        [SerializeField] private Vector3 _handAttachOffset = new Vector3(0f, -0.03f, 0.05f);

        [SerializeField] private SteamVR_Action_Single _trigger;
        [SerializeField] private SteamVR_Action_Vibration _vibration;

        [SerializeField] private SteamVR_Skeleton_Poser _rearPoser;
        [SerializeField] private SteamVR_Skeleton_Poser _forePoser;
        [SerializeField] private LaserSight _laserSight;

        private WeaponAudio _weaponAudio;
        private WeaponBarrel _weaponBarrel;
        private CollisionIgnoring _collisionIgnoring;
        private Rigidbody _rigidbody;
        private Collider _collider;

        private Hand _rearHand;
        private Hand _foreHand;
        private AxisPressDetector _triggerPressDetector;

        private int _shotsRemaining;
        private float _reloadReadyTime;
        private bool _reloading;
        private bool _wasHolding;

        private void Awake()
        {
            _weaponAudio = GetComponent<WeaponAudio>();
            _weaponBarrel = GetComponent<WeaponBarrel>();
            _collisionIgnoring = GetComponent<CollisionIgnoring>();
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();

            _collisionIgnoring.Init();
            _weaponBarrel.Init(_weaponAudio);
            _triggerPressDetector = new AxisPressDetector(_trigger);

            _shotsRemaining = _barrelCount;

            // Starts kinematic so it sits still on the counter instead of tipping/sliding off
            // from its own (intentionally tight) collider before anyone has picked it up.
            _rigidbody.isKinematic = true;
        }

        private void Update()
        {
            if (_reloading && Time.time >= _reloadReadyTime)
            {
                _shotsRemaining = _barrelCount;
                _reloading = false;
            }

            UpdateGripState();
            UpdateFiring();
        }

        private void LateUpdate()
        {
            ApplyPose();
        }

        private void UpdateGripState()
        {
            var newRearHand = FindGrippingHand(_rearGrip, _foreGrip, null);
            var newForeHand = FindGrippingHand(_foreGrip, _rearGrip, newRearHand);

            UpdateGripVisual(_rearHand, newRearHand, _rearPoser);
            UpdateGripVisual(_foreHand, newForeHand, _forePoser);

            _rearHand = newRearHand;
            _foreHand = newForeHand;

            if (_laserSight != null)
                _laserSight.SetVisible(_rearHand != null || _foreHand != null);
        }

        // Blends the hand's skeleton into the grip pose while it holds this point, and back to
        // normal tracked-hand motion on release, matching how the handgun poses its grip visually.
        private static void UpdateGripVisual(Hand previousHand, Hand currentHand, SteamVR_Skeleton_Poser poser)
        {
            if (previousHand == currentHand) return;

            if (previousHand != null && previousHand.HasSkeleton())
                previousHand.skeleton.BlendToSkeleton();

            if (currentHand != null && poser != null && currentHand.HasSkeleton())
                currentHand.skeleton.BlendToPoser(poser);
        }

        // Grabbing no longer requires the hand to be near the exact grip point: touching anywhere
        // on the gun's own collider and squeezing grip is enough. Which fixed point it attaches to
        // is decided by proximity (whichever of rear/fore is physically closer to the hand), so the
        // visual hand/gun pose still always snaps to one of the two defined grips, never a free spot.
        private Hand FindGrippingHand(Transform gripPoint, Transform otherGripPoint, Hand exclude)
        {
            if (gripPoint == null || Player.instance == null) return null;

            foreach (var hand in Player.instance.hands)
            {
                if (hand == null || hand == exclude) continue;
                if (hand.handType != SteamVR_Input_Sources.LeftHand && hand.handType != SteamVR_Input_Sources.RightHand)
                    continue;
                if (hand.currentAttachedObject != null) continue;
                if (hand.grabGripAction == null || !hand.grabGripAction.GetState(hand.handType)) continue;
                if (!IsNearWeapon(hand)) continue;

                if (otherGripPoint != null)
                {
                    var toThis = Vector3.Distance(hand.transform.position, gripPoint.position);
                    var toOther = Vector3.Distance(hand.transform.position, otherGripPoint.position);
                    if (toOther < toThis) continue;
                }

                return hand;
            }

            return null;
        }

        private bool IsNearWeapon(Hand hand)
        {
            if (_collider == null) return false;

            var closest = _collider.ClosestPoint(hand.transform.position);
            return Vector3.Distance(hand.transform.position, closest) <= _grabRadius;
        }

        private void ApplyPose()
        {
            var holding = _rearHand != null || _foreHand != null;

            // Only flip physics on an actual grab/release edge: forcing isKinematic every frame
            // while at rest (holding == false) fights the Rigidbody settling under gravity.
            if (holding != _wasHolding)
            {
                _rigidbody.isKinematic = holding;
                _wasHolding = holding;
            }

            if (!holding) return;

            if (_rearHand != null && _foreHand != null)
            {
                var rearPos = HandAttachPosition(_rearHand);
                var direction = HandAttachPosition(_foreHand) - rearPos;
                if (direction.sqrMagnitude < 0.0001f) return;

                transform.rotation = AxisLookRotation(direction, Vector3.up);
                transform.position = rearPos - transform.rotation * _rearGrip.localPosition;
            }
            else if (_rearHand != null)
            {
                SnapGripToHand(_rearGrip, _rearHand);
            }
            else
            {
                SnapGripToHand(_foreGrip, _foreHand);
            }
        }

        // Positions/rotates the gun so that gripPoint lands exactly on hand's pose
        // (gripPoint's local rotation is identity, so matching the hand's rotation
        // directly is enough; only the position needs the local-offset correction).
        private void SnapGripToHand(Transform gripPoint, Hand hand)
        {
            transform.rotation = hand.transform.rotation;
            transform.position = HandAttachPosition(hand) - transform.rotation * gripPoint.localPosition;
        }

        // The controller's tracked pose sits back near the wrist/strap, not the palm, which
        // left a visible gap between the hand and the gun. Offsetting forward and down (in the
        // hand's own local space) pulls the attach point toward where the fingers actually are.
        private Vector3 HandAttachPosition(Hand hand)
        {
            return hand.transform.TransformPoint(_handAttachOffset);
        }

        // Builds a rotation whose local +X axis (the barrel/muzzle direction) points along
        // xAxis, while keeping local +Y as close to upHint as possible (locks roll to world-up).
        private static Quaternion AxisLookRotation(Vector3 xAxis, Vector3 upHint)
        {
            xAxis.Normalize();
            var rotation = Quaternion.FromToRotation(Vector3.right, xAxis);
            var currentUp = rotation * Vector3.up;
            var projectedUp = Vector3.ProjectOnPlane(upHint, xAxis);
            if (projectedUp.sqrMagnitude > 0.0001f)
            {
                var rollFix = Quaternion.FromToRotation(currentUp, projectedUp.normalized);
                rotation = rollFix * rotation;
            }

            return rotation;
        }

        private void UpdateFiring()
        {
            var canFire = _rearHand != null && _foreHand != null;
            if (!canFire)
            {
                _triggerPressDetector.Reset();
                return;
            }

            // Firing is always read from the left hand, whichever grip point it's holding.
            var triggerHand = _rearHand.handType == SteamVR_Input_Sources.LeftHand ? _rearHand : _foreHand;
            if (_triggerPressDetector.UpdateAndCheckPressed(triggerHand.handType))
            {
                Fire();
            }
        }

        private void Fire()
        {
            if (_shotsRemaining <= 0) return;

            _shotsRemaining--;
            if (_shotsRemaining == 0)
            {
                _reloading = true;
                _reloadReadyTime = Time.time + _reloadDelay;
            }

            var cartridge = Instantiate(_cartridgePrefab);
            cartridge.Init();
            cartridge.Activate();
            _weaponBarrel.Fire(cartridge);

            ApplyHeavyKick();
        }

        private void ApplyHeavyKick()
        {
            _vibration.Execute(0f, 0.12f, 65f, 1f, _rearHand.handType);
            _vibration.Execute(0f, 0.08f, 55f, 0.8f, _foreHand.handType);
        }
    }
}
