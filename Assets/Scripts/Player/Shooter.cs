using Fusion;
using UnityEngine;

public class Shooter : NetworkBehaviour
{
    [SerializeField] private float shootingCooldown = 0.5f;
    [SerializeField] private Animator cannonAnimator;
    [SerializeField] private AudioSource shootSource;
    [SerializeField] private UpgradeShop upgradeShop;

    [Networked] private TickTimer ShootCooldownTimer { get; set; }
    [Networked] public Vector3 LastFireDirection { get; private set; }

    public int LastFireTick { get; private set; }

    private readonly int animShootId = Animator.StringToHash("Shoot");

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
            ShootCooldownTimer = TickTimer.None;
    }

    public void TryShoot()
    {
        if (!Object.HasStateAuthority ||
            !ShootCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        ShootCooldownTimer = TickTimer.CreateFromSeconds(Runner, shootingCooldown);
        LastFireTick = Runner.Tick;
        LastFireDirection = transform.forward.normalized;

        if (Runner.IsForward)
        {
            cannonAnimator.SetTrigger(animShootId);
            shootSource.Stop();
            shootSource.Play();
        }

        PlayShootEffectsRPC();

        var matchManager = MatchManager.Instance;
        var placementManager = matchManager ? matchManager.PlacementManager : null;
        var projectile = placementManager?.SpawnProjectile(
            Object,
            transform.position,
            LastFireDirection);

        if (projectile && upgradeShop)
            upgradeShop.ApplyProjectileUpgrades(projectile);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void PlayShootEffectsRPC()
    {
        if (cannonAnimator)
            cannonAnimator.SetTrigger(animShootId);

        if (shootSource)
        {
            shootSource.Stop();
            shootSource.Play();
        }
    }
}
