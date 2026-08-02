using Enums;

namespace Abb2kTools.Projectiles
{
    public class PiercingUpgrade : Upgrade<Projectile>
    {
        public override UpgradeType UpgradeType => UpgradeType.BulletPierce;

        public override void ApplyUpgrade(Projectile projectile)
        {
            projectile.piercingLeft++;
        }
    }
}