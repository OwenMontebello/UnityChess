using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using UnityChess;

public class DLCStoreManager : MonoBehaviourSingleton<DLCStoreManager>
{
    [System.Serializable]
    public class ChessSkin
    {
        public string skinId;
        public string skinName;
        public string skinDescription;
        public int price = 50;
        public Sprite previewImage;
        
        public string firstSkinId;  // White pieces skin type (Gold, Silver, etc.)
        public string secondSkinId; // Black pieces skin type
    }

    [Header("UI References")]
    [SerializeField] private GameObject storePanel;
    [SerializeField] private Transform skinItemsContainer;
    [SerializeField] private GameObject skinItemPrefab;
    [SerializeField] private Button closeStoreButton;
    [SerializeField] private Text playerCurrencyText;

    [Header("Store Configuration")]
    [SerializeField] private List<ChessSkin> availableSkins = new List<ChessSkin>
    {
        new ChessSkin 
        {
            skinId = "YellowXSilver", 
            skinName = "Yellow X Silver", 
            skinDescription = "Gold and Silver Pieces",
            price = 50,
            firstSkinId = "Gold",
            secondSkinId = "Silver"
        },
        new ChessSkin 
        {
            skinId = "RedXBlue", 
            skinName = "Red X Blue", 
            skinDescription = "Red and Blue Pieces",
            price = 50,
            firstSkinId = "Red",
            secondSkinId = "Blue"
        },
        new ChessSkin 
        {
            skinId = "BlackXWhite", 
            skinName = "Black X White", 
            skinDescription = "Default Pieces",
            price = 0,
            firstSkinId = "Default",
            secondSkinId = "Default"
        }
    };
    
    [Header("Dynamic Skin Loading")]
    [SerializeField] private FirebaseMaterialDownloader materialDownloader;

    // Player currency (for demo purposes)
    private int playerCurrency = 1000;
    
    // List of skins the player owns
    private List<string> ownedSkinIds = new List<string>();
    
    // Currently equipped skin
    private string currentEquippedSkinId = "BlackXWhite";

    private void Start()
    {
        // Initialize store UI
        if (closeStoreButton != null)
            closeStoreButton.onClick.AddListener(CloseStore);
            
        // Hide store on startup
        if (storePanel != null)
            storePanel.SetActive(false);
            
        // Add default skin to owned skins
        ownedSkinIds.Add("BlackXWhite");
        
        // Find FirebaseMaterialDownloader if not assigned
        if (materialDownloader == null)
        {
            materialDownloader = FindObjectOfType<FirebaseMaterialDownloader>();
            if (materialDownloader == null)
            {
                Debug.LogError("FirebaseMaterialDownloader not found!");
            }
        }
        
        // Initialize the store items
        PopulateStoreItems();
    }
    
    public void OpenStore()
    {
        storePanel.SetActive(true);
        UpdateCurrencyDisplay();
    }
    
    public void CloseStore()
    {
        storePanel.SetActive(false);
    }
    
    private void PopulateStoreItems()
    {
        // Clear existing items
        if (skinItemsContainer != null)
        {
            foreach (Transform child in skinItemsContainer)
            {
                Destroy(child.gameObject);
            }
            
            // Create a new item for each skin
            foreach (ChessSkin skin in availableSkins)
            {
                GameObject skinItemGO = Instantiate(skinItemPrefab, skinItemsContainer);
                SkinItemUI skinItem = skinItemGO.GetComponent<SkinItemUI>();
                
                if (skinItem != null)
                {
                    bool isOwned = ownedSkinIds.Contains(skin.skinId);
                    skinItem.Initialize(skin, isOwned, PurchaseSkin);
                }
            }
        }
        else
        {
            Debug.LogError("skinItemsContainer is not assigned!");
        }
    }
    
