using UnityEngine;
using UnityEngine.UI;
using System;

public class SkinItemUI : MonoBehaviour
{
    // UI elements
    [SerializeField] private Text skinNameText;
    [SerializeField] private Text skinDescriptionText;
    [SerializeField] private Text priceText;
    [SerializeField] private Image previewImage;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Text buttonText;
    
    // Skin data references
    private string skinId;
    private int skinIndex;
    private Action<string> onPurchaseCallback;
    private DLCStoreManager.ChessSkin skinData;
    
    // Setup UI display
    public void Initialize(DLCStoreManager.ChessSkin skin, bool isOwned, Action<string> purchaseCallback)
    {
        // Set skin info
        skinId = skin.skinId;
        skinData = skin;
        skinNameText.text = skin.skinName;
        skinDescriptionText.text = skin.skinDescription;
        previewImage.sprite = skin.previewImage;
        onPurchaseCallback = purchaseCallback;
        
        // Get skin index for equipping
        skinIndex = DLCStoreManager.Instance.GetSkinIndex(skinId);
        
        // Setup button based on ownership
        if (isOwned)
        {
            buttonText.text = "EQUIP";
            priceText.text = skin.price > 0 ? $"£{skin.price}" : "FREE";
            purchaseButton.interactable = true;
            purchaseButton.onClick.AddListener(() => OnEquipClicked());
        }
        else
        {
            buttonText.text = "BUY";
            priceText.text = $"£{skin.price}";
            purchaseButton.interactable = true;
            purchaseButton.onClick.AddListener(() => OnPurchaseClicked());
        }
    }
    
    // Handle buy button click
    private void OnPurchaseClicked()
    {
        onPurchaseCallback?.Invoke(skinId);
    }
    
    // Handle equip button click
    private void OnEquipClicked()
    {
        // Apply the skin
        DLCStoreManager.Instance.EquipSkin(skinIndex);
    }
}