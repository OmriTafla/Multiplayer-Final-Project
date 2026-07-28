using System;
using System.Collections.Generic;
using Fusion;
using Singleton;
using UnityEngine;

public class ScoreManager : NetworkedSingleton<ScoreManager>
{
    public static event Action ScoresChanged;

    [Networked, Capacity(GameManager.MAX_PLAYERS)]
    private NetworkDictionary<PlayerRef, int> Scores { get; } =
        MakeInitializer<PlayerRef, int>(new Dictionary<PlayerRef, int>());

    [Networked, OnChangedRender(nameof(OnScoreRevisionChanged))]
    private int ScoreRevision { get; set; }

    [SerializeField] private int scoreForHit = 1;

    private bool isSpawned;

    public bool IsReady => isSpawned;

    public override void Spawned()
    {
        isSpawned = true;

        if (Object.HasStateAuthority)
        {
            foreach (var player in Runner.ActivePlayers)
            {
                if (!Scores.ContainsKey(player))
                    Scores.Add(player, 0);
            }
        }

        NotifyScoresChanged();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        isSpawned = false;
        NotifyScoresChanged();
    }

    public void AddPlayer(PlayerRef player)
    {
        if (!isSpawned || !Object.HasStateAuthority || player == PlayerRef.None)
            return;

        if (Scores.ContainsKey(player))
            return;

        Scores.Add(player, 0);
        MarkScoresChanged();
    }

    public void AddScoreForHit(PlayerRef player) => AddScore(player, scoreForHit);

    public void AddScore(PlayerRef player, int score)
    {
        if (!isSpawned || !Object.HasStateAuthority || player == PlayerRef.None)
            return;

        if (!Scores.ContainsKey(player))
            Scores.Add(player, 0);

        Scores.Set(player, Scores.Get(player) + score);
        MarkScoresChanged();
    }

    public void RemovePlayer(PlayerRef player)
    {
        if (!isSpawned || !Object.HasStateAuthority)
            return;

        if (Scores.Remove(player))
            MarkScoresChanged();
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

    private void MarkScoresChanged()
    {
        if (!isSpawned)
            return;

        ScoreRevision++;
        NotifyScoresChanged();
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

    private void OnDestroy()
    {
        isSpawned = false;

        if (Instance == this)
            Instance = null;
    }
}
