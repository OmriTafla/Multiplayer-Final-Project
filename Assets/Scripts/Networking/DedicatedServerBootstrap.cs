#if UNITY_SERVER
#define DEDICATED_SERVER
#endif

using System;
using UnityEngine;

public class DedicatedServerBootstrap : MonoBehaviour
{
    [SerializeField] private string defaultSessionName = "MainWorld";

    private async void Start()
    {
#if DEDICATED_SERVER
        var manager = SinglePeer_NetworkRunnerManager.Instance;

        if (manager == null)
        {
            Debug.LogError("Dedicated server cannot start because the runner manager is missing", this);
            Application.Quit(1);
            return;
        }

        if (manager.IsRunning || manager.OperationInProgress)
            return;

        var sessionName = GetCommandLineValue("-session", defaultSessionName);
        var result = await manager.StartForCurrentBuild(sessionName);

        if (!result.Ok)
        {
            Debug.LogError($"Dedicated server start failed: {result.ShutdownReason}", this);
            Application.Quit(1);
            return;
        }

        Debug.Log($"Dedicated server started session '{sessionName}'");
#else
        enabled = false;
#endif
    }

    private static string GetCommandLineValue(string key, string fallback)
    {
        var arguments = Environment.GetCommandLineArgs();

        for (var i = 0; i < arguments.Length - 1; i++)
        {
            if (string.Equals(arguments[i], key, StringComparison.OrdinalIgnoreCase))
                return arguments[i + 1];
        }

        return fallback;
    }
}
