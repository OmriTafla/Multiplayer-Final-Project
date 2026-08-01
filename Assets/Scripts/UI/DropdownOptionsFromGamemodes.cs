using System;
using System.Collections.Generic;
using Enums;
using EnumUtils;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Dropdown))]
public class DropdownOptionsFromGamemodes : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private bool includeAny;

    public TMP_Dropdown Dropdown
    {
        get
        {
            ResolveDropdown();
            return dropdown;
        }
    }

    public bool IncludesAny => includeAny;

    private void Awake()
    {
        ResolveDropdown();
        RefreshOptions();
    }

    private void Reset()
    {
        ResolveDropdown();
        RefreshOptions();
    }

    private void OnValidate()
    {
        ResolveDropdown();
        RefreshOptions();
    }

    public bool TryGetSelectedGameMode(out IOGameMode gameMode)
    {
        ResolveDropdown();
        gameMode = default;

        if (!dropdown)
            return false;

        var modeIndex = dropdown.value - (includeAny ? 1 : 0);
        var values = (IOGameMode[])Enum.GetValues(typeof(IOGameMode));

        if (modeIndex < 0 || modeIndex >= values.Length)
            return false;

        gameMode = values[modeIndex];
        return true;
    }

    public void SetSelectedGameMode(IOGameMode gameMode)
    {
        ResolveDropdown();

        if (!dropdown)
            return;

        var value = (int)gameMode + (includeAny ? 1 : 0);
        dropdown.SetValueWithoutNotify(value);
        dropdown.RefreshShownValue();
    }

    private void ResolveDropdown()
    {
        if (!dropdown)
            dropdown = GetComponent<TMP_Dropdown>();
    }

    private void RefreshOptions()
    {
        if (!dropdown)
            return;

        var selectedValue = dropdown.value;
        var options = new List<TMP_Dropdown.OptionData>();

        if (includeAny)
            options.Add(new TMP_Dropdown.OptionData("All Game Modes"));

        foreach (IOGameMode gameMode in Enum.GetValues(typeof(IOGameMode)))
            options.Add(new TMP_Dropdown.OptionData(gameMode.GetDisplayName()));

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(Mathf.Clamp(selectedValue, 0, options.Count - 1));
        dropdown.RefreshShownValue();
    }
}
