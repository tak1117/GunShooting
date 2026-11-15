using JetBrains.Annotations;
using UnityEngine;
using Valve.VR.InteractionSystem;

namespace Utilities.Logger
{
    [RequireComponent(typeof(Throwable))]
    public class DummyLog : MonoBehaviour
    {
        [SerializeField] private bool _append;

        [UsedImplicitly]
        private void OnAttachedToHand(Hand hand)
        {
            InGameLogger.Log($"{gameObject.name}", _append);
        }

        [UsedImplicitly]
        private void OnDetachedFromHand(Hand hand)
        {
            InGameLogger.ClearLog();
        }
    }
}
