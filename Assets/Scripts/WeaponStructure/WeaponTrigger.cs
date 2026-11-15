using System;
using StructureComponents;
using UnityEngine;
using Valve.VR;

namespace WeaponStructure
{
    public class WeaponTrigger : MonoBehaviour
    {
        public event Action OnTriggerPressed;

        [SerializeField] private SteamVR_Action_Single _trigger;
        [SerializeField] private SteamVR_Action_Vibration _vibration;

        private Attachment _attachment;
        private AxisPressDetector _pressDetector;
        private bool _isInit;

        public void Init(Attachment attachment)
        {
            _attachment = attachment;
            _pressDetector = new AxisPressDetector(_trigger);
            _isInit = true;
        }

        private void Update()
        {
            if (!_isInit) return;
            if (!_attachment.TryGetHand(out var hand)) return;

            if (_pressDetector.UpdateAndCheckPressed(hand.handType))
            {
                Pulse(hand.handType);
                OnTriggerPressed?.Invoke();
            }
        }

        private void Pulse(SteamVR_Input_Sources type)
        {
            _vibration.Execute(0f, 0.3f, 75, 1f, type);
        }
    }
}
