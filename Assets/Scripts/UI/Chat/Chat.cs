using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.Serialization;

public class Chat : NetworkBehaviour
{
    [SerializeField] private Message messagePrefab;
    [FormerlySerializedAs("Content")]
    [SerializeField] private Transform content;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private string username = "Player";

    public void CallMessageRPC()
    {
        string message = inputField.text;
        if (string.IsNullOrWhiteSpace(message)) return;

        if (!Object || !Object.IsValid)
        {
            Debug.LogWarning("Chat NetworkObject not valid > message not sent over network!");
            return;
        }

        RPC_SendMessage(username, message);
        inputField.text = "";
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SendMessage(string username, string message, RpcInfo rpcInfo = default)
    {
        AddMessage(username, message);
    }

    private void AddMessage(string username, string message)
    {
        var messageComponent = Instantiate(messagePrefab, content);
        messageComponent.transform.localPosition = Vector3.zero;
        messageComponent.SetText($"{username}: {message}");
    }
}
