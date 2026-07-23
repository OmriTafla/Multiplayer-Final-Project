using System;
using Fusion;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [Networked]
    private TickTimer lifeTimer { get; set; }
    
    [SerializeField] private float speed = 20f;

    [SerializeField]
    private DamageData damageData;

    public override void Spawned()
    {
        if (!Object.HasStateAuthority) return;
        lifeTimer = TickTimer.CreateFromSeconds(Runner, 20f);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        transform.position += transform.forward * speed * Time.fixedDeltaTime;
        
        if (lifeTimer.Expired(Runner))
        {   
            Runner.Despawn(Object);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority) return;
        var hitObject = other.GetComponentInParent<NetworkObject>();
        if (!hitObject) return;

        if (hitObject.TryGetComponent(out Player player) && hitObject.InputAuthority == Object.InputAuthority)
            return;
        if (hitObject.TryGetComponent(out Projectile projectile)) return;

        TriggerHitOnTargetRPC(hitObject.StateAuthority, hitObject.Id, damageData);
        ScoreManager.Instance.AddScoreForHit_Client();

        Debug.Log("Projectile hit " + hitObject.name);
        Runner.Despawn(Object);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void TriggerHitOnTargetRPC([RpcTarget] PlayerRef player, NetworkId theOneUnskilledIndividualThatWasHit, DamageData sentDamageData)
    {
        if (!SinglePeer_NetworkRunnerManager.Instance.NetworkRunner.TryFindObject(theOneUnskilledIndividualThatWasHit, out NetworkObject sillyHittable)) return;

        if (sentDamageData.damage > DamageData.MAX_POSSIBLE_DAMAGE || sentDamageData.damage < DamageData.MIN_POSSIBLE_DAMAGE)
        {
            Debug.LogError($"This {sentDamageData} was sent by an EVIL CHEATOR!!!! HAXXOR!!!! >:(");
            return;
        }

        if (sillyHittable.TryGetComponent(out IHitable hitable))
            hitable.OnHit(sentDamageData);
    }
}
