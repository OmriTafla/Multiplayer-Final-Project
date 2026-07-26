using System.Collections.Generic;
using Fusion;
using Singleton;
using UnityEngine;

public class ScoreManager : NetworkSingleton<ScoreManager>
{
    [Networked, Capacity(64)]
    private NetworkDictionary<PlayerRef, int> Scores { get; } =
        MakeInitializer<PlayerRef, int>(new Dictionary<PlayerRef, int>());

    [SerializeField] private int scoreForHit = 1;

    public void AddScoreForHit(PlayerRef player)
    {
        if (!Object.HasStateAuthority || player == PlayerRef.None)
            return;

        if (!Scores.ContainsKey(player))
        {
            Scores.Add(player, scoreForHit);
            return;
        }

        Scores.Set(player, Scores.Get(player) + scoreForHit);
    }

    public void RemovePlayer(PlayerRef player)
    {
        if (!Object.HasStateAuthority)
            return;

        if (Scores.ContainsKey(player))
            Scores.Remove(player);
    }

    public Dictionary<PlayerRef, int> GetScores()
    {
        return new Dictionary<PlayerRef, int>(Scores);
    }

    [ContextMenu("Print Scores")]
    private void PrintScores()
    {
        foreach (var pair in Scores)
            Debug.Log($"{pair.Key.PlayerId}: {pair.Value}");
    }
}
