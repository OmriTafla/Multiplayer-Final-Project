using Fusion;

namespace Abb2kTools.Projectiles
{
    public abstract class Upgrade<T> where T : NetworkBehaviour
    {
        public abstract void ApplyUpgrade(T behaviour);
    }
}