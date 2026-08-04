using Enums;

public static class Upgrade
{
    public static void ApplyUpgrade(
        Projectile projectile,
        UpgradeType upgradeType,
        int times)
    {
        if (!projectile || times <= 0)
            return;

        if (upgradeType == UpgradeType.BulletPierce)
            projectile.piercingLeft += (uint)times;
    }
}
