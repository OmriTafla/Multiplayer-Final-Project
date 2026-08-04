using Enums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UpgradeShopItemUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text levelLabel;
    [SerializeField] private TMP_Text priceLabel;
    [SerializeField] private Button buyButton;

    private UpgradeShop shop;
    private UpgradeType type;

    private void Awake()
    {
        if (buyButton)
            buyButton.onClick.AddListener(Buy);
    }

    private void OnDestroy()
    {
        if (buyButton)
            buyButton.onClick.RemoveListener(Buy);
    }

    public void Bind(UpgradeShop newShop, UpgradeOffer offer)
    {
        shop = newShop;
        type = offer.Type;

        if (icon)
        {
            icon.sprite = offer.Icon;
            icon.gameObject.SetActive(offer.Icon);
        }

        if (nameLabel)
            nameLabel.text = offer.DisplayName;

        if (levelLabel)
            levelLabel.text = $"Level {offer.CurrentLevel + 1}/{offer.MaxLevel}";

        if (priceLabel)
            priceLabel.text = $"Buy {offer.Price}";

        if (buyButton)
            buyButton.interactable = shop && !shop.IsPurchasePending(type);
    }

    private void Buy()
    {
        if (shop)
            shop.TryBuyUpgrade(type);
    }
}
