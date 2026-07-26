using System;
using UnityEngine;

public class DedicatedServerBootstrap : MonoBehaviour
{
    [SerializeField] private string defaultSessionName = "MainWorld";

    private async void Start()
    {
        if (!Application.isBatchMode)
            return;

        var manager = SinglePeer_NetworkRunnerManager.Instance;

        if (manager == null)
        {
            Debug.LogError("Dedicated server cannot start because the runner manager is missing");
            Application.Quit(1);
            return;
        }

        if (manager.IsRunning || manager.OperationInProgress)
            return;

        var sessionName = GetCommandLineValue("-session", defaultSessionName);
        var result = await manager.StartPersistentServer(sessionName);

        if (!result.Ok)
        {
            Debug.LogError($"Persistent world server start failed: {result.ShutdownReason}");
            Application.Quit(1);
            return;
        }

        Debug.Log($"Persistent world server started: {sessionName}");
    }

    private static string GetCommandLineValue(string key, string fallback)
    {
        var arguments = Environment.GetCommandLineArgs();

        for (var i = 0; i < arguments.Length - 1; i++)
        {
            if (arguments[i] == key)
                return arguments[i + 1];
        }

        return fallback;
    }
}
