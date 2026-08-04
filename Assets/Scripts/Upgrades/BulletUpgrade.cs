using Enums;
using Fusion;

namespace Abb2kTools.Projectiles
{
    public static class Upgrade<T> where T : NetworkBehaviour
    {
        public static void ApplyUpgrade(T behaviour);
    }
}