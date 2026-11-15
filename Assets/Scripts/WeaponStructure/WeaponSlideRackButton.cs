using StructureComponents;
using UnityEngine;
using Valve.VR;

namespace WeaponStructure
{
    /// <summary>
    /// Racks the slide when the hand holding the weapon presses the rack button
    /// (B / Y on Touch-style controllers). Alternative to grabbing the slide by hand.
    /// </summary>
    [RequireComponent(typeof(Attachment))]
    public class WeaponSlideRackButton : MonoBehaviour
    {
        [SerializeField] private WeaponSlide _slide;

        private Attachment _attachment;
        private SteamVR_Action_Boolean _rackButton;

        private void Awake()
        {
            _attachment = GetComponent<Attachment>();
            if (_slide == null) _slide = GetComponentInChildren<WeaponSlide>();
        }

        private void Start()
        {
            _rackButton = SteamVR_Actions.default_PullY;
        }

        private void Update()
        {
            if (_slide == null || _rackButton == null) return;
            if (!_attachment.TryGetHand(out var hand)) return;
            if (_rackButton.GetStateDown(hand.handType)) _slide.Rack();
        }
    }
}
