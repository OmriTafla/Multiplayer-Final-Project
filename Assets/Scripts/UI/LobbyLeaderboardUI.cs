#if UNITY_SERVER
#define DEDICATED_SERVER
#endif

using System.Collections.Generic;
using System.Linq;
using EasyTextEffects;
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

        TextEffect targetEffect = target.GetComponent<TextEffect>();;

        var title = mode.GetDisplayName();

        if (session is null)
        {
            target.text =
                $"<b><link=swing+gradient>{title}</b></link>\n" +
                "<color=#FFD166>No active session</color>\n" +
                "<link=wiggle>Start Game to host</link>";

            if (targetEffect)
                targetEffect.Refresh();

            return;
        }

        var onlineLine = session.IsOpen
            ? "<color=#77FF99>Server online</color>"
            : "<color=#FFD166>Match locked</color>";
        var playerCount = Mathf.Max(0, session.PlayerCount);
        var maximumPlayers = Mathf.Max(1, session.MaxPlayers);

        if (playerCount >= maximumPlayers)
            onlineLine = "<color=#FFD166>Session full</color>";

        var capacityLine = $"{playerCount}/{maximumPlayers} players";

        if (!session.Properties.TryGetValue(
                SessionPropertyKeys.Leaderboard,
                out var property) ||
            !property.IsString ||
            !LiveLeaderboardSnapshot.TryParse(
                (string)property,
                out var snapshot) ||
            snapshot.gameMode != (int)mode)
        {
            target.text =
                $"<b>{title}</b>\n{onlineLine} · {capacityLine}\n\n" +
                (playerCount == 0
                    ? "No active players"
                    : "Leaderboard syncing...");
            return;
        }

        var body = snapshot.BuildBody(mode);

        target.text = string.IsNullOrWhiteSpace(body)
            ? $"<b><link=swing+gradient>{title}</b></link>\n<link=bounce>{onlineLine}</link> · {capacityLine}\n\n<link=swing>No active players</link>"
            : $"<b><link=swing+gradient>{title}</b></link>\n<link=bounce>{onlineLine}</link> · {capacityLine}\n\n<link=swing>{body}</link>";

        if (targetEffect)
            targetEffect.Refresh();
    }

    private void SetConnecting()
    {
        if (freeForAllText)
            freeForAllText.text = "<b><link=swing+gradient>Free For All</b></link>\n<link=size>Connecting...</link>";

        if (twoTeamsText)
            twoTeamsText.text = "<b><link=swing+gradient>Two Teams</b></link>\n<link=size>Connecting...</link>";
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
                "<b><link=swing+gradient>Free For All</b>\n" +
                "<color=#FF7777>Leaderboard connection unavailable</color></link>";
        }

        if (twoTeamsText)
        {
            twoTeamsText.text =
                "<b><link=swing+gradient>Two Teams</b>\n" +
                "<color=#FF7777>Leaderboard connection unavailable</color></link>";
        }
    }

    private void SetStartButtonInteractable(bool interactable)
    {
        if (startGameButton)
            startGameButton.interactable = interactable;
    }
}
