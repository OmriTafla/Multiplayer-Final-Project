using System;
using System.Collections.Generic;
using System.Linq;
using Enums;
using Fusion;
using Managers;
using UnityEngine;

[Serializable]
public sealed class LiveLeaderboardSnapshot
{
    public int version = 1;
    public int gameMode;
    public string teamZeroName = "Blue Team";
    public string teamOneName = "Red Team";
    public string teamZeroColor = "267FFF";
    public string teamOneColor = "FF3333";
    public LiveLeaderboardEntry[] players = Array.Empty<LiveLeaderboardEntry>();

    public static string Capture(
        NetworkRunner runner,
        Dictionary<PlayerRef, int> scores,
        TeamsManager teamsManager,
        IOGameMode gameMode)
    {
        var snapshot = new LiveLeaderboardSnapshot
        {
            gameMode = (int)gameMode
        };

        if (teamsManager && teamsManager.IsReady)
        {
            snapshot.teamZeroName = teamsManager.GetTeamName(0);
            snapshot.teamOneName = teamsManager.GetTeamName(1);
            snapshot.teamZeroColor =
                ColorUtility.ToHtmlStringRGB(teamsManager.GetTeamColor(0));
            snapshot.teamOneColor =
                ColorUtility.ToHtmlStringRGB(teamsManager.GetTeamColor(1));
        }

        snapshot.players = scores
            .OrderBy(score => score.Key.PlayerId)
            .Select(score => BuildEntry(
                runner,
                teamsManager,
                score.Key,
                score.Value))
            .ToArray();

        return JsonUtility.ToJson(snapshot);
    }

    public static bool TryParse(string json, out LiveLeaderboardSnapshot snapshot)
    {
        snapshot = null;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            snapshot = JsonUtility.FromJson<LiveLeaderboardSnapshot>(json);

            if (snapshot is null)
                return false;

            snapshot.players ??= Array.Empty<LiveLeaderboardEntry>();
            return true;
        }
        catch (ArgumentException)
        {
            snapshot = null;
            return false;
        }
    }

    public string BuildBody(IOGameMode mode)
    {
        return mode == IOGameMode.TwoTeams
            ? BuildTwoTeamsBody()
            : BuildFreeForAllBody();
    }

    private string BuildFreeForAllBody()
    {
        return string.Join(
            "\n",
            players
                .OrderByDescending(player => player.score)
                .ThenBy(player => player.playerId)
                .Select((player, index) =>
                    $"{index + 1}. {BuildPlayerLine(player)}"));
    }

    private string BuildTwoTeamsBody()
    {
        var sections = new List<string>
        {
            BuildTeamSection(0, teamZeroName, teamZeroColor),
            BuildTeamSection(1, teamOneName, teamOneColor)
        };

        var unassigned = players
            .Where(player => player.teamId < 0 || player.teamId > 1)
            .OrderByDescending(player => player.score)
            .ThenBy(player => player.playerId)
            .ToArray();

        if (unassigned.Length > 0)
        {
            sections.Add(string.Join(
                "\n",
                new[] { "<b>Unassigned</b>" }
                    .Concat(unassigned.Select(player =>
                        $"  {BuildPlayerLine(player)}"))));
        }

        return string.Join("\n\n", sections);
    }

    private string BuildTeamSection(int teamId, string teamName, string teamColor)
    {
        var teamPlayers = players
            .Where(player => player.teamId == teamId)
            .OrderByDescending(player => player.score)
            .ThenBy(player => player.playerId)
            .ToArray();

        var total = teamPlayers.Sum(player => player.score);
        var lines = new List<string>
        {
            $"<color=#{SanitizeColor(teamColor)}><b>{EscapeText(teamName)}: {total}</b></color>"
        };

        lines.AddRange(teamPlayers.Select(player =>
            $"  {BuildPlayerLine(player)}"));

        return string.Join("\n", lines);
    }

    private static LiveLeaderboardEntry BuildEntry(
        NetworkRunner runner,
        TeamsManager teamsManager,
        PlayerRef player,
        int score)
    {
        var entry = new LiveLeaderboardEntry
        {
            playerId = player.PlayerId,
            playerName = $"Player {player.PlayerId}",
            score = score,
            teamId = -1,
            color = "FFFFFF"
        };

        var playerObject = runner
            ? runner.GetPlayerObject(player)
            : null;

        if (playerObject &&
            playerObject.TryGetComponent(out UI.PlayerData playerData) &&
            playerData.IsReady)
        {
            var nickname = playerData.NickName.ToString();

            if (!string.IsNullOrWhiteSpace(nickname))
                entry.playerName = nickname;

            entry.teamId = playerData.TeamId;
            entry.color = ColorUtility.ToHtmlStringRGB(playerData.Color);
        }

        if (entry.teamId < 0 &&
            teamsManager &&
            teamsManager.IsReady &&
            teamsManager.TryGetTeam(player, out var assignedTeam))
        {
            entry.teamId = assignedTeam;
            entry.color = ColorUtility.ToHtmlStringRGB(
                teamsManager.GetPlayerColor(assignedTeam));
        }

        return entry;
    }

    private static string BuildPlayerLine(LiveLeaderboardEntry player)
    {
        return
            $"<color=#{SanitizeColor(player.color)}>{EscapeText(player.playerName)}: {player.score}</color>";
    }

    private static string EscapeText(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Player"
            : value.Trim().Replace("<", "‹").Replace(">", "›");
    }

    private static string SanitizeColor(string value)
    {
        var normalized = value?.Trim().TrimStart('#');

        if (!string.IsNullOrWhiteSpace(normalized) &&
            ColorUtility.TryParseHtmlString($"#{normalized}", out _))
        {
            return normalized;
        }

        return "FFFFFF";
    }
}

[Serializable]
public sealed class LiveLeaderboardEntry
{
    public int playerId;
    public string playerName;
    public int score;
    public int teamId;
    public string color;
}
