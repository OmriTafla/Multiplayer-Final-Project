using DG.Tweening;
using Fusion;
using Managers;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 20f;
    [SerializeField] private DamageData damageData;

    [SerializeField]
    private Renderer myRenderer;

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
        // IgnoreOwnerAndFriendlyCollisions();
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

    // private void OnCollisionEnter(Collision collision)
    // {
    //     if (collision.collider)
    //         HandleImpact(collision.collider);
    // }

    private void HandleImpact(Collider other)
    {
        if (impactResolved || Object == null || !Object.HasStateAuthority || other == null)
            return;

        var target = other.GetComponentInParent<NetworkObject>();

        if (target == null || target == Object)
            return;

        if (target.TryGetComponent(out Projectile _))
            return;

        var shooter = OwnerPlayerRef;

        if (target.TryGetComponent(out Player targetPlayer))
        {
            var targetPlayerRef = targetPlayer.Object.InputAuthority;

            //TODO: get the reference from the spawning player
            teamsManager ??= FindAnyObjectByType<TeamsManager>();

            if (teamsManager && !teamsManager.CanDamage(shooter, targetPlayerRef))
            {
                // IgnoreCollisionsWith(targetPlayer);
                return;
            }

            impactResolved = true;

            targetPlayer.OnHit(damageData, shooter);
            
            // if (targetPlayer.TryReceiveHit(shooter, damageData))
            //     ScoreManager.Instance?.AddScoreForHit(shooter);

            PlayHitAnimAndkillRPC(target.transform.position);
            return;
        }

        if (target.TryGetComponent(out IHitable hittable))
        {
            hittable.OnHit(damageData, shooter);
            impactResolved = true;
            PlayHitAnimAndkillRPC(target.transform.position);
        }
    }

    // private void IgnoreOwnerAndFriendlyCollisions()
    // {
    //     var attacker = OwnerPlayerRef;
    //     var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
    //
    //     foreach (var player in players)
    //     {
    //         if (player == null || player.Object == null)
    //             continue;
    //
    //         var target = player.Object.InputAuthority;
    //
    //         if (target == attacker ||
    //             teamsManager != null && !teamsManager.CanDamage(attacker, target))
    //         {
    //             IgnoreCollisionsWith(player);
    //         }
    //     }
    // }

    // private void IgnoreCollisionsWith(Player player)
    // {
    //     if (!player)
    //         return;
    //
    //     projectileColliders ??= GetComponentsInChildren<Collider>(true);
    //     var playerColliders = player.GetCollisionColliders();
    //
    //     foreach (var projectileCollider in projectileColliders)
    //     {
    //         if (projectileCollider == null)
    //             continue;
    //
    //         foreach (var playerCollider in playerColliders)
    //         {
    //             if (playerCollider != null)
    //                 Physics.IgnoreCollision(projectileCollider, playerCollider, true);
    //         }
    //     }
    // }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void PlayHitAnimAndkillRPC(Vector3 hitPos)
    {
        speed = 0;

        transform.rotation = Quaternion.LookRotation((transform.position - hitPos).normalized);

        myRenderer.material.DOColor(Color.white, .15f);

        var seq = DOTween.Sequence()
            .Append(transform.DOScaleY(transform.localScale.y / 1.4f, .1f).SetEase(Ease.OutExpo))
            .Join(transform.DOScaleX(transform.localScale.x * 1.4f, .2f).SetEase(Ease.OutExpo))
            .Append(transform.DOScale(0, .1f).SetEase(Ease.OutExpo))
            .SetLink(gameObject);
        if (Object.HasStateAuthority)
            seq.AppendCallback(() => Runner.Despawn(Object));
    }
}