using System;
using Enums;
using Fusion;
using UnityEngine;

namespace Managers
{
    public class TeamsManager : NetworkBehaviour
    {
        public static event Action RulesChanged;

        [SerializeField] private string teamZeroName = "Blue Team";
        [SerializeField] private string teamOneName = "Red Team";
        [SerializeField] private Color teamZeroColor = new Color(0.15f, 0.5f, 1f, 1f);
        [SerializeField] private Color teamOneColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color fallbackColor = Color.white;

        [Networked, Capacity(GameManager.MAX_PLAYERS), UnitySerializeField]
        private NetworkDictionary<PlayerRef, int> PlayersInTeams => default;

        [Networked, OnChangedRender(nameof(OnRulesChanged))]
        private int ActiveGameModeValue { get; set; }

        [Networked, OnChangedRender(nameof(OnRulesChanged))]
        private int TeamRevision { get; set; }

        public IOGameMode ActiveGameMode
        {
            get
            {
                return (IOGameMode)ActiveGameModeValue == IOGameMode.TwoTeams
                    ? IOGameMode.TwoTeams
                    : IOGameMode.FreeForAll;
            }
        }

        public bool IsFreeForAll => ActiveGameMode == IOGameMode.FreeForAll;
        public bool IsTwoTeams => ActiveGameMode == IOGameMode.TwoTeams;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                var configuredMode = GameManager.Instance != null
                    ? GameManager.Instance.GameMode
                    : IOGameMode.TwoTeams;

                ActiveGameModeValue = (int)configuredMode;
            }

            NotifyRulesChanged();
        }

        public int AutoAssignPlayerToTeam(PlayerRef player)
        {
            if (!Object.HasStateAuthority || player == PlayerRef.None)
                return -1;

            if (PlayersInTeams.TryGet(player, out var existingTeam))
                return existingTeam;

            var teamId = IsFreeForAll
                ? player.PlayerId
                : GetBalancedTeam();

            PlayersInTeams.Add(player, teamId);
            MarkTeamsChanged();
            return teamId;
        }

        public bool TryGetTeam(PlayerRef player, out int teamId)
        {
            return PlayersInTeams.TryGet(player, out teamId);
        }

        public bool AreTeammates(PlayerRef firstPlayer, PlayerRef secondPlayer)
        {
            if (!IsTwoTeams || firstPlayer == PlayerRef.None || secondPlayer == PlayerRef.None)
                return false;

            return PlayersInTeams.TryGet(firstPlayer, out var firstTeam) &&
                   PlayersInTeams.TryGet(secondPlayer, out var secondTeam) &&
                   firstTeam == secondTeam;
        }

        public bool CanDamage(PlayerRef attacker, PlayerRef target)
        {
            if (attacker == PlayerRef.None || target == PlayerRef.None || attacker == target)
                return false;

            if (IsFreeForAll)
                return true;

            if (!PlayersInTeams.TryGet(attacker, out var attackerTeam))
                return false;

            if (!PlayersInTeams.TryGet(target, out var targetTeam))
                return false;

            return attackerTeam != targetTeam;
        }

        public Color GetPlayerColor(int teamId)
        {
            if (IsTwoTeams)
                return GetTeamColor(teamId);

            var hue = Mathf.Repeat(teamId * 0.61803398875f, 1f);
            return Color.HSVToRGB(hue, 0.65f, 1f);
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

        public string GetTeamName(int teamId)
        {
            return teamId switch
            {
                0 => teamZeroName,
                1 => teamOneName,
                _ => $"Team {teamId + 1}"
            };
        }

        public void HandlePlayerLeft(PlayerRef player)
        {
            if (!Object.HasStateAuthority)
                return;

            if (PlayersInTeams.Remove(player))
                MarkTeamsChanged();
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

        private void MarkTeamsChanged()
        {
            TeamRevision++;
            NotifyRulesChanged();
        }

        private void OnRulesChanged()
        {
            NotifyRulesChanged();
        }

        private static void NotifyRulesChanged()
        {
            RulesChanged?.Invoke();
        }
    }
}
