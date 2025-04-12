using UnityEngine;
using UnityEngine.UI;
using System;

public class SkinItemUI : MonoBehaviour
{
    [SerializeField] private Text skinNameText;
    [SerializeField] private Text skinDescriptionText;
    [SerializeField] private Text priceText;
    [SerializeField] private Image previewImage;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Text buttonText;
    
    private string skinId;
    private int skinIndex;
    private Action<string> onPurchaseCallback;
    private DLCStoreManager.ChessSkin skinData;
    
    public void Initialize(DLCStoreManager.ChessSkin skin, bool isOwned, Action<string> purchaseCallback)
    {
        // Set skin data
        skinId = skin.skinId;
        skinData = skin;
        skinNameText.text = skin.skinName;
        skinDescriptionText.text = skin.skinDescription;
        previewImage.sprite = skin.previewImage;
        onPurchaseCallback = purchaseCallback;
        
        // Store the index of this skin in the available skins list
        skinIndex = DLCStoreManager.Instance.GetSkinIndex(skinId);
        
        // Configure the purchase button based on ownership
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
    
    private void OnPurchaseClicked()
    {
        onPurchaseCallback?.Invoke(skinId);
    }
    
    private void OnEquipClicked()
    {
        // Call the equip method on DLCStoreManager
        DLCStoreManager.Instance.EquipSkin(skinIndex);
    }
}