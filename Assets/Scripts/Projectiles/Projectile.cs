using Fusion;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 20f;
    [SerializeField] private DamageData damageData;

    [Networked]
    private TickTimer LifeTimer { get; set; }

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        LifeTimer =
            TickTimer.CreateFromSeconds(Runner, lifetime);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        transform.position +=
            transform.forward *
            speed *
            Runner.DeltaTime;

        if (LifeTimer.Expired(Runner))
            Runner.Despawn(Object);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority)
            return;

        var target =
            other.GetComponentInParent<NetworkObject>();

        if (target == null)
            return;

        if (target == Object)
            return;

        if (target.InputAuthority == Object.InputAuthority)
        {
            if (target.TryGetComponent(out Player _))
                return;
        }

        if (target.TryGetComponent(out Projectile _))
            return;

        if (target.TryGetComponent(out IHitable hittable))
            hittable.OnHit(damageData);

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScoreForHit_Client();

        Runner.Despawn(Object);
    }
}