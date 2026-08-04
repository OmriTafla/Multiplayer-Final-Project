using Enums;

public static class Upgrade
{
    private const float FirstDamageUpgrade = 5f;
    private const float SecondDamageUpgrade = 8f;

    public static void ApplyUpgrade(
        Projectile projectile,
        UpgradeType upgradeType,
        int times)
    {
        if (!projectile || times <= 0)
            return;

        if (upgradeType == UpgradeType.BulletPierce)
            projectile.AddPiercing((uint)times);

        if (upgradeType == UpgradeType.BulletDamage)
            projectile.SetDamage(times == 1
                ? FirstDamageUpgrade
                : SecondDamageUpgrade);
    }
}
