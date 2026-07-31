using DG.Tweening;
using Fusion;
using Managers;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 20f;
    [SerializeField] private DamageData damageData;

    public int piercingLeft = 0;

    [SerializeField]
    private Renderer myRenderer;

    [Networked]
    private TickTimer LifeTimer { get; set; }

    [Networked] public PlayerRef OwnerPlayerRef { get; set; }

    private TeamsManager teamsManager;
    private bool impactResolved;

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        LifeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
        teamsManager = FindAnyObjectByType<TeamsManager>();
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

    private void HandleImpact(Collider other)
    {
        if (impactResolved || !Object || !Object.HasStateAuthority || !other)
            return;

        var target = other.GetComponentInParent<NetworkObject>();

        if (!target || target == Object)
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
                return;

            ResolveCollision(targetPlayer, shooter, target);
            return;
        }

        if (target.TryGetComponent(out IHitable hittable))
        {
            ResolveCollision(hittable, shooter, target);
        }
    }

    private void ResolveCollision(IHitable targetPlayer, PlayerRef shooter, NetworkObject target)
    {
        targetPlayer.OnHit(damageData, shooter);
        if (piercingLeft == 0)
        {
            impactResolved = true;
            PlayHitAnimAndkillRPC(target.transform.position);
            return;
        }
        piercingLeft--;
    }

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