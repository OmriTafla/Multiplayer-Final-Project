using System.Collections.Generic;
using System.Linq;
using Fusion;
using Managers;
using TMPro;
using UnityEngine;

public class DisplayScores : NetworkBehaviour
{
    [SerializeField] private TMP_Text scoresText;

    private TeamsManager teamsManager;

    private void OnEnable()
    {
        ScoreManager.ScoresChanged += RefreshScores;
        UI.PlayerData.PlayerDataChanged += RefreshScores;
        TeamsManager.RulesChanged += RefreshScores;
        ShowEmptyScores();
        RefreshScores();
    }

    private void OnDisable()
    {
        ScoreManager.ScoresChanged -= RefreshScores;
        UI.PlayerData.PlayerDataChanged -= RefreshScores;
        TeamsManager.RulesChanged -= RefreshScores;
    }

    public override void Spawned()
    {
        ResolveTeamsManager();
        RefreshScores();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        ShowEmptyScores();
    }

    public void DisplayScoresTextRPC()
    {
        RefreshScores();
    }

    public void RefreshScores()
    {
        if (scoresText == null)
            return;

        var scoreManager = ScoreManager.Instance;

        if (scoreManager == null ||
            !scoreManager.IsReady ||
            !scoreManager.TryGetScores(out var scores))
        {
            ShowEmptyScores();
            return;
        }

        ResolveTeamsManager();

        var entries = BuildEntries(scoreManager.Runner, scores);

        if (teamsManager != null && teamsManager.IsTwoTeams)
            DisplayTeamScores(entries);
        else
            DisplayFreeForAllScores(entries);
    }

    private void DisplayFreeForAllScores(List<PlayerScoreEntry> entries)
    {
        var lines = entries
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Player.PlayerId)
            .Select(BuildPlayerLine);

        var body = string.Join("\n", lines);
        scoresText.text = string.IsNullOrEmpty(body)
            ? "Free For All"
            : $"Free For All\n{body}";
    }

    private void DisplayTeamScores(List<PlayerScoreEntry> entries)
    {
        var sections = new List<string>();

        for (var teamId = 0; teamId <= 1; teamId++)
        {
            var teamEntries = entries
                .Where(entry => entry.TeamId == teamId)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.Player.PlayerId)
                .ToList();

            var teamScore = teamEntries.Sum(entry => entry.Score);
            var teamName = teamsManager.GetTeamName(teamId);
            var teamColor = teamsManager.GetTeamColor(teamId);
            var teamColorHex = ColorUtility.ToHtmlStringRGB(teamColor);
            var lines = new List<string>
            {
                $"<color=#{teamColorHex}><b>{teamName}: {teamScore}</b></color>"
            };

            lines.AddRange(teamEntries.Select(BuildPlayerLine));
            sections.Add(string.Join("\n", lines));
        }

        scoresText.text = $"Two Teams\n{string.Join("\n\n", sections)}";
    }

    private static List<PlayerScoreEntry> BuildEntries(
        NetworkRunner runner,
        Dictionary<PlayerRef, int> scores)
    {
        var entries = new List<PlayerScoreEntry>();

        foreach (var score in scores)
        {
            var playerName = $"Player {score.Key.PlayerId}";
            var playerColor = Color.white;
            var teamId = -1;

            if (runner != null)
            {
                var playerObject = runner.GetPlayerObject(score.Key);

                if (playerObject != null &&
                    playerObject.TryGetComponent(out UI.PlayerData playerData))
                {
                    var nickname = playerData.NickName.ToString();

                    if (!string.IsNullOrWhiteSpace(nickname))
                        playerName = nickname;

                    playerColor = playerData.Color;
                    teamId = playerData.TeamId;
                }
            }

            entries.Add(new PlayerScoreEntry(
                score.Key,
                playerName,
                playerColor,
                teamId,
                score.Value));
        }

        return entries;
    }

    private static string BuildPlayerLine(PlayerScoreEntry entry)
    {
        var colorHex = ColorUtility.ToHtmlStringRGB(entry.Color);
        return $"<color=#{colorHex}>  {entry.Name}: {entry.Score}</color>";
    }

    private void ShowEmptyScores()
    {
        if (scoresText != null)
            scoresText.text = "Scores:";
    }

    private void ResolveTeamsManager()
    {
        if (teamsManager == null)
            teamsManager = FindAnyObjectByType<TeamsManager>();
    }

    private sealed class PlayerScoreEntry
    {
        public PlayerRef Player { get; }
        public string Name { get; }
        public Color Color { get; }
        public int TeamId { get; }
        public int Score { get; }

        public PlayerScoreEntry(
            PlayerRef player,
            string name,
            Color color,
            int teamId,
            int score)
        {
            Player = player;
            Name = name;
            Color = color;
            TeamId = teamId;
            Score = score;
        }
    }
}
