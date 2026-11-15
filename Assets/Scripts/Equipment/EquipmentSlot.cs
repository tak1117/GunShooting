using Equipment.Interfaces;
using UnityEngine;

namespace Equipment
{
    public class EquipmentSlot<T> where T : IEquippable
    {
        private protected Transform Transform;
        private protected T Equipment;

        public void Init(Transform transform)
        {
            Equipment = default;
            Transform = transform;
        }

        public Transform GetEquipSlotTransform()
        {
            return Transform;
        }

        public void SetEquipmentInSlot(T equipment)
        {
            Equipment = equipment;
        }

        public bool IsSlotAvailable()
        {
            return Equipment == null;
        }

        public void RemoveEquipmentInSlot(out T removed)
        {
            removed = Equipment;
            Equipment = default;
        }
    }
}
