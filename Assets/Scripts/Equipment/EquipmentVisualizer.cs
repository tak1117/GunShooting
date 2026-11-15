using Equipment.Interfaces;
using UnityEngine;

namespace Equipment
{
    public class EquipmentVisualizer<T> : MonoBehaviour where T : IEquippable
    {
        private protected EquipmentSlot<T> Slot;
        private T _current;

        public virtual void Init()
        {
            _current = default;
            Slot = new EquipmentSlot<T>();
            Slot.Init(transform);
        }

        private void OnTriggerStay(Collider other)
        {
            if (_current != null) return;
            if (!other.TryGetComponent<T>(out var equipment)) return;
            if (!equipment.GetAttachment().TryGetHand(out _)) return;

            _current = equipment;
            _current.GetAttachment().OnDrop += EquipmentDrop;
        }

        private void OnTriggerExit(Collider other)
        {
            if (_current == null) return;
            if (!other.TryGetComponent<T>(out var equipment)) return;
            if (!_current.Equals(equipment)) return;

            _current.GetAttachment().OnDrop -= EquipmentDrop;
            _current = default;
        }

        private void EquipmentDrop()
        {
            if (!Slot.IsSlotAvailable()) return;

            var attachment = _current.GetAttachment();
            attachment.OnDrop -= EquipmentDrop;
            attachment.OnTake += EquipmentTake;

            _current.Equip(Slot.GetEquipSlotTransform());
            Slot.SetEquipmentInSlot(_current);
            OnEquipped(_current);

            _current = default;
        }

        private void EquipmentTake()
        {
            Slot.RemoveEquipmentInSlot(out var equipment);
            equipment.GetAttachment().OnTake -= EquipmentTake;

            OnUnequipped(equipment);
            equipment.UnEquip();
        }

        protected virtual void OnEquipped(T equipment)
        {
        }

        protected virtual void OnUnequipped(T equipment)
        {
        }
    }
}
