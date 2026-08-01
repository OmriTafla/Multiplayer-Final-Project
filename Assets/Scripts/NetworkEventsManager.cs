using Fusion;
using Singleton;
using UnityEngine;

[RequireComponent(typeof(NetworkEvents))]
public class NetworkEventsManager : Singleton<NetworkEventsManager>
{
    [SerializeField, HideInInspector] private NetworkEvents networkEvents;

    private void OnValidate()
    {
        networkEvents = GetComponent<NetworkEvents>();
    }

    private void Start()
    {
        if (!networkEvents)
            networkEvents = GetComponent<NetworkEvents>();

        if (SinglePeer_NetworkRunnerManager.Instance)
            SinglePeer_NetworkRunnerManager.Instance.ConfigureNetworkEvents(networkEvents);
    }
}
