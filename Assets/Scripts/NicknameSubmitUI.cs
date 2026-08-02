#if UNITY_SERVER
#define DEDICATED_SERVER
#else
#define CLIENT_BUILD
#endif

using System.Threading.Tasks;
using Enums;
using EnumUtils;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class NicknameSubmitUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputNicknameField;
    [FormerlySerializedAs("dropdownColour")]
    [SerializeField] private TMP_Dropdown gameModeDropdown;
    [SerializeField] private string mapName = "GameScene";
    [FormerlySerializedAs("signInButton")]
    [SerializeField] private Button startGameButton;

    private bool connecting;

    private void Awake()
    {
#if DEDICATED_SERVER
        gameObject.SetActive(false);
#else
        if (startGameButton &&
            startGameButton.onClick.GetPersistentEventCount() == 0)
        {
            startGameButton.onClick.AddListener(StartGame);
        }
#endif
    }

    private void OnEnable()
    {
#if CLIENT_BUILD
        if (inputNicknameField)
            inputNicknameField.onSubmit.AddListener(HandleSubmit);
#endif
    }

    private void OnDisable()
    {
#if CLIENT_BUILD
        if (inputNicknameField)
            inputNicknameField.onSubmit.RemoveListener(HandleSubmit);
#endif
    }

    public async void StartGame()
    {
#if DEDICATED_SERVER
        return;
#else
        if (connecting)
            return;

        if (!inputNicknameField)
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

        if (!TryGetSelectedGameMode(out var gameMode))
        {
            Debug.LogError("NicknameSubmitUI is missing a valid game mode dropdown", this);
            return;
        }

        var manager = SinglePeer_NetworkRunnerManager.Instance;

        if (!manager)
        {
            Debug.LogError("The network runner manager is missing", this);
            return;
        }

        if (manager.OperationInProgress)
            return;

        var sessionName = manager.GetSessionNameForMode(gameMode, mapName);
        var modeName = gameMode.GetDisplayName();

        var gameManager = GameManager.Instance;

        if (gameManager)
            gameManager.SetGameMode(gameMode);

        PlayerPrefs.SetString("PendingNickname", nickname);
        PlayerPrefs.SetInt("PendingGameMode", (int)gameMode);
        PlayerPrefs.DeleteKey("PendingColour");
        PlayerPrefs.Save();

        connecting = true;
        SetControlsInteractable(false);

        var uiManager = UIManager.Instance;

        if (uiManager)
            uiManager.ShowStatus($"Joining or hosting {modeName}...");

        string failureReason;

        try
        {
            StartGameResult result = await manager.StartHostOrClient(sessionName);

            if (result.Ok)
                return;

            failureReason = GetFailureReason(result);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            failureReason = exception.Message;
        }

        connecting = false;
        SetControlsInteractable(true);

        Debug.LogError(
            $"Connection to '{sessionName}' failed: {failureReason}",
            this);

        if (uiManager)
        {
            uiManager.ShowStatus(
                $"Could not join or host {modeName}.\n{failureReason}");
        }

        await Task.Delay(2500);

        if (this && uiManager)
            uiManager.ShowStartMenu();
#endif
    }

    private bool TryGetSelectedGameMode(out IOGameMode gameMode)
    {
        gameMode = default;

        if (!gameModeDropdown)
            return false;

        if (!System.Enum.IsDefined(typeof(IOGameMode), gameModeDropdown.value))
            return false;

        gameMode = (IOGameMode)gameModeDropdown.value;
        return true;
    }

    private void HandleSubmit(string value)
    {
        StartGame();
    }

    private void SetControlsInteractable(bool interactable)
    {
        if (startGameButton)
            startGameButton.interactable = interactable;

        if (gameModeDropdown)
            gameModeDropdown.interactable = interactable;

        if (inputNicknameField)
            inputNicknameField.interactable = interactable;
    }

    private static string GetFailureReason(StartGameResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            return result.ErrorMessage;

        var shutdownReason = result.ShutdownReason.ToString();

        return string.IsNullOrWhiteSpace(shutdownReason)
            ? "Unknown connection error"
            : shutdownReason;
    }

}
