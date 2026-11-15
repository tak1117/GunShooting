using System;
using System.Collections;
using JetBrains.Annotations;
using UnityEngine;
using Valve.VR.InteractionSystem;

namespace Equipment.Head
{
    public class HelmetVisor : MonoBehaviour
    {
        public event Action VisorMoved;
        
        [SerializeField] private float _maxAngle;
        [SerializeField] private float _minAngle;
        [SerializeField] private float _time;

        private bool _inMaxRotation;
        private bool _inMinRotation;

        public void Init()
        {
            _inMaxRotation = false;
            _inMinRotation = true;
        }

        private IEnumerator RotateVisor(float targetAngle, bool resultingInMaxRotation)
        {
            var rotation = Quaternion.Euler(Vector3.right * targetAngle);
            var t = 0f;
            var dummy = 0f;

            while (t <= _time)
            {
                transform.localRotation = Quaternion.Lerp(transform.localRotation, rotation, dummy);
                t += Time.deltaTime;
                dummy = t / _time;

                yield return null;
            }

            transform.localRotation = rotation;

            _inMaxRotation = resultingInMaxRotation;
            _inMinRotation = !resultingInMaxRotation;
        }

        [UsedImplicitly]
        private void OnAttachedToHand(Hand hand)
        {
            if (!_inMaxRotation)
            {
                StartCoroutine(RotateVisor(_maxAngle, resultingInMaxRotation: true));
                VisorMoved?.Invoke();
            }
            else if (!_inMinRotation)
            {
                StartCoroutine(RotateVisor(_minAngle, resultingInMaxRotation: false));
                VisorMoved?.Invoke();
            }
        }
    }
}