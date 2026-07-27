using Enums;
using Fusion;
using UnityEngine;

namespace Managers
{
    public class TeamsManager : NetworkBehaviour
    {
        [SerializeField] private Color teamZeroColor = new Color(0.15f, 0.5f, 1f, 1f);
        [SerializeField] private Color teamOneColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color fallbackColor = Color.white;

        [Networked, Capacity(GameManager.MAX_PLAYERS), UnitySerializeField]
        private NetworkDictionary<PlayerRef, int> PlayersInTeams => default;

        public int AutoAssignPlayerToTeam(PlayerRef player)
        {
            if (!Object.HasStateAuthority || player == PlayerRef.None)
                return -1;

            if (PlayersInTeams.TryGet(player, out var existingTeam))
                return existingTeam;

            var gameMode = GameManager.Instance != null
                ? GameManager.Instance.GameMode
                : IOGameMode.TwoTeams;

            var teamId = gameMode == IOGameMode.FreeForAll
                ? player.PlayerId
                : GetBalancedTeam();

            PlayersInTeams.Add(player, teamId);
            return teamId;
        }

        public bool TryGetTeam(PlayerRef player, out int teamId)
        {
            return PlayersInTeams.TryGet(player, out teamId);
        }

        public bool AreTeammates(PlayerRef firstPlayer, PlayerRef secondPlayer)
        {
            return PlayersInTeams.TryGet(firstPlayer, out var firstTeam) &&
                   PlayersInTeams.TryGet(secondPlayer, out var secondTeam) &&
                   firstTeam == secondTeam;
        }

        public Color GetTeamColor(int teamId)
        {
            return teamId switch
            {
                0 => teamZeroColor,
                1 => teamOneColor,
                _ => fallbackColor
            };
        }

        public void HandlePlayerLeft(PlayerRef player)
        {
            if (!Object.HasStateAuthority)
                return;

            PlayersInTeams.Remove(player);
        }

        private int GetBalancedTeam()
        {
            var teamZeroPlayers = 0;
            var teamOnePlayers = 0;

            foreach (var playerInTeam in PlayersInTeams)
            {
                if (playerInTeam.Value == 0)
                    teamZeroPlayers++;
                else if (playerInTeam.Value == 1)
                    teamOnePlayers++;
            }

            return teamZeroPlayers <= teamOnePlayers ? 0 : 1;
        }
    }
}
