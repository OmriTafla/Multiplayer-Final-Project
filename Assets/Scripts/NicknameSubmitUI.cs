using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NicknameSubmitUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputNicknameField;
    [SerializeField] private TMP_Dropdown dropdownColour;
    [SerializeField] private Button signInButton;
    [SerializeField] private string lobbyName = "Cool";

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

        signingIn = true;

        if (signInButton != null)
            signInButton.interactable = false;

        PlayerPrefs.SetString("PendingNickname", nickname);
        PlayerPrefs.SetString("PendingColour", dropdownColour.options[dropdownColour.value].text);
        PlayerPrefs.Save();

        Debug.Log($"Saved nickname '{nickname}'. Joining lobby '{lobbyName}'...");

        if (UIManager.Instance != null)
            UIManager.Instance.ShowWaitingScreen();

        try
        {
            var lobbyJoiner = LobbyJoiner.Instance;

            if (lobbyJoiner == null)
                lobbyJoiner = FindAnyObjectByType<LobbyJoiner>();

            if (lobbyJoiner == null)
            {
                Debug.LogError("No active LobbyJoiner exists in LobbyScene");
                ShowSignInMenu();
                return;
            }

            var joined = await lobbyJoiner.JoinLobbyAsync(lobbyName);

            if (joined)
            {
                Debug.Log($"Joined lobby '{lobbyName}'. Opening session browser");

                if (UIManager.Instance != null)
                    UIManager.Instance.ShowSessionsMenu();

                return;
            }

            Debug.LogError($"Could not join lobby '{lobbyName}'");
            ShowSignInMenu();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ShowSignInMenu();
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

    private void ShowSignInMenu()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ShowLobbyMenu();
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
