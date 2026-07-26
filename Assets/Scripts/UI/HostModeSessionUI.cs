using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HostModeSessionUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField sessionNameInput;
    [SerializeField] private Button createSessionButton;
    [SerializeField] private Button joinSessionButton;
    [SerializeField] private Button leaveSessionButton;
    [SerializeField] private TMP_Text statusLabel;

    private SinglePeer_NetworkRunnerManager manager;

    private void Awake()
    {
        manager = SinglePeer_NetworkRunnerManager.Instance;

        createSessionButton.onClick.AddListener(CreateSession);
        joinSessionButton.onClick.AddListener(JoinSession);
        leaveSessionButton.onClick.AddListener(LeaveSession);

        SetStatus("Not connected");
    }

    private void OnDestroy()
    {
        createSessionButton.onClick.RemoveListener(CreateSession);
        joinSessionButton.onClick.RemoveListener(JoinSession);
        leaveSessionButton.onClick.RemoveListener(LeaveSession);
    }

    private async void CreateSession()
    {
        if (!ValidateSessionName(out var sessionName))
            return;

        SetInteractable(false);
        SetStatus($"Creating {sessionName}...");

        var result = await manager.StartHost(sessionName);

        if (result.Ok)
            SetStatus($"Hosting {sessionName}");
        else
            SetStatus($"Host failed: {result.ShutdownReason}");

        SetInteractable(true);
    }

    private async void JoinSession()
    {
        if (!ValidateSessionName(out var sessionName))
            return;

        SetInteractable(false);
        SetStatus($"Joining {sessionName}...");

        var result = await manager.StartClient(sessionName);

        if (result.Ok)
            SetStatus($"Joined {sessionName}");
        else
            SetStatus($"Join failed: {result.ShutdownReason}");

        SetInteractable(true);
    }

    private async void LeaveSession()
    {
        SetInteractable(false);
        SetStatus("Leaving session...");

        await manager.ShutdownRunner();
        manager.CreateRunner();

        SetStatus("Not connected");
        SetInteractable(true);
    }

    private bool ValidateSessionName(out string sessionName)
    {
        sessionName = sessionNameInput.text.Trim();

        if (!string.IsNullOrWhiteSpace(sessionName))
            return true;

        SetStatus("Enter a session name");
        return false;
    }

    private void SetInteractable(bool interactable)
    {
        createSessionButton.interactable = interactable;
        joinSessionButton.interactable = interactable;
        leaveSessionButton.interactable = interactable;
        sessionNameInput.interactable = interactable;
    }

    private void SetStatus(string status)
    {
        if (statusLabel != null)
            statusLabel.text = status;

        Debug.Log(status);
    }
}