using Fusion;
using Singleton;
using UnityEngine;

[RequireComponent(typeof(NetworkEvents))]
public class NetworkEventsManager : Singleton<NetworkEventsManager>
{
    [SerializeField, HideInInspector] private NetworkEvents networkEvents;

    private void Start()
    {
        if (!networkEvents)
        {
            Debug.LogError("NetworkEventsManager requires a NetworkEvents reference", this);
            return;
        }

        if (SinglePeer_NetworkRunnerManager.Instance)
            SinglePeer_NetworkRunnerManager.Instance.ConfigureNetworkEvents(networkEvents);
    }
}
