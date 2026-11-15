using UnityEngine;

namespace Equipment.Head
{
    public class HelmetAudio : EquipAudioPlayer
    {
        [SerializeField] private AudioClip _visor;

        public void PlayHelmetVisorSound() => PlayClip(_visor, 1f);
    }
}
