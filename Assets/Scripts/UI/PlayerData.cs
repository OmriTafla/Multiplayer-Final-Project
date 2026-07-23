using Fusion;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class PlayerData : NetworkBehaviour
    {
        [Networked] public NetworkString<_16> NickName { get; set; }
        [Networked] public Color Color { get; set; }

        public override void Spawned()
        {
            if (!HasInputAuthority) return;

            string pendingName = PlayerPrefs.GetString("PendingNickname", "");
            string pendingColourName = PlayerPrefs.GetString("PendingColour", "White");

            if (string.IsNullOrWhiteSpace(pendingName)) return;

            Color pendingColor = ColorFromName(pendingColourName);
            Rpc_SetNicknameAndColor(pendingName, pendingColor);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_SetNicknameAndColor(string nickname, Color color)
        {
            NickName = nickname;
            Color = color;
        }

        private static Color ColorFromName(string name)
        {
            return name switch
            {
                "Red" => Color.red,
                "Blue" => Color.blue,
                "Green" => Color.green,
                "Yellow" => Color.yellow,
                _ => Color.white,
            };
        }
    }
}