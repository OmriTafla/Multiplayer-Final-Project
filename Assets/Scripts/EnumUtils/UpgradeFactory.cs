using System;
using Abb2kTools.Projectiles;
using Enums;

namespace EnumUtils
{
    public static class UpgradeFactory
    {
        public static Upgrade<Projectile> MakeProjectileUpgrade(UpgradeType type) 
        {
            switch (type)
            {
                case UpgradeType.BulletPierce:
                    return new PiercingUpgrade();
                case UpgradeType.Unknown:
                    default:
                        throw new ArgumentOutOfRangeException($"{type} not supported by factory");
            }
        }
}
}