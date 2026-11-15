using UnityEngine;

namespace Equipment
{
    [RequireComponent(typeof(AudioSource))]
    public abstract class EquipAudioPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip _equip;

        private AudioSource _source;

        private void Start()
        {
            _source = GetComponent<AudioSource>();
        }

        public void PlayEquipSound() => PlayClip(_equip, 1f);

        public void PlayUnEquipSound() => PlayClip(_equip, 0.75f);

        protected void PlayClip(AudioClip clip, float volume)
        {
            _source.volume = volume;
            _source.clip = clip;
            _source.pitch = Random.Range(0.9f, 1.1f);
            _source.Play();
        }
    }
}
