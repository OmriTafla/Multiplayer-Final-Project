using System;
using System.Collections;
using System.Collections.Generic;
using Enums;
using Fusion;
using Managers;
using Singleton;
using UnityEngine;

public class ScoreManager : NetworkedSingleton<ScoreManager>
{
    public static event Action ScoresChanged;
    public static event Action<LiveLeaderboardEntry> LeaderboardJsonReceived;

    public static LiveLeaderboardEntry LatestJsonLeaderboardEntry { get; private set; }

    [Networked, Capacity(GameManager.MAX_PLAYERS)]
    private NetworkDictionary<PlayerRef, int> Scores { get; } =
        MakeInitializer<PlayerRef, int>(new Dictionary<PlayerRef, int>());

    [Networked, OnChangedRender(nameof(OnScoreRevisionChanged))]
    private int ScoreRevision { get; set; }

    [SerializeField, Min(0f)] private float leaderboardPublishDelay = 0.25f;
    [SerializeField] private TeamsManager teamsManager;

    private const float ScoreRatioOnKill = 0.5f;

    private bool isSpawned;
    private bool leaderboardEventsSubscribed;
    private Coroutine leaderboardPublishRoutine;
    private string lastPublishedLeaderboard;

    public bool IsReady => isSpawned;

    public override void Spawned()
    {
        isSpawned = true;

        if (Object.HasStateAuthority)
        {
            lastPublishedLeaderboard = null;
            SubscribeLeaderboardEvents();

            foreach (var player in Runner.CommittedPlayers)
            {
                if (!Scores.ContainsKey(player))
                    Scores.Add(player, 0);
            }

            RequestLeaderboardPublish(true);
        }

        NotifyScoresChanged();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        StopLeaderboardPublishRoutine();
        UnsubscribeLeaderboardEvents();
        isSpawned = false;

        if (Instance == this)
            LatestJsonLeaderboardEntry = null;

        NotifyScoresChanged();
    }

    public void AddPlayer(PlayerRef player)
    {
        if (!isSpawned || !Object.HasStateAuthority || player == PlayerRef.None)
            return;

        if (Scores.ContainsKey(player))
            return;

        Scores.Add(player, 0);
        MarkScoresChanged(true);
    }

    public void AddScoreForKillingPlayer(PlayerRef killer, PlayerRef killed)
    {
        if (!isSpawned ||
            !Object.HasStateAuthority ||
            killer == PlayerRef.None ||
            killed == PlayerRef.None ||
            killer == killed ||
            !Scores.ContainsKey(killed))
        {
            return;
        }

        AddScore(killer, (int)(Scores.Get(killed) * ScoreRatioOnKill));
    }

    public void ResetPlayerScore(PlayerRef player)
    {
        if (!isSpawned ||
            !Object.HasStateAuthority ||
            player == PlayerRef.None ||
            !Scores.ContainsKey(player))
        {
            return;
        }

        if (Scores.Get(player) == 0)
            return;

        Scores.Set(player, 0);
        MarkScoresChanged(false);
    }

    public void AddScore(PlayerRef player, int score)
    {
        if (!isSpawned ||
            !Object.HasStateAuthority ||
            player == PlayerRef.None ||
            !Runner ||
            !Runner.IsPlayerCommitted(player) ||
            !Scores.ContainsKey(player))
        {
            return;
        }

        Scores.Set(player, Scores.Get(player) + score);
        MarkScoresChanged(false);
    }

    public void RemovePlayer(PlayerRef player)
    {
        if (!isSpawned || !Object.HasStateAuthority)
            return;

        if (Scores.Remove(player))
            MarkScoresChanged(true);
    }

    public bool TryGetScores(out Dictionary<PlayerRef, int> scores)
    {
        scores = new Dictionary<PlayerRef, int>();

        if (!isSpawned)
            return false;

        scores = new Dictionary<PlayerRef, int>(Scores);
        return true;
    }

    public Dictionary<PlayerRef, int> GetScores()
    {
        TryGetScores(out var scores);
        return scores;
    }

