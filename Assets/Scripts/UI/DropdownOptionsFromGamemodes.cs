using System;
using System.Collections.Generic;
using Enums;
using EnumUtils;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Dropdown))]
public class DropdownOptionsFromGamemodes : MonoBehaviour
{
    private static readonly IOGameMode[] GameModes =
        (IOGameMode[])Enum.GetValues(typeof(IOGameMode));

    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private bool includeAny;

    public TMP_Dropdown Dropdown
    {
        get
        {
            return dropdown;
        }
    }

    public bool IncludesAny => includeAny;

    private void Awake()
    {
        RefreshOptions();
    }

    private void OnValidate()
    {
        RefreshOptions();
    }

    public bool TryGetSelectedGameMode(out IOGameMode gameMode)
    {
        gameMode = default;

        if (!dropdown)
            return false;

        var modeIndex = dropdown.value - (includeAny ? 1 : 0);
        if (modeIndex < 0 || modeIndex >= GameModes.Length)
            return false;

        gameMode = GameModes[modeIndex];
        return true;
    }

    public void SetSelectedGameMode(IOGameMode gameMode)
    {
        if (!dropdown)
            return;

        var value = (int)gameMode + (includeAny ? 1 : 0);
        dropdown.SetValueWithoutNotify(value);
        dropdown.RefreshShownValue();
    }

    private void RefreshOptions()
    {
        if (!dropdown)
            return;

        var selectedValue = dropdown.value;
        var options = new List<TMP_Dropdown.OptionData>();

        if (includeAny)
            options.Add(new TMP_Dropdown.OptionData("All Game Modes"));

        foreach (var gameMode in GameModes)
            options.Add(new TMP_Dropdown.OptionData(gameMode.GetDisplayName()));

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(Mathf.Clamp(selectedValue, 0, options.Count - 1));
        dropdown.RefreshShownValue();
    }
}
