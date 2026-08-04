using System.Collections.Generic;
using UnityEngine;

public sealed class UpgradeShopUI : MonoBehaviour
{
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private UpgradeShopItemUI itemPrefab;
    [SerializeField] private GameObject emptyState;

    private readonly List<UpgradeOffer> offers = new();
    private readonly List<UpgradeShopItemUI> items = new();
    private UpgradeShop shop;

    private void OnEnable()
    {
        ScoreManager.ScoresChanged += Refresh;
        UpgradeShop.LocalShopChanged += BindLocalShop;
        BindLocalShop();
    }

    private void OnDisable()
    {
        ScoreManager.ScoresChanged -= Refresh;
        UpgradeShop.LocalShopChanged -= BindLocalShop;
        Bind(null);
    }

    private void BindLocalShop()
    {
        Bind(UpgradeShop.Local);
    }

    private void Bind(UpgradeShop newShop)
    {
        if (shop == newShop)
        {
            Refresh();
            return;
        }

        if (shop)
            shop.Changed -= Refresh;

        shop = newShop;

        if (shop)
            shop.Changed += Refresh;

        Refresh();
    }

    private void Refresh()
    {
        offers.Clear();

        if (shop)
            shop.GetAvailableUpgrades(offers);

        EnsureItemCount(offers.Count);

        for (var index = 0; index < items.Count; index++)
        {
            var visible = index < offers.Count;
            items[index].gameObject.SetActive(visible);

            if (visible)
                items[index].Bind(shop, offers[index]);
        }

        if (emptyState)
            emptyState.SetActive(offers.Count == 0);
    }

    private void EnsureItemCount(int count)
    {
        if (!contentRoot || !itemPrefab)
            return;

        while (items.Count < count)
        {
            var item = Instantiate(itemPrefab, contentRoot);
            items.Add(item);
        }
    }
}
