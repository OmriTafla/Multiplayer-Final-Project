using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NicknameSubmitUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputNicknameField;
    [SerializeField] private TMP_Dropdown dropdownColour;
    [SerializeField] private Button signInButton;
    [SerializeField] private bool allowEditorHostFallback = true;

    private bool signingIn;

    private void Awake()
    {
        if (signInButton == null)
            signInButton = FindSignInButton(transform);

        if (signInButton != null && signInButton.onClick.GetPersistentEventCount() == 0)
            signInButton.onClick.AddListener(SignIn);
    }

    private void OnEnable()
    {
        if (inputNicknameField != null)
            inputNicknameField.onSubmit.AddListener(HandleSubmit);
    }

    private void OnDisable()
    {
        if (inputNicknameField != null)
            inputNicknameField.onSubmit.RemoveListener(HandleSubmit);
    }

    public async void SignIn()
    {
        Debug.Log("Sign In button pressed");

        if (signingIn)
        {
            Debug.LogWarning("Sign in is already in progress");
            return;
        }

        if (inputNicknameField == null)
        {
            Debug.LogError("NicknameSubmitUI is missing the nickname input field reference");
            return;
        }

        var nickname = inputNicknameField.text.Trim();

        if (string.IsNullOrWhiteSpace(nickname))
        {
            Debug.LogWarning("Enter a nickname before signing in");
            inputNicknameField.ActivateInputField();
            return;
        }

        if (dropdownColour == null || dropdownColour.options.Count == 0)
        {
            Debug.LogError("NicknameSubmitUI is missing a valid colour dropdown");
            return;
        }

        var manager = SinglePeer_NetworkRunnerManager.Instance;

        if (manager == null)
        {
            Debug.LogError("No active SinglePeer_NetworkRunnerManager exists");
            return;
        }

        signingIn = true;

        if (signInButton != null)
            signInButton.interactable = false;

        PlayerPrefs.SetString("PendingNickname", nickname);
        PlayerPrefs.SetString("PendingColour", dropdownColour.options[dropdownColour.value].text);
        PlayerPrefs.Save();

        UIManager.Instance?.ShowWaitingScreen();

        try
        {
            Debug.Log($"Joining persistent world '{manager.PersistentSessionName}'...");

            var result = await manager.JoinPersistentWorld();

            if (!result.Ok && allowEditorHostFallback && Application.isEditor && !Application.isBatchMode)
            {
                Debug.LogWarning("Persistent server was unavailable. Starting an Editor development host.");
                result = await manager.StartPersistentHostForDevelopment();
            }

            if (result.Ok)
            {
                Debug.Log($"Connected to persistent world '{manager.PersistentSessionName}'. Waiting for GameScene synchronization.");
                return;
            }

            var message = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? result.ShutdownReason.ToString()
                : result.ErrorMessage;

            Debug.LogError($"Could not join persistent world: {message}");
            UIManager.Instance?.ShowLobbyMenu();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            UIManager.Instance?.ShowLobbyMenu();
        }
        finally
        {
            signingIn = false;

            if (signInButton != null)
                signInButton.interactable = true;
        }
    }

    private void HandleSubmit(string value)
    {
        SignIn();
    }

    private static Button FindSignInButton(Transform root)
    {
        var buttons = root.GetComponentsInChildren<Button>(true);

        foreach (var button in buttons)
        {
            if (button.name == "SignIn")
                return button;
        }

        return null;
    }
}
