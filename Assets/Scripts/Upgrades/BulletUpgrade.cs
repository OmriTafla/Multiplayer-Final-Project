using Enums;
using Fusion;

namespace Abb2kTools.Projectiles
{
    public abstract class Upgrade<T> where T : NetworkBehaviour
    {
        public abstract UpgradeType UpgradeType { get; }
        public abstract void ApplyUpgrade(T behaviour);
    }
}