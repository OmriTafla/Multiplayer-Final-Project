namespace Abb2kTools.Projectiles
{
    public class PiercingUpgrade : Upgrade<Projectile>
    {
        public override void ApplyUpgrade(Projectile projectile)
        {
            projectile.piercingLeft++;
        }
    }
}