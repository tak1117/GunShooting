using System;
using System.Collections;
using JetBrains.Annotations;
using UnityEngine;
using Valve.VR.InteractionSystem;

namespace WeaponStructure
{
    public class WeaponSlide : MonoBehaviour
    {
        public event Action OnForward;
        public event Action OnBackward;

        [SerializeField] private float _distance;
        [SerializeField] private float _time = 0.25f;

        private const float RackThreshold = 0.85f;

        private bool _isRacking;
        private bool _isMovingForward;

        private Transform _startPos;
        private Transform _endPos;

        private Hand _attachedHand;
        private WeaponBarrel _weaponBarrel;

        private Coroutine _moveSlide;
        private Coroutine _moveBackwardSlide;
        private Coroutine _moveForwardSlide;

        private float _difference;
        private bool _isInPosition;
        private WeaponAudio _weaponAudio;

        public void Init(WeaponBarrel weaponBarrel, WeaponAudio weaponAudio)
        {
            _weaponAudio = weaponAudio;
            _weaponBarrel = weaponBarrel;

            CreateSlidePositions();

            _weaponBarrel.OnBulletFired += BackwardSlideDeploy;
        }

        public bool IsInPosition()
        {
            return _isInPosition;
        }

        public void ForwardSlide()
        {
            ForwardSlideDeploy();
        }

        /// <summary>
        /// Rack the slide without grabbing it: pull it fully back, then let it return forward
        /// (which chambers a cartridge from the inserted magazine, if any).
        /// </summary>
        public void Rack()
        {
            if (_attachedHand != null || _isRacking) return;
            StartCoroutine(RackRoutine());
        }

        private IEnumerator RackRoutine()
        {
            _isRacking = true;

            if (_moveForwardSlide != null) StopCoroutine(_moveForwardSlide);
            if (_moveBackwardSlide != null) StopCoroutine(_moveBackwardSlide);
            _isMovingForward = false;

            // MoveSlideBackward raises OnBackward at the end; with ammo in the magazine the
            // firing pin answers by sending the slide forward on its own.
            yield return StartCoroutine(MoveSlideBackward());
            yield return null;

            // Empty magazine: nobody sent it forward, so return it ourselves.
            if (!_isMovingForward && !_isInPosition && _attachedHand == null)
                yield return StartCoroutine(MoveSlideForward());

            while (_isMovingForward && _attachedHand == null) yield return null;
            _isRacking = false;
        }

        // Move slide to back position
        private void BackwardSlideDeploy()
        {
            if (_attachedHand == null) _moveBackwardSlide = StartCoroutine(MoveSlideBackward());
        }

        private void ForwardSlideDeploy()
        {
            if (_attachedHand == null) _moveForwardSlide = StartCoroutine(MoveSlideForward());
        }

        private void CreateSlidePositions()
        {
            var obj = new GameObject("startPos");
            var gateTransform = transform;
            var position = gateTransform.position;
            var rotation = gateTransform.rotation;
            var parentTransform = gateTransform.parent;

            _startPos = Instantiate(obj, position, rotation, parentTransform).transform;
            obj.name = "endPos";
            _endPos = Instantiate(obj, position - transform.forward * _distance, rotation,
                parentTransform).transform;

            Destroy(obj);
        }

        private IEnumerator MoveSlide()
        {
            _isInPosition = false;
            var reachedBack = false;

            while (true)
            {
                var gateTransform = transform;
                var forward = gateTransform.forward;
                var toHand = _attachedHand.transform.position - gateTransform.position;
                var localPosition = gateTransform.localPosition;
                var startZ = _startPos.localPosition.z;
                var endZ = _endPos.localPosition.z;
                var z = Vector3.Dot(forward, toHand) + localPosition.z - _difference;

                // Clamp instead of ignoring out-of-range values, otherwise a fast pull that
                // overshoots the end leaves the slide stuck short of it.
                z = Mathf.Clamp(z, Mathf.Min(startZ, endZ), Mathf.Max(startZ, endZ));
                transform.localPosition = new Vector3(localPosition.x, localPosition.y, z);

                // Count the slide as racked once it travels most of the way back (once per pull).
                var travel = Mathf.Approximately(startZ, endZ) ? 1f : (startZ - z) / (startZ - endZ);
                if (!reachedBack && travel >= RackThreshold)
                {
                    reachedBack = true;
                    OnBackward?.Invoke();
                }
                else if (reachedBack && travel < RackThreshold * 0.5f)
                {
                    reachedBack = false;
                }

                yield return null;
            }
        }

        private IEnumerator MoveSlideForward()
        {
            _isMovingForward = true;
            var t = 0f;
            var dummy = 0f;
            var startPosLocalPosition = _startPos.localPosition;
            var localPosition = transform.localPosition;
            var distance = Math.Abs(startPosLocalPosition.z - localPosition.z);
            var maxDistance = Math.Abs(startPosLocalPosition.z - _endPos.localPosition.z);
            var part = distance / maxDistance;
            var timeToMove = part * _time;

            while (t <= timeToMove)
            {
                transform.localPosition = Vector3.Lerp(localPosition, startPosLocalPosition, dummy);
                t += Time.deltaTime;
                dummy = t / timeToMove;

                yield return null;
            }

            transform.position = _startPos.position;
            _isInPosition = true;
            _isMovingForward = false;
            OnForward?.Invoke();
            _weaponAudio.PlayForwardSlideSound();
        }

        private IEnumerator MoveSlideBackward()
        {
            _isInPosition = false;

            var t = 0f;
            var dummy = 0f;
            var endPosLocalPosition = _endPos.localPosition;
            var localPosition = transform.localPosition;
            var distance = Math.Abs(endPosLocalPosition.z - localPosition.z);
            var maxDistance = Math.Abs(endPosLocalPosition.z - _startPos.localPosition.z);
            var part = distance / maxDistance;
            var timeToMove = part * _time;

            while (t <= timeToMove)
            {
                transform.localPosition = Vector3.Lerp(localPosition, endPosLocalPosition, dummy);
                t += Time.deltaTime;
                dummy = t / timeToMove;

                yield return null;
            }

            transform.position = _endPos.position;
            OnBackward?.Invoke();
            _weaponAudio.PlayBackwardSlideSound();
        }

        [UsedImplicitly]
        private void OnAttachedToHand(Hand hand)
        {
            _attachedHand = hand;

            var gateMoverTransform = transform;
            var forward = gateMoverTransform.forward;
            var toHand = _attachedHand.transform.position - gateMoverTransform.position;
            _difference = Vector3.Dot(forward, toHand);

            if (_moveForwardSlide != null) StopCoroutine(_moveForwardSlide);
            if (_moveBackwardSlide != null) StopCoroutine(_moveBackwardSlide);
            _isMovingForward = false;

            _moveSlide = StartCoroutine(MoveSlide());
        }

        [UsedImplicitly]
        private void OnDetachedFromHand(Hand hand)
        {
            if (_moveSlide != null) StopCoroutine(_moveSlide);
            _attachedHand = null;

            if (_moveForwardSlide != null) StopCoroutine(_moveForwardSlide);
            if (_moveBackwardSlide != null) StopCoroutine(_moveBackwardSlide);

            _moveForwardSlide = StartCoroutine(MoveSlideForward());
        }
    }
}