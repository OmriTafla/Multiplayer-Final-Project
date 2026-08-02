using System.Collections.Generic;
using System.Linq;
using Enums;
using EnumUtils;
using Fusion;
using Managers;
using TMPro;
using UnityEngine;

public class DisplayScores : NetworkBehaviour
{
    [SerializeField] private TMP_Text scoresText;
    [SerializeField] private TeamsManager teamsManager;

    private void OnEnable()
    {
        ScoreManager.ScoresChanged += RefreshScores;
        UI.PlayerData.PlayerDataChanged += RefreshScores;
        TeamsManager.RulesChanged += RefreshScores;
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
        RefreshScores();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        ShowEmptyScores();
    }

    public void RefreshScores()
    {
        if (!scoresText)
            return;

        var scoreManager = ScoreManager.Instance;

        if (!scoreManager ||
            !scoreManager.IsReady ||
            !scoreManager.TryGetScores(out var scores))
        {
            ShowEmptyScores();
            return;
        }

        var entries = BuildEntries(scores);
        var body = teamsManager &&
                   teamsManager.IsReady &&
                   teamsManager.IsTwoTeams
            ? BuildTwoTeamsBody(entries)
            : BuildFreeForAllBody(entries);

        scoresText.text = BuildDisplayText(body);
    }

    private string BuildFreeForAllBody(List<PlayerScoreEntry> entries)
    {
        return string.Join(
            "\n",
            entries
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.Player.PlayerId)
                .Select((entry, index) =>
                    $"{index + 1}. {BuildPlayerScoreLine(entry)}"));
    }

    private string BuildTwoTeamsBody(List<PlayerScoreEntry> entries)
    {
        var sections = new List<string>();

        for (var teamId = 0; teamId <= 1; teamId++)
        {
            var teamEntries = entries
                .Where(entry => entry.TeamId == teamId)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.Player.PlayerId)
                .ToArray();

            var teamName = teamsManager.GetTeamName(teamId);
            var teamColor = ColorUtility.ToHtmlStringRGB(
                teamsManager.GetTeamColor(teamId));
            var teamTotal = teamEntries.Sum(entry => entry.Score);
            var lines = new List<string>
            {
                $"<color=#{teamColor}><b>{teamName}: {teamTotal}</b></color>"
            };

            lines.AddRange(teamEntries.Select(entry =>
                $"  {BuildPlayerScoreLine(entry)}"));
            sections.Add(string.Join("\n", lines));
        }

        var unassigned = entries
            .Where(entry => entry.TeamId < 0 || entry.TeamId > 1)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Player.PlayerId)
            .ToArray();

        if (unassigned.Length > 0)
        {
            sections.Add(string.Join(
                "\n",
                new[] { "<b>Unassigned</b>" }
                    .Concat(unassigned.Select(entry =>
                        $"  {BuildPlayerScoreLine(entry)}"))));
        }

        return string.Join("\n\n", sections);
    }

    private List<PlayerScoreEntry> BuildEntries(
        Dictionary<PlayerRef, int> scores)
    {
        var entries = new List<PlayerScoreEntry>();

        foreach (var score in scores)
        {
            var playerName = $"Player {score.Key.PlayerId}";
            var playerColor = Color.white;
            var teamId = -1;

            if (UI.PlayerData.TryGet(score.Key, out var playerData) &&
                playerData.IsReady)
            {
                var nickname = playerData.NickName.ToString();

                if (!string.IsNullOrWhiteSpace(nickname))
                    playerName = nickname;

                playerColor = playerData.Color;
                teamId = playerData.TeamId;
            }

            if (teamId < 0 &&
                teamsManager &&
                teamsManager.IsReady &&
                teamsManager.TryGetTeam(score.Key, out var assignedTeam))
            {
                teamId = assignedTeam;
                playerColor = teamsManager.GetPlayerColor(assignedTeam);
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

    private string BuildDisplayText(string body)
    {
        var gameMode = teamsManager && teamsManager.IsReady
            ? teamsManager.ActiveGameMode
            : GameManager.Instance
                ? GameManager.Instance.GameMode
                : IOGameMode.FreeForAll;
        var modeName = gameMode.GetDisplayName();

        return string.IsNullOrWhiteSpace(body)
            ? $"<b>Mode: {modeName}</b>\n\nPlayers"
            : $"<b>Mode: {modeName}</b>\n\n{body}";
    }

    private static string BuildPlayerScoreLine(PlayerScoreEntry entry)
    {
        var colorHex = ColorUtility.ToHtmlStringRGB(entry.Color);
        var playerName = entry.Name
            .Replace("<", "‹")
            .Replace(">", "›");
        return $"<color=#{colorHex}>{playerName}: {entry.Score}</color>";
    }

    private void ShowEmptyScores()
    {
        if (scoresText)
            scoresText.text = BuildDisplayText(string.Empty);
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
