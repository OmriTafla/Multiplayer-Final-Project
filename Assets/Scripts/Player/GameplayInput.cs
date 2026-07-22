using Fusion;
using UnityEngine;

namespace DefaultNamespace
{
    public struct GameplayInput : INetworkInput
    {
        public Vector3 MoveDirection;
        public Vector2 LookRotation;
    }

}