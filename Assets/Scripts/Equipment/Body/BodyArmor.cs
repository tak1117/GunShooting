using Equipment.Interfaces;
using StructureComponents;
using UnityEngine;
using Equipment;

namespace Equipment.Body
{
    public class BodyArmor : MonoBehaviour, IBody
    {
        [SerializeField] private string _name;

        private Attachment _attachment;
        private Rigidbody _rb;
        private BodyAudio _bodyAudio;

        private void Start()
        {
            _attachment = GetComponent<Attachment>();
            _rb = GetComponent<Rigidbody>();
            _bodyAudio = GetComponent<BodyAudio>();

            _attachment.Init();
        }

        public void Equip(Transform slot)
        {
            EquipMotion.Attach(transform, slot, _rb);
            _bodyAudio.PlayEquipSound();
        }

        public void UnEquip()
        {
            EquipMotion.Detach(transform, _rb);
            _bodyAudio.PlayUnEquipSound();
        }

        public Attachment GetAttachment()
        {
            return _attachment;
        }

        public string GetEquipmentName()
        {
            return _name;
        }
    }
}