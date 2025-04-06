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
    private Action<string> onPurchaseCallback;
    
    public void Initialize(DLCStoreManager.ChessSkin skin, bool isOwned, Action<string> purchaseCallback)
    {
        // Set skin data
        skinId = skin.skinId;
        skinNameText.text = skin.skinName;
        skinDescriptionText.text = skin.skinDescription;
        previewImage.sprite = skin.previewImage;
        onPurchaseCallback = purchaseCallback;
        
        // Configure the purchase button based on ownership
        if (isOwned)
        {
            buttonText.text = "EQUIP";
            priceText.text = "";
            purchaseButton.interactable = true;
        }
        else
        {
            buttonText.text = "BUY";
            priceText.text = skin.price.ToString();
            purchaseButton.interactable = true;
            purchaseButton.onClick.AddListener(() => OnPurchaseClicked());
        }
    }
    
    private void OnPurchaseClicked()
    {
        onPurchaseCallback?.Invoke(skinId);
    }
}