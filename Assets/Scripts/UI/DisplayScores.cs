using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;

public class DisplayScores : NetworkBehaviour
{
    [SerializeField] private TMP_Text scoresText;

    private void OnEnable()
    {
        ScoreManager.ScoresChanged += RefreshScores;
        UI.PlayerData.PlayerDataChanged += RefreshScores;
        ShowEmptyScores();
        RefreshScores();
    }

    private void OnDisable()
    {
        ScoreManager.ScoresChanged -= RefreshScores;
        UI.PlayerData.PlayerDataChanged -= RefreshScores;
    }

    public override void Spawned()
    {
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

        var runner = scoreManager.Runner;
        var scoreLines = scores
            .OrderByDescending(score => score.Value)
            .ThenBy(score => score.Key.PlayerId)
            .Select(score => BuildScoreLine(runner, score.Key, score.Value));

        var body = string.Join("\n", scoreLines);
        scoresText.text = string.IsNullOrEmpty(body)
            ? "Scores:"
            : $"Scores:\n{body}";
    }

    private void ShowEmptyScores()
    {
        if (scoresText != null)
            scoresText.text = "Scores:";
    }

    private static string BuildScoreLine(
        NetworkRunner runner,
        PlayerRef player,
        int score)
    {
        var playerName = $"Player {player.PlayerId}";
        var playerColor = Color.white;

        if (runner != null)
        {
            var playerObject = runner.GetPlayerObject(player);

            if (playerObject != null &&
                playerObject.TryGetComponent(out UI.PlayerData playerData))
            {
                var nickname = playerData.NickName.ToString();

                if (!string.IsNullOrWhiteSpace(nickname))
                    playerName = nickname;

                playerColor = playerData.Color;
            }
        }

        var colorHex = ColorUtility.ToHtmlStringRGB(playerColor);
        return $"<color=#{colorHex}>{playerName}: {score}</color>";
    }
}
