using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace UI
{
    public class PlayerData : NetworkBehaviour
    {
        public static event Action PlayerDataChanged;

        private static readonly Dictionary<PlayerRef, PlayerData> SpawnedPlayers = new();

        [Networked, OnChangedRender(nameof(OnDataChanged))]
        public NetworkString<_16> NickName { get; set; }

        [Networked, OnChangedRender(nameof(OnDataChanged))]
        public Color Color { get; set; }

        [Networked, OnChangedRender(nameof(OnDataChanged))]
        public int TeamId { get; private set; } = -1;

        private bool isSpawned;
        private PlayerRef registeredPlayer = PlayerRef.None;

        public bool IsReady => isSpawned;

        public override void Spawned()
        {
            isSpawned = true;
            registeredPlayer = Object.InputAuthority;

            if (registeredPlayer != PlayerRef.None)
                SpawnedPlayers[registeredPlayer] = this;

            NotifyChanged();

            if (!HasInputAuthority)
                return;

            var pendingName = PlayerPrefs.GetString("PendingNickname", string.Empty);

            if (string.IsNullOrWhiteSpace(pendingName))
                return;

            RpcSetNickname(pendingName.Trim());
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            isSpawned = false;
            Unregister();
            NotifyChanged();
        }

        public static bool TryGet(PlayerRef player, out PlayerData playerData)
        {
            if (SpawnedPlayers.TryGetValue(player, out playerData) &&
                playerData &&
                playerData.isSpawned)
            {
                return true;
            }

            SpawnedPlayers.Remove(player);
            playerData = null;
            return false;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcSetNickname(string nickname)
        {
            NickName = nickname;
            NotifyChanged();
        }

        public void SetTeam(int teamId, Color playerColor)
        {
            if (!isSpawned || !Object.HasStateAuthority)
                return;

            TeamId = teamId;
            Color = playerColor;
            NotifyChanged();
        }

        private void OnDataChanged()
        {
            if (isSpawned && !Object.HasStateAuthority)
                NotifyChanged();
        }

        private static void NotifyChanged()
        {
            PlayerDataChanged?.Invoke();
        }

        private void OnDestroy()
        {
            isSpawned = false;
            Unregister();
        }

        private void Unregister()
        {
            if (registeredPlayer == PlayerRef.None)
                return;

            if (SpawnedPlayers.TryGetValue(registeredPlayer, out var playerData) &&
                playerData == this)
            {
                SpawnedPlayers.Remove(registeredPlayer);
            }

            registeredPlayer = PlayerRef.None;
        }
    }
}
