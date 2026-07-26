using Fusion;
using UnityEngine;

public enum GameplayButton
{
    Fire,
    Place,
    Delete
}

public struct GameplayInput : INetworkInput
{
    public Vector2 Move;
    public Vector3 AimPosition;
    public NetworkButtons Buttons;
}