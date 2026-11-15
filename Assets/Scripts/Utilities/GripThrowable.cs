using Valve.VR.InteractionSystem;

namespace Utilities
{
    public class GripThrowable : Throwable
    {
        // True while a different hand already holds this object. Blocking the grab here
        // (before AttachObject ever runs) keeps the off-hand free to brace the weapon
        // instead of stealing/hiding via the default DetachFromOtherHand attach flag.
        private bool IsHeldByOtherHand(Hand hand) =>
            interactable.attachedToHand != null && interactable.attachedToHand != hand;

        protected override void OnHandHoverBegin(Hand hand)
        {
            if (attached || catchingSpeedThreshold == -1 || IsHeldByOtherHand(hand)) return;

            var catchingThreshold = catchingSpeedThreshold *
                                    SteamVR_Utils.GetLossyScale(Player.instance.trackingOriginTransform);

            var bestGrabType = hand.GetBestGrabbingType();

            if (bestGrabType != GrabTypes.Grip) return;

            if (rigidbody.velocity.magnitude >= catchingThreshold)
                hand.AttachObject(gameObject, bestGrabType, attachmentFlags);
        }

        protected override void HandHoverUpdate(Hand hand)
        {
            if (IsHeldByOtherHand(hand)) return;

            var startingGrabType = hand.GetGrabStarting();

            if (startingGrabType != GrabTypes.Grip) return;

            hand.AttachObject(gameObject, startingGrabType, attachmentFlags, attachmentOffset);
            hand.HideGrabHint();
        }
    }
}