    private void MarkScoresChanged(bool publishImmediately)
    {
        if (!isSpawned)
            return;

        ScoreRevision++;
        NotifyScoresChanged();
        RequestLeaderboardPublish(publishImmediately);
    }

    private void OnScoreRevisionChanged()
    {
        if (isSpawned)
            NotifyScoresChanged();
    }

    private static void NotifyScoresChanged()
    {
        ScoresChanged?.Invoke();
    }

    private void SubscribeLeaderboardEvents()
    {
        if (leaderboardEventsSubscribed)
            return;

        UI.PlayerData.PlayerDataChanged += HandleLeaderboardDataChanged;
        TeamsManager.RulesChanged += HandleLeaderboardDataChanged;
        leaderboardEventsSubscribed = true;
    }

    private void UnsubscribeLeaderboardEvents()
    {
        if (!leaderboardEventsSubscribed)
            return;

        UI.PlayerData.PlayerDataChanged -= HandleLeaderboardDataChanged;
        TeamsManager.RulesChanged -= HandleLeaderboardDataChanged;
        leaderboardEventsSubscribed = false;
    }

    private void HandleLeaderboardDataChanged()
    {
        RequestLeaderboardPublish(false);
    }

    private void RequestLeaderboardPublish(bool immediately)
    {
        if (!isSpawned ||
            !Object.HasStateAuthority ||
            !Runner ||
            !Runner.IsServer)
        {
            return;
        }

        if (immediately)
        {
            StopLeaderboardPublishRoutine();
            PublishLeaderboard();
            return;
        }

        if (leaderboardPublishRoutine is null)
        {
            leaderboardPublishRoutine =
                StartCoroutine(PublishLeaderboardAfterDelay());
        }
    }

    private IEnumerator PublishLeaderboardAfterDelay()
    {
        if (leaderboardPublishDelay > 0f)
            yield return new WaitForSecondsRealtime(leaderboardPublishDelay);

        leaderboardPublishRoutine = null;
        PublishLeaderboard();
    }

    private void PublishLeaderboard()
    {
        if (!isSpawned ||
            !Object.HasStateAuthority ||
            !Runner ||
            !Runner.IsRunning ||
            !Runner.IsServer ||
            !Runner.SessionInfo)
        {
            return;
        }

        var readyTeamsManager = teamsManager && teamsManager.IsReady
            ? teamsManager
            : null;
        var gameMode = readyTeamsManager
            ? readyTeamsManager.ActiveGameMode
            : GameManager.Instance
                ? GameManager.Instance.GameMode
                : IOGameMode.FreeForAll;

        var leaderboard = LiveLeaderboardSnapshot.Capture(
            new Dictionary<PlayerRef, int>(Scores),
            readyTeamsManager,
            gameMode);

        if (leaderboard == lastPublishedLeaderboard)
            return;

        if (LiveLeaderboardSnapshot.TryParse(
                leaderboard,
                out var snapshot))
        {
            foreach (var player in snapshot.players)
                ReceiveLeaderboardJsonRPC(JsonUtility.ToJson(player));
        }

        Runner.SessionInfo.UpdateCustomProperties(
            new Dictionary<string, SessionProperty>
            {
                { SessionPropertyKeys.Leaderboard, leaderboard }
            });

        lastPublishedLeaderboard = leaderboard;
    }

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All,
        TickAligned = false)]
    private void ReceiveLeaderboardJsonRPC(string json)
    {
        if (!LiveLeaderboardSnapshot.TryParseEntry(
                json,
                out var entry))
            return;

        LatestJsonLeaderboardEntry = entry;
        LeaderboardJsonReceived?.Invoke(entry);
    }

    private void StopLeaderboardPublishRoutine()
    {
        if (leaderboardPublishRoutine is null)
            return;

        StopCoroutine(leaderboardPublishRoutine);
        leaderboardPublishRoutine = null;
    }

    private void OnDestroy()
    {
        StopLeaderboardPublishRoutine();
        UnsubscribeLeaderboardEvents();
        isSpawned = false;

        if (Instance == this)
        {
            LatestJsonLeaderboardEntry = null;
            Instance = null;
        }
    }
}
