using System;
using JetBrains.Annotations;
using UnityEngine;
using Valve.VR.InteractionSystem;

namespace StructureComponents
{
    public class Attachment : MonoBehaviour
    {
        public event Action OnDrop;
        public event Action OnTake;

        private Hand _attachedHand;

        public void Init()
        {
            _attachedHand = null;
        }

        public bool TryGetHand(out Hand hand)
        {
            hand = _attachedHand;

            return _attachedHand != null;
        }

        [UsedImplicitly]
        private void OnAttachedToHand(Hand hand)
        {
            // Already held by the other hand: reject this grab instead of stealing the
            // attachment, so the off-hand can brace the weapon (e.g. a two-handed hold)
            // without taking over the grip or hiding itself.
            if (_attachedHand != null && _attachedHand != hand)
            {
                hand.DetachObject(gameObject);
                return;
            }

            OnTake?.Invoke();
            _attachedHand = hand;
        }

        [UsedImplicitly]
        private void OnDetachedFromHand(Hand hand)
        {
            OnDrop?.Invoke();
            _attachedHand = null;
        }

        private void OnDestroy()
        {
            if (_attachedHand == null) return;
            _attachedHand.DetachObject(gameObject);
            _attachedHand.ResetTemporarySkeletonRangeOfMotion();
        }
    }
}