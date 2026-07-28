using Fusion;
using Managers;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 20f;
    [SerializeField] private DamageData damageData;

    [Networked]
    private TickTimer LifeTimer { get; set; }
    [Networked] public PlayerRef OwnerPlayerRef { get; set; }

    private Collider[] projectileColliders;
    private TeamsManager teamsManager;
    private bool impactResolved;

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        LifeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
        projectileColliders = GetComponentsInChildren<Collider>(true);
        teamsManager = FindAnyObjectByType<TeamsManager>();
        IgnoreOwnerAndFriendlyCollisions();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        transform.position += transform.forward * speed * Runner.DeltaTime;

        if (LifeTimer.Expired(Runner))
            Runner.Despawn(Object);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleImpact(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null)
            HandleImpact(collision.collider);
    }

    private void HandleImpact(Collider other)
    {
        if (impactResolved || Object == null || !Object.HasStateAuthority || other == null)
            return;

        var target = other.GetComponentInParent<NetworkObject>();

        if (target == null || target == Object)
            return;

        if (target.TryGetComponent(out Projectile _))
            return;

        if (target.TryGetComponent(out Player targetPlayer))
        {
            var attacker = OwnerPlayerRef;
            var targetPlayerRef = targetPlayer.Object.InputAuthority;

            teamsManager ??= FindAnyObjectByType<TeamsManager>();

            if (teamsManager == null || !teamsManager.CanDamage(attacker, targetPlayerRef))
            {
                IgnoreCollisionsWith(targetPlayer);
                return;
            }

            impactResolved = true;

            if (targetPlayer.TryReceiveHit(attacker, damageData))
                ScoreManager.Instance?.AddScoreForHit(attacker);

            Runner.Despawn(Object);
            return;
        }

        if (target.TryGetComponent(out IHitable hittable))
            hittable.OnHit(damageData);

        impactResolved = true;
        Runner.Despawn(Object);
    }

    private void IgnoreOwnerAndFriendlyCollisions()
    {
        var attacker = OwnerPlayerRef;
        var players = FindObjectsByType<Player>(FindObjectsSortMode.None);

        foreach (var player in players)
        {
            if (player == null || player.Object == null)
                continue;

            var target = player.Object.InputAuthority;

            if (target == attacker ||
                teamsManager != null && !teamsManager.CanDamage(attacker, target))
            {
                IgnoreCollisionsWith(player);
            }
        }
    }

    private void IgnoreCollisionsWith(Player player)
    {
        if (player == null)
            return;

        projectileColliders ??= GetComponentsInChildren<Collider>(true);
        var playerColliders = player.GetCollisionColliders();

        foreach (var projectileCollider in projectileColliders)
        {
            if (projectileCollider == null)
                continue;

            foreach (var playerCollider in playerColliders)
            {
                if (playerCollider != null)
                    Physics.IgnoreCollision(projectileCollider, playerCollider, true);
            }
        }
    }
}
