
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Abb2kTools.Projectiles
{
    public class Shooter : NetworkBehaviour
    {
        [Networked] private TickTimer ShootCooldownTimer { get; set; }
        [Networked] public Vector3 LastFireDirection { get; private set; }
        public int LastFireTick { get; private set; }
        [SerializeField] private float shootingCooldown = 0.5f;
        public HashSet<Upgrade<Projectile>> bulletUpgrades = new();
        
        [Header("Animation")]
        [SerializeField] private Animator cannonAnimator;
        private readonly int animShootId = Animator.StringToHash("Shoot");
        [SerializeField] private AudioSource shootSource;
    
        public override void Spawned()
        {
            base.Spawned();

            if (Object.HasStateAuthority)
            {
                ShootCooldownTimer = TickTimer.None;
            }
        }

        public void TryShoot()
        {
            if (!Object.HasStateAuthority)
                return;
        
            if (!ShootCooldownTimer.ExpiredOrNotRunning(Runner))
                return;

            ShootCooldownTimer = TickTimer.CreateFromSeconds(Runner, shootingCooldown);
            LastFireTick = Runner.Tick;
            LastFireDirection = transform.forward.normalized;
            
            if (Runner.IsForward)
            {
                cannonAnimator.SetTrigger(animShootId);
                shootSource.Stop();
                shootSource.Play();
            }

            if (Object.HasStateAuthority)
            {
                PlayShootEffectsRPC();

                
                var placementManager = FindAnyObjectByType<PlacementManager>();
                var newProjectile = placementManager?.SpawnProjectile(
                    Object, transform.position, LastFireDirection);

                foreach (var bulletUpgrade in bulletUpgrades)
                {
                    bulletUpgrade.ApplyUpgrade(newProjectile);
                }
            }
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
}