using StructureComponents;
using UnityEngine;
using Valve.VR;
using WeaponsRealization;
using WeaponStructure;

namespace Utilities
{
    /// <summary>
    /// Temporary debug helper: logs every step of the pistol loading / firing chain
    /// (grab, magazine in/out, slide back/forward, chambering, trigger, shot) to the console.
    /// Put it on any active object in the scene; remove when no longer needed.
    /// </summary>
    public class WeaponDebugLogger : MonoBehaviour
    {
        private SteamVR_Action_Single _trigger;

        private void Start()
        {
            _trigger = SteamVR_Actions.default_Trigger;
            foreach (var pistol in FindObjectsOfType<Pistol>())
                Hook(pistol);
        }

        private void Hook(Pistol pistol)
        {
            var name = pistol.name;
            void Log(string message) => Debug.Log("[WeaponDebug] " + name + ": " + message);

            var attachment = pistol.GetComponent<Attachment>();
            attachment.OnTake += () => Log("grabbed");
            attachment.OnDrop += () => Log("dropped");

            var container = pistol.GetComponent<WeaponContainer>();
            container.OnEntered += () => Log("magazine inserted (has ammo: " + container.GetStored().CheckCartridge() + ")");
            container.OnPopped += () => Log("magazine ejected");

            var slide = pistol.GetComponentInChildren<WeaponSlide>();
            var slideInteractable = slide.GetComponent<Valve.VR.InteractionSystem.Interactable>();
            if (slideInteractable != null)
            {
                slideInteractable.onAttachedToHand += hand => Log("slide grabbed (" + hand.handType + ")");
                slideInteractable.onDetachedFromHand += hand => Log("slide released (" + hand.handType + ")");
            }
            slide.OnBackward += () => Log("slide reached back");
            slide.OnForward += () => Log("slide returned forward");

            var chamber = pistol.GetComponent<WeaponChamber>();
            chamber.OnCartridgeEnter += () => Log("cartridge chambered");

            pistol.GetComponent<WeaponTrigger>().OnTriggerPressed += () =>
                Log("trigger pressed (slide in position: " + slide.IsInPosition() + ", chambered: " + chamber.CheckCartridge() + ")");
            pistol.GetComponent<WeaponBarrel>().OnBulletFired += () => Log("SHOT FIRED");

            pistol.gameObject.AddComponent<TriggerAxisProbe>().Init(attachment, _trigger, Log);
        }

        private class TriggerAxisProbe : MonoBehaviour
        {
            private Attachment _attachment;
            private SteamVR_Action_Single _trigger;
            private System.Action<string> _log;
            private float _maxAxis;

            public void Init(Attachment attachment, SteamVR_Action_Single trigger, System.Action<string> log)
            {
                _attachment = attachment;
                _trigger = trigger;
                _log = log;
            }

            private void Update()
            {
                if (!_attachment.TryGetHand(out var hand)) return;
                var axis = _trigger[hand.handType].axis;
                if (axis > 0.2f && axis > _maxAxis + 0.2f)
                {
                    _maxAxis = axis;
                    _log("trigger axis " + axis.ToString("F2") + " (" + hand.handType + ", fires at > 0.90)");
                }
                if (axis < 0.1f) _maxAxis = 0f;
            }
        }
    }
}
