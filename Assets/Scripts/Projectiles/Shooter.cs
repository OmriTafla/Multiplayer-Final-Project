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
            if (!ShootCooldownTimer.ExpiredOrNotRunning(Runner))
                return;

            ShootCooldownTimer = TickTimer.CreateFromSeconds(Runner, shootingCooldown);
            LastFireTick = Runner.Tick;
            LastFireDirection = transform.forward.normalized;

            if (Object.HasStateAuthority)
            {
                var placementManager = FindAnyObjectByType<PlacementManager>();
                placementManager?.SpawnProjectile(Object, transform.position, LastFireDirection);
            }
        }
    }
}