using System;
using System.Collections.Generic;
using Enums;
using Fusion;
using UnityEngine;

public sealed class UpgradeShop : NetworkBehaviour
{
    public static event Action LocalShopChanged;

    public static UpgradeShop Local { get; private set; }

    public event Action Changed;
    public event Action<UpgradeType, bool> PurchaseCompleted;

    [SerializeField] private UpgradePrices upgradePrices;

    [Networked, Capacity((int)UpgradeType.COUNT)]
    private NetworkDictionary<UpgradeType, int> PurchasedLevels => default;

    [Networked, OnChangedRender(nameof(OnUpgradeRevisionChanged))]
    private int UpgradeRevision { get; set; }

    private readonly Dictionary<UpgradeType, int> pendingLevels = new();
    private readonly List<UpgradeType> completedPendingLevels = new();

    public PlayerRef Player => Object ? Object.InputAuthority : Fusion.PlayerRef.None;
    public UpgradePrices Prices => upgradePrices;

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            Local = this;
            LocalShopChanged?.Invoke();
        }

        NotifyChanged();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        ClearLocalShop();
        pendingLevels.Clear();
    }

    public bool TryBuyUpgrade(UpgradeType type)
    {
        if (!Object ||
            !Object.HasInputAuthority ||
            pendingLevels.ContainsKey(type) ||
            !TryGetOffer(type, out var offer))
        {
            return false;
        }

        pendingLevels[type] = offer.CurrentLevel + 1;
        Changed?.Invoke();
        RequestBuyUpgradeRpc(type);
        return true;
    }

    public bool TryGetOffer(UpgradeType type, out UpgradeOffer offer)
    {
        offer = default;

        if (!upgradePrices ||
            !upgradePrices.TryGet(type, out var upgrade) ||
            !TryGetPlayerScore(out var score))
        {
            return false;
        }

        var currentLevel = GetPurchasedLevel(type);

        if (!upgrade.TryGetPrice(currentLevel, out var price) || score < price)
            return false;

        offer = new UpgradeOffer(
            type,
            upgrade.DisplayName,
            upgrade.Icon,
            price,
            currentLevel,
            upgrade.PurchaseCount);

        return true;
    }

    public void GetAvailableUpgrades(List<UpgradeOffer> results)
    {
        results.Clear();

        if (!upgradePrices)
            return;

        foreach (var upgrade in upgradePrices.Upgrades)
        {
            if (upgrade is not null && TryGetOffer(upgrade.Type, out var offer))
                results.Add(offer);
        }
    }

    public int GetPurchasedLevel(UpgradeType type)
    {
        return PurchasedLevels.ContainsKey(type)
            ? PurchasedLevels.Get(type)
            : 0;
    }

    public bool IsPurchasePending(UpgradeType type)
    {
        return pendingLevels.ContainsKey(type);
    }

    public void ApplyProjectileUpgrades(Projectile projectile)
    {
        if (!Object.HasStateAuthority || !projectile)
            return;

        foreach (var purchasedUpgrade in PurchasedLevels)
            Upgrade.ApplyUpgrade(projectile, purchasedUpgrade.Key, purchasedUpgrade.Value);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestBuyUpgradeRpc(UpgradeType type)
    {
        var purchased = TryBuyUpgradeOnServer(type);
        PurchaseResultRpc(type, purchased);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void PurchaseResultRpc(UpgradeType type, NetworkBool purchased)
    {
        if (!purchased)
        {
            pendingLevels.Remove(type);
            Changed?.Invoke();
        }

        PurchaseCompleted?.Invoke(type, purchased);
    }

    private bool TryBuyUpgradeOnServer(UpgradeType type)
    {
        if (!Object.HasStateAuthority ||
            !Runner ||
            !Runner.IsPlayerValid(Object.InputAuthority) ||
            !upgradePrices ||
            !upgradePrices.TryGet(type, out var upgrade))
        {
            return false;
        }

        var currentLevel = GetPurchasedLevel(type);

        if (!upgrade.TryGetPrice(currentLevel, out var price))
            return false;

        var scoreManager = ScoreManager.Instance;

        if (!scoreManager ||
            !scoreManager.TrySpendScore(Object.InputAuthority, price))
        {
            return false;
        }

        PurchasedLevels.Set(type, currentLevel + 1);
        UpgradeRevision++;
        NotifyChanged();
        return true;
    }

    private bool TryGetPlayerScore(out int score)
    {
        var scoreManager = ScoreManager.Instance;

        if (!scoreManager)
        {
            score = 0;
            return false;
        }

        return scoreManager.TryGetScore(Object.InputAuthority, out score);
    }

    private void OnUpgradeRevisionChanged()
    {
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        completedPendingLevels.Clear();

        foreach (var pendingLevel in pendingLevels)
        {
            if (GetPurchasedLevel(pendingLevel.Key) >= pendingLevel.Value)
                completedPendingLevels.Add(pendingLevel.Key);
        }

        foreach (var type in completedPendingLevels)
            pendingLevels.Remove(type);

        Changed?.Invoke();
    }

    private void ClearLocalShop()
    {
        if (Local != this)
            return;

        Local = null;
        LocalShopChanged?.Invoke();
    }

    private void OnDestroy()
    {
        ClearLocalShop();
    }
}

public readonly struct UpgradeOffer
{
    public UpgradeOffer(
        UpgradeType type,
        string displayName,
        Sprite icon,
        int price,
        int currentLevel,
        int maxLevel)
    {
        Type = type;
        DisplayName = displayName;
        Icon = icon;
        Price = price;
        CurrentLevel = currentLevel;
        MaxLevel = maxLevel;
    }

    public UpgradeType Type { get; }
    public string DisplayName { get; }
    public Sprite Icon { get; }
    public int Price { get; }
    public int CurrentLevel { get; }
    public int MaxLevel { get; }
}
