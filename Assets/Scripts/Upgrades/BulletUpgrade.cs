using System;
using Enums;
using Fusion;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

namespace Abb2kTools.Projectiles
{
    public static class Upgrade
    {
        public static void ApplyUpgrade<T>(T behaviour, UpgradeType upgradeType, uint times = 1) where T : NetworkBehaviour
        {
            if (behaviour is Projectile projectile)
            {
                switch (upgradeType)
                {
                    case UpgradeType.BulletPierce:
                        Debug.Log("Adding pierce");
                    projectile.piercingLeft += times;
                    return;
                    
                    default:
                        throw new ArgumentOutOfRangeException($"Upgrade type {upgradeType} not supported.");
                }
            }
        }
    }
}