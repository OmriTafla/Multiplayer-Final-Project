using System.Collections.Generic;
using System.Linq;
using Enums;
using Fusion;
using UnityEngine;

namespace Managers
{
    public class TeamsManager : NetworkBehaviour
    {
        [Networked, Capacity(GameManager.MAX_PLAYERS), UnitySerializeField]
        private NetworkDictionary<PlayerRef, int> PlayersInTeams => default;

        public void AutoAssignPlayerToTeam(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;
            
            if (!GameManager.Instance)
            {
                Debug.LogError("Teams Manager can't assign teams because there's no Game Manager to report game mode");
                return;
            }
            
            switch (GameManager.Instance.GameMode)
            {
                case IOGameMode.FreeForAll:
                    PlayersInTeams.Set(player, player.PlayerId);
                    break;
                
                case IOGameMode.TwoTeams:
                    if (PlayersInTeams.Count == 0)
                    {
                        PlayersInTeams.Set(player, 0);
                        break;
                    }
                    
                    Dictionary<int, int> teamSizes = new();
                    foreach (var playerInTeam in PlayersInTeams)
                    {
                        if (teamSizes.TryAdd(playerInTeam.Value, 1))
                        {
                            continue;
                        }
                        
                        teamSizes[playerInTeam.Value]++;
                    }

                    if (teamSizes.Count == 1)
                    {
                        PlayersInTeams.Set(player, 1);
                        break;
                    }
                    
                    var smallestTeamSize = teamSizes.Min(team => team.Value);
                    var chosenTeam = teamSizes
                        .First(team => team.Value == smallestTeamSize)
                        .Key;
                    
                    PlayersInTeams.Set(player, chosenTeam);
                    
                    break;
                
                default:
                    Debug.LogError("New player was not added to team because game mode isn't supported");
                    break;
            }
        }

        public void HandlePlayerLeft(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;
            
            if (!PlayersInTeams.Remove(player))
            {
                Debug.LogWarning($"Tried to remove player {player.PlayerId} from teams but it wasn't in a team");
            }
        }
    }
}