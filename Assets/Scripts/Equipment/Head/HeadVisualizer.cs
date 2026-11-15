using Audio;
using Equipment.Interfaces;

namespace Equipment.Head
{
    public class HeadVisualizer : EquipmentVisualizer<IHead>
    {
        protected override void OnEquipped(IHead equipment)
        {
            AudioManager.Instance.MixerHelmetOn(equipment.GetHelmetSuppression());
        }

        protected override void OnUnequipped(IHead equipment)
        {
            AudioManager.Instance.MixerDefault();
        }
    }
}
