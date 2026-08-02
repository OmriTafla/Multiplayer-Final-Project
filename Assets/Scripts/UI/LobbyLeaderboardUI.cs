#if UNITY_SERVER
#define DEDICATED_SERVER
#endif

using System.Collections.Generic;
using System.Linq;
using Enums;
using EnumUtils;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyLeaderboardUI : MonoBehaviour
{
    [SerializeField] private TMP_Text freeForAllText;
    [SerializeField] private TMP_Text twoTeamsText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private string mapName = "GameScene";

    private NetworkEvents networkEvents;
    private bool receivedSessionList;

    private async void Start()
    {
#if DEDICATED_SERVER
        gameObject.SetActive(false);
#else
        SetConnecting();
        SetStartButtonInteractable(false);

        var manager = SinglePeer_NetworkRunnerManager.Instance;

        if (!manager)
        {
            SetAllUnavailable();
            SetStartButtonInteractable(true);
            return;
        }

        networkEvents = manager.NetworkEvents;

        if (!networkEvents)
        {
            SetConnectionUnavailable();
            SetStartButtonInteractable(true);
            return;
        }

        networkEvents.OnSessionListUpdate.AddListener(
            HandleSessionListUpdated);

        var joined = await manager.JoinLeaderboardLobby();

        if (!this)
            return;

        if (!joined)
            SetConnectionUnavailable();
        else if (!receivedSessionList)
            SetAllUnavailable();

        SetStartButtonInteractable(true);
#endif
    }

    private void OnDestroy()
    {
        if (networkEvents)
        {
            networkEvents.OnSessionListUpdate.RemoveListener(
                HandleSessionListUpdated);
        }
    }

    public void HandleSessionListUpdated(
        NetworkRunner runner,
        List<SessionInfo> sessionList)
    {
        receivedSessionList = true;

        var manager = SinglePeer_NetworkRunnerManager.Instance;

        if (!manager)
        {
            SetAllUnavailable();
            return;
        }

        SetModePanel(
            freeForAllText,
            IOGameMode.FreeForAll,
            FindModeSession(
                manager,
                sessionList,
                IOGameMode.FreeForAll));

        SetModePanel(
            twoTeamsText,
            IOGameMode.TwoTeams,
            FindModeSession(
                manager,
                sessionList,
                IOGameMode.TwoTeams));
    }

    private SessionInfo FindModeSession(
        SinglePeer_NetworkRunnerManager manager,
        IEnumerable<SessionInfo> sessions,
        IOGameMode mode)
    {
        var expectedSessionName =
            manager.GetSessionNameForMode(mode, mapName);

        return sessions.FirstOrDefault(session =>
            session.IsVisible &&
            string.Equals(
                session.Name,
                expectedSessionName,
                System.StringComparison.Ordinal));
    }

    private static void SetModePanel(
        TMP_Text target,
        IOGameMode mode,
        SessionInfo session)
    {
        if (!target)
            return;

        var title = mode.GetDisplayName();

        if (session is null)
        {
            target.text =
                $"<b>{title}</b>\n" +
                "<color=#FFD166>No active session</color>\n" +
                "Start Game to host";
            return;
        }

        var onlineLine = session.IsOpen
            ? "<color=#77FF99>Server online</color>"
            : "<color=#FFD166>Match locked</color>";

        if (!session.Properties.TryGetValue(
                SessionPropertyKeys.Leaderboard,
                out var property) ||
            !property.IsString ||
            !LiveLeaderboardSnapshot.TryParse(
                (string)property,
                out var snapshot))
        {
            target.text =
                $"<b>{title}</b>\n{onlineLine}\n\nNo active players";
            return;
        }

        var playerCount = snapshot.players.Length;
        var playerLabel = playerCount == 1 ? "player" : "players";
        var body = snapshot.BuildBody(mode);

        target.text = string.IsNullOrWhiteSpace(body)
            ? $"<b>{title}</b>\n{onlineLine}\n\nNo active players"
            : $"<b>{title}</b>\n{onlineLine} · {playerCount} {playerLabel}\n\n{body}";
    }

    private void SetConnecting()
    {
        if (freeForAllText)
            freeForAllText.text = "<b>Free For All</b>\nConnecting...";

        if (twoTeamsText)
            twoTeamsText.text = "<b>Two Teams</b>\nConnecting...";
    }

    private void SetAllUnavailable()
    {
        SetModePanel(
            freeForAllText,
            IOGameMode.FreeForAll,
            null);

        SetModePanel(
            twoTeamsText,
            IOGameMode.TwoTeams,
            null);
    }

    private void SetConnectionUnavailable()
    {
        if (freeForAllText)
        {
            freeForAllText.text =
                "<b>Free For All</b>\n" +
                "<color=#FF7777>Leaderboard connection unavailable</color>";
        }

        if (twoTeamsText)
        {
            twoTeamsText.text =
                "<b>Two Teams</b>\n" +
                "<color=#FF7777>Leaderboard connection unavailable</color>";
        }
    }

    private void SetStartButtonInteractable(bool interactable)
    {
        if (startGameButton)
            startGameButton.interactable = interactable;
    }
}
