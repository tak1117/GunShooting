using UnityEngine;

namespace Equipment
{
    internal static class EquipMotion
    {
        public static void Attach(Transform self, Transform slot, Rigidbody rb)
        {
            self.parent = slot;
            self.position = slot.position;
            self.rotation = slot.rotation;
            rb.isKinematic = true;
        }

        public static void Detach(Transform self, Rigidbody rb)
        {
            self.parent = null;
            rb.isKinematic = false;
        }
    }
}
