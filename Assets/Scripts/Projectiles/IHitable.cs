using Fusion;
using UnityEngine;

[System.Serializable]
public struct DamageData : INetworkStruct
{
    public const float MAX_POSSIBLE_DAMAGE = 8;
    public const float MIN_POSSIBLE_DAMAGE = 0;
    public float damage;
}

public interface IHitable
{
    void OnHit(DamageData data, PlayerRef? hitBy);
}
