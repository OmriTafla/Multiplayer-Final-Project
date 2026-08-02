
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
        public HashSet<BulletUpgrade> bulletUpgrades = new();
    
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

            if (Object.HasStateAuthority)
            {
                var placementManager = FindAnyObjectByType<PlacementManager>();
                var newProjectile = placementManager?.SpawnProjectile(
                    Object, transform.position, LastFireDirection);

                foreach (var bulletUpgrade in bulletUpgrades)
                {
                    bulletUpgrade.ApplyUpgrade(newProjectile);
                }
            }
        }
    }
}