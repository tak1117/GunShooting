using Ammunition.CartridgeStructure;
using UnityEngine;

namespace Ammunition.CartridgesRealization
{
    public class Cartridge9X19Blank : Cartridge
    {
        public override void Init()
        {
            var bullet = GetBullet();
            bullet.GetComponent<MeshRenderer>().enabled = false;
        }
    }
}