using DG.Tweening;
using Fusion;
using UnityEngine;

namespace Collectible
{
    public class Collectible : NetworkBehaviour, IHitable
    {
        public int scoreForHit;

        public void OnHit(DamageData data, PlayerRef? hitBy)
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }
            
            if (hitBy.HasValue)
            {
                ScoreManager.Instance.AddScore(hitBy.Value, scoreForHit);
            }

            SelfDestruct();
        }

        private void SelfDestruct()
        {
            Runner.Despawn(Object);
        }
    }
}