using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SessionConnectionUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField sessionNameInput;
    [SerializeField] private Button createButton;
    [SerializeField] private TMP_Text statusLabel;

    private void OnEnable()
    {
        createButton.onClick.AddListener(CreateSession);
    }

    private void OnDisable()
    {
        createButton.onClick.RemoveListener(CreateSession);
    }

    private async void CreateSession()
    {
        var sessionName = sessionNameInput.text.Trim();

        if (string.IsNullOrWhiteSpace(sessionName))
        {
            SetStatus("Enter a session name.");
            return;
        }

        SetInteractable(false);
        SetStatus($"Creating {sessionName}...");

        var result =
            await SinglePeer_NetworkRunnerManager.Instance.StartHost(sessionName);

        if (!result.Ok)
        {
            SetStatus($"Creation failed: {result.ShutdownReason}");
            SetInteractable(true);
            return;
        }

        SetStatus($"Hosting {sessionName}");
    }

    public async void JoinSession(SessionInfo sessionInfo)
    {
        if (!CanJoin(sessionInfo))
            return;

        SetInteractable(false);
        SetStatus($"Joining {sessionInfo.Name}...");

        var result =
            await SinglePeer_NetworkRunnerManager.Instance.StartClient(
                sessionInfo.Name);

        if (!result.Ok)
        {
            SetStatus($"Join failed: {result.ShutdownReason}");
            SetInteractable(true);
            return;
        }

        SetStatus($"Joined {sessionInfo.Name}");
    }

    private static bool CanJoin(SessionInfo sessionInfo)
    {
        return sessionInfo.IsOpen &&
               sessionInfo.IsVisible &&
               sessionInfo.PlayerCount < sessionInfo.MaxPlayers;
    }

    private void SetInteractable(bool interactable)
    {
        createButton.interactable = interactable;
        sessionNameInput.interactable = interactable;
    }

    private void SetStatus(string message)
    {
        if (statusLabel != null)
            statusLabel.text = message;

        Debug.Log(message);
    }
}