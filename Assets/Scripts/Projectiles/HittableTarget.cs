using Fusion;
using UnityEngine;

public class HittableTarget : NetworkBehaviour
{
    [SerializeField] private Renderer modelRenderer;
    [SerializeField] private ParticleSystem hitEffectPrefab;

    [Networked, OnChangedRender(nameof(OnHitStateChanged))]
    private bool IsHit { get; set; }

    private Color originalColor;
    private readonly Color hitColor = Color.red;

    public override void Spawned()
    {
        if (modelRenderer != null)
            originalColor = modelRenderer.material.color;

        OnHitStateChanged();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority || IsHit)
            return;

        var projectile = other.GetComponentInParent<Projectile>();
        var hitObject = other.GetComponentInParent<NetworkObject>();

        if (projectile == null || hitObject == null)
            return;

        Debug.Log($"{gameObject.name} was hit by {hitObject.name}");
        IsHit = true;
        RpcPlayHitEffect();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcPlayHitEffect()
    {
        if (hitEffectPrefab == null)
            return;

        var effect = Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        effect.Play();
        Destroy(effect.gameObject, effect.main.duration);
    }

    private void OnHitStateChanged()
    {
        if (modelRenderer != null)
            modelRenderer.material.color = IsHit ? hitColor : originalColor;
    }
}
