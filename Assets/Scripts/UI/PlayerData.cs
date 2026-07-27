using System;
using Fusion;
using UnityEngine;

namespace UI
{
    public class PlayerData : NetworkBehaviour
    {
        public static event Action PlayerDataChanged;

        [Networked, OnChangedRender(nameof(OnDataChanged))]
        public NetworkString<_16> NickName { get; set; }

        [Networked, OnChangedRender(nameof(OnDataChanged))]
        public Color Color { get; set; }

        [Networked]
        public int TeamId { get; private set; } = -1;

        public override void Spawned()
        {
            NotifyChanged();

            if (!HasInputAuthority)
                return;

            var pendingName = PlayerPrefs.GetString("PendingNickname", "");
            var pendingColourName = PlayerPrefs.GetString("PendingColour", "White");

            if (string.IsNullOrWhiteSpace(pendingName))
                return;

            Rpc_SetNicknameAndColor(
                pendingName,
                ColorFromName(pendingColourName));
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            NotifyChanged();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void Rpc_SetNicknameAndColor(string nickname, Color requestedColor)
        {
            NickName = nickname;

            if (TeamId < 0)
                Color = requestedColor;

            NotifyChanged();
        }

        public void SetTeam(int teamId, Color teamColor)
        {
            if (!Object.HasStateAuthority)
                return;

            TeamId = teamId;
            Color = teamColor;
            NotifyChanged();
        }

        private void OnDataChanged()
        {
            if (!Object.HasStateAuthority)
                NotifyChanged();
        }

        private static void NotifyChanged()
        {
            PlayerDataChanged?.Invoke();
        }

        private static Color ColorFromName(string name)
        {
            return name switch
            {
                "Red" => UnityEngine.Color.red,
                "Blue" => UnityEngine.Color.blue,
                "Green" => UnityEngine.Color.green,
                "Yellow" => UnityEngine.Color.yellow,
                _ => UnityEngine.Color.white
            };
        }
    }
}
