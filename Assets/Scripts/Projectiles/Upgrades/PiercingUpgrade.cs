namespace Abb2kTools.Projectiles
{
    public class PiercingUpgrade : BulletUpgrade
    {
        public override void ApplyUpgrade(Projectile projectile)
        {
            projectile.piercingLeft++;
        }
    }
}