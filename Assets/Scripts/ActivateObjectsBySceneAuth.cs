using System;
using Fusion;
using UnityEngine;

public class ActivateObjectsBySceneAuth : MonoBehaviour
{
    [SerializeField] private GameObject[] sceneAuthExclusiveObjs;

    public void HandleNewRunner(NetworkRunner newRunner)
    {
        var isSceneAuth = newRunner && newRunner.IsServer;
        foreach (var obj in sceneAuthExclusiveObjs)
            obj.SetActive(isSceneAuth);
    }
    
    public void HandlePlayerLeft(NetworkRunner runner, PlayerRef _)
    {
        print("Player left");
        HandleNewRunner(runner);
    }

    private void OnEnable()
    {
        if (SinglePeer_NetworkRunnerManager.Instance)
            HandleNewRunner(SinglePeer_NetworkRunnerManager.Instance.NetworkRunner);
    }
}
