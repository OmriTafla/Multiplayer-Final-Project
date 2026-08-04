using Fusion;
using UnityEngine;

namespace Collectible
{
    public class Collectible : NetworkBehaviour, IHitable
    {
        [SerializeField, Min(1)] private int scoreForHit = 1;

        public void OnHit(DamageData data, PlayerRef? hitBy)
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }
            
            if (!hitBy.HasValue)
                return;

            var scoreManager = ScoreManager.Instance;

            if (!scoreManager ||
                !scoreManager.TryAddScore(hitBy.Value, scoreForHit))
            {
                return;
            }

            Runner.Despawn(Object);
        }
    }
}
