#if UNITY_SERVER
#define DEDICATED_SERVER
#else
#define HOST_OR_CLIENT
#endif

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NicknameSubmitUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputNicknameField;
    [SerializeField] private TMP_Dropdown dropdownColour;
    [SerializeField] private Button signInButton;

    private bool connecting;

    private void Awake()
    {
#if DEDICATED_SERVER
        gameObject.SetActive(false);
#else
        if (signInButton == null)
            signInButton = FindSignInButton(transform);

        if (signInButton != null && signInButton.onClick.GetPersistentEventCount() == 0)
            signInButton.onClick.AddListener(Connect);
#endif
    }

    private void OnEnable()
    {
#if HOST_OR_CLIENT
        if (inputNicknameField != null)
            inputNicknameField.onSubmit.AddListener(HandleSubmit);
#endif
    }

    private void OnDisable()
    {
#if HOST_OR_CLIENT
        if (inputNicknameField != null)
            inputNicknameField.onSubmit.RemoveListener(HandleSubmit);
#endif
    }

    public void SignIn()
    {
        Connect();
    }

    public async void Connect()
    {
#if DEDICATED_SERVER
        return;
#else
        if (connecting)
            return;

        if (inputNicknameField == null)
        {
            Debug.LogError("NicknameSubmitUI is missing the nickname input field reference", this);
            return;
        }

        var nickname = inputNicknameField.text.Trim();

        if (string.IsNullOrWhiteSpace(nickname))
        {
            inputNicknameField.ActivateInputField();
            return;
        }

        if (dropdownColour == null || dropdownColour.options.Count == 0)
        {
            Debug.LogError("NicknameSubmitUI is missing a valid colour dropdown", this);
            return;
        }

        var manager = SinglePeer_NetworkRunnerManager.Instance;

        if (manager == null)
        {
            Debug.LogError("No active SinglePeer_NetworkRunnerManager exists", this);
            return;
        }

        connecting = true;

        if (signInButton != null)
            signInButton.interactable = false;

        PlayerPrefs.SetString("PendingNickname", nickname);
        PlayerPrefs.SetString("PendingColour", dropdownColour.options[dropdownColour.value].text);
        PlayerPrefs.Save();

        UIManager.Instance?.ShowWaitingScreen();

        try
        {
            var result = await manager.StartForCurrentBuild();

            if (result.Ok)
            {
                Debug.Log($"Connected to '{manager.PersistentSessionName}' as {manager.NetworkRunner.GameMode}");
                return;
            }

            var message = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? result.ShutdownReason.ToString()
                : result.ErrorMessage;

            Debug.LogError($"Connection failed: {message}");
            UIManager.Instance?.ShowLobbyMenu();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            UIManager.Instance?.ShowLobbyMenu();
        }
        finally
        {
            connecting = false;

            if (signInButton != null)
                signInButton.interactable = true;
        }
#endif
    }

    private void HandleSubmit(string value)
    {
        Connect();
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
