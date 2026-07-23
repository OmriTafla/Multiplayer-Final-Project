using Fusion;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

public class NicknameSubmitUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputNicknameField;
    [SerializeField] private TMP_Dropdown dropdownColour;

    public void SignIn()
    {
        if (string.IsNullOrWhiteSpace(inputNicknameField.text)) return;
        PlayerPrefs.SetString("PendingNickname", inputNicknameField.text);
        PlayerPrefs.SetString("PendingColour", dropdownColour.options[dropdownColour.value].text);
    }
}