    public void PurchaseSkin(string skinId)
    {
        // Find the skin in available skins
        ChessSkin skinToPurchase = availableSkins.FirstOrDefault(s => s.skinId == skinId);
        
        if (skinToPurchase != null && !ownedSkinIds.Contains(skinId))
        {
            // Check if player has enough currency
            if (playerCurrency >= skinToPurchase.price)
            {
                // Deduct currency
                playerCurrency -= skinToPurchase.price;
                
                // Add to owned skins
                ownedSkinIds.Add(skinId);
                
                // Download the skin
                if (skinToPurchase.firstSkinId != "Default" && !string.IsNullOrEmpty(skinToPurchase.firstSkinId))
                {
                    materialDownloader.DownloadFullSkin(skinToPurchase.firstSkinId);
                }
                
                if (skinToPurchase.secondSkinId != "Default" && !string.IsNullOrEmpty(skinToPurchase.secondSkinId))
                {
                    materialDownloader.DownloadFullSkin(skinToPurchase.secondSkinId);
                }
                
                // Update UI
                UpdateCurrencyDisplay();
                PopulateStoreItems();
                
                Debug.Log($"Purchased skin: {skinToPurchase.skinName}");
            }
            else
            {
                Debug.Log("Not enough currency to purchase skin!");
                // You could show a UI message here
            }
        }
    }
    
    private void UpdateCurrencyDisplay()
    {
        if (playerCurrencyText != null)
        {
            playerCurrencyText.text = $"Credits: {playerCurrency}";
        }
    }
    
    // For testing - add currency to player
    public void AddCurrency(int amount)
    {
        playerCurrency += amount;
        UpdateCurrencyDisplay();
    }

    // Call this method from each equip button
    public void EquipSkin(int skinIndex)
    {
        Debug.Log($"Equipping skin at index {skinIndex}");
        
        if (skinIndex < 0 || skinIndex >= availableSkins.Count)
        {
            Debug.LogWarning($"Invalid skin index: {skinIndex}");
            return;
        }

        ChessSkin skin = availableSkins[skinIndex];
        Debug.Log($"Selected skin: {skin.skinName}, firstSkinId: {skin.firstSkinId}, secondSkinId: {skin.secondSkinId}");
        
        // Check if skin is owned or purchasable
        if (!ownedSkinIds.Contains(skin.skinId))
        {
            if (playerCurrency >= skin.price)
            {
                Debug.Log($"Purchasing skin {skin.skinName} for {skin.price} credits");
                playerCurrency -= skin.price;
                ownedSkinIds.Add(skin.skinId);
                
                // Download the skin immediately after purchase
                if (skin.firstSkinId != "Default" && !string.IsNullOrEmpty(skin.firstSkinId))
                {
                    materialDownloader.DownloadFullSkin(skin.firstSkinId);
                }
                
                if (skin.secondSkinId != "Default" && !string.IsNullOrEmpty(skin.secondSkinId))
                {
                    materialDownloader.DownloadFullSkin(skin.secondSkinId);
                }
                
                UpdateCurrencyDisplay();
                PopulateStoreItems();
            }
            else
            {
                Debug.Log($"Not enough currency to purchase skin! Price: {skin.price}");
                return;
            }
        }

        // Apply skin
        if (skin.skinId == "BlackXWhite")
        {
            // Apply default materials
            ApplyDefaultMaterials();
        }
        else
        {
            // Apply custom materials
            if (skin.firstSkinId != "Default" && !string.IsNullOrEmpty(skin.firstSkinId))
            {
                materialDownloader.ApplySkinToPieces(skin.firstSkinId, Side.White);
            }
            
            if (skin.secondSkinId != "Default" && !string.IsNullOrEmpty(skin.secondSkinId))
            {
                materialDownloader.ApplySkinToPieces(skin.secondSkinId, Side.Black);
            }
        }
        
        // Save the equipped skin
        currentEquippedSkinId = skin.skinId;
        Debug.Log($"Equipped skin: {skin.skinName}");
    }
    
    private void ApplyDefaultMaterials()
    {
        Debug.Log("Applying default black and white materials");
        
        VisualPiece[] pieces = FindObjectsOfType<VisualPiece>();
        
        foreach (VisualPiece piece in pieces)
        {
            Renderer renderer = piece.GetComponent<Renderer>();
            if (renderer == null) continue;
            
            // Create a basic material
            Material defaultMaterial = new Material(Shader.Find("Standard"));
            defaultMaterial.color = piece.PieceColor == Side.White ? Color.white : Color.black;
            
            renderer.material = defaultMaterial;
            Debug.Log($"Applied default material to {piece.name}");
        }
    }
}