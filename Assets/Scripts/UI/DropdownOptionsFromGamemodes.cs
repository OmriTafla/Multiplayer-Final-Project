using System.Linq;
using Enums;
using TMPro;
using UnityEngine;
using EnumUtils;

[RequireComponent(typeof(TMP_Dropdown))]
public class DropdownOptionsFromGamemodes : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private bool includeAny = true;

    private void OnValidate()
    {
        dropdown = GetComponent<TMP_Dropdown>();

        var gameModes = (typeof(IOGameMode).GetEnumValues() as IOGameMode[]).Select(_ => _);

        if (!includeAny)
            gameModes = gameModes.Where(mode => mode != IOGameMode.Any);
        
        dropdown.options = gameModes.Select(
            mode => new TMP_Dropdown.OptionData(
                mode.GetDisplayName())).ToList();
    }
}
