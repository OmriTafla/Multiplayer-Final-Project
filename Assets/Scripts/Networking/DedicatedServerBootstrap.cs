using System;
using UnityEngine;

public class DedicatedServerBootstrap : MonoBehaviour
{
    [SerializeField] private string defaultSessionName = "DedicatedServer";

    private async void Start()
    {
        if (!Application.isBatchMode)
            return;

        var sessionName =
            GetCommandLineValue(
                "-session",
                defaultSessionName);

        var result =
            await SinglePeer_NetworkRunnerManager.Instance
                .StartDedicatedServer(sessionName);

        if (!result.Ok)
        {
            Debug.LogError(
                $"Dedicated server start failed: " +
                result.ShutdownReason);

            Application.Quit(1);
            return;
        }

        Debug.Log(
            $"Dedicated server started: {sessionName}");
    }

    private static string GetCommandLineValue(
        string key,
        string fallback)
    {
        var arguments =
            Environment.GetCommandLineArgs();

        for (var i = 0; i < arguments.Length - 1; i++)
        {
            if (arguments[i] == key)
                return arguments[i + 1];
        }

        return fallback;
    }
}