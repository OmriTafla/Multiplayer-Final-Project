using DG.Tweening;
using Fusion;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 20f;
    [SerializeField] private DamageData damageData;
    public uint piercingLeft = 0;

    [SerializeField]
    private Renderer myRenderer;

    [Networked]
    private TickTimer LifeTimer { get; set; }

    private bool impactResolved;

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        LifeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
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

        var shooter = Object.InputAuthority;
        var hitPosition = other.ClosestPoint(transform.position);

        if (Player.TryGet(target.InputAuthority, out var targetPlayer) &&
            targetPlayer.Object == target)
        {
            var targetPlayerRef = targetPlayer.Object.InputAuthority;
            var matchManager = MatchManager.Instance;
            var teamsManager = matchManager ? matchManager.TeamsManager : null;

            if (teamsManager && !teamsManager.CanDamage(shooter, targetPlayerRef))
                return;

            targetPlayer.OnHit(damageData, shooter);

            ResolveCollision(hitPosition);
            return;
        }

        if (target.TryGetComponent(out IHitable hittable))
        {
            hittable.OnHit(damageData, shooter);
            ResolveCollision(hitPosition);
        }
    }

    private void ResolveCollision(Vector3 hitPosition)
    {
        if (piercingLeft == 0)
        {
            impactResolved = true;
            PlayHitAnimAndkillRPC(hitPosition);
            return;
        }
        piercingLeft--;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void PlayHitAnimAndkillRPC(Vector3 hitPos)
    {
        speed = 0;

        var impactDirection = transform.position - hitPos;

        if (impactDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(impactDirection.normalized);

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
