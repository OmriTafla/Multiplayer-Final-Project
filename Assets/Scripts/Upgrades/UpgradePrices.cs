using System;
using System.Collections.Generic;
using Enums;
using UnityEngine;

[CreateAssetMenu(fileName = "Upgrade Prices", menuName = "Multiplayer/Upgrade Prices")]
public sealed class UpgradePrices : ScriptableObject
{
    [SerializeField] private UpgradePrice[] upgrades = Array.Empty<UpgradePrice>();

    public IReadOnlyList<UpgradePrice> Upgrades => upgrades;

    public bool TryGet(UpgradeType type, out UpgradePrice upgrade)
    {
        if (upgrades is not null)
        {
            foreach (var candidate in upgrades)
            {
                if (candidate is not null && candidate.Type == type)
                {
                    upgrade = candidate;
                    return true;
                }
            }
        }

        upgrade = null;
        return false;
    }

    private void OnValidate()
    {
        upgrades ??= Array.Empty<UpgradePrice>();

        foreach (var upgrade in upgrades)
            upgrade?.Validate();
    }
}

[Serializable]
public sealed class UpgradePrice
{
    [SerializeField] private UpgradeType type;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField] private int[] prices = Array.Empty<int>();

    public UpgradeType Type => type;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? type.ToString()
        : displayName;
    public Sprite Icon => icon;
    public int PurchaseCount => prices?.Length ?? 0;

    public bool TryGetPrice(int purchasedCount, out int price)
    {
        if (prices is null ||
            purchasedCount < 0 ||
            purchasedCount >= prices.Length)
        {
            price = 0;
            return false;
        }

        price = prices[purchasedCount];
        return price >= 0;
    }

    public void Validate()
    {
        prices ??= Array.Empty<int>();

        for (var index = 0; index < prices.Length; index++)
            prices[index] = Mathf.Max(0, prices[index]);
    }
}
