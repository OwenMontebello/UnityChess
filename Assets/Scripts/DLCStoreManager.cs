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
        public string firstSkinId;  // For White pieces (e.g., "Gold", "Red")
        public string secondSkinId; // For Black pieces (e.g., "Silver", "Blue")
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

    // Local list of owned skins and the currently equipped skin.
    private List<string> ownedSkinIds = new List<string>();
    private string currentEquippedSkinId = "BlackXWhite";

    // The single source of truth for currency.
    private PurchaseTransactionHandler purchaseHandler;

    private void Start()
    {
        // Optionally clear old data for testing:
        // PlayerPrefs.DeleteKey("PlayerCredits");

        purchaseHandler = new PurchaseTransactionHandler();

        if (closeStoreButton != null)
            closeStoreButton.onClick.AddListener(CloseStore);
        if (storePanel != null)
            storePanel.SetActive(false);

        // Add default skin.
        ownedSkinIds.Add("BlackXWhite");

        if (materialDownloader == null)
        {
            materialDownloader = FindObjectOfType<FirebaseMaterialDownloader>();
            if (materialDownloader == null)
                Debug.LogError("FirebaseMaterialDownloader not found!");
        }

        LoadPlayerData();
        UpdateCurrencyDisplay();
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
        if (skinItemsContainer != null)
        {
            foreach (Transform child in skinItemsContainer)
                Destroy(child.gameObject);

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
        ChessSkin skinToPurchase = availableSkins.FirstOrDefault(s => s.skinId == skinId);
        if (skinToPurchase != null && !ownedSkinIds.Contains(skinId))
        {
            if (purchaseHandler.PurchaseSkin(skinId, skinToPurchase.price))
            {
                LoadPlayerData();
                UpdateCurrencyDisplay();
                PopulateStoreItems();

                if (!string.IsNullOrEmpty(skinToPurchase.firstSkinId) && skinToPurchase.firstSkinId != "Default")
                    materialDownloader.DownloadFullSkin(skinToPurchase.firstSkinId);
                if (!string.IsNullOrEmpty(skinToPurchase.secondSkinId) && skinToPurchase.secondSkinId != "Default")
                    materialDownloader.DownloadFullSkin(skinToPurchase.secondSkinId);
                Debug.Log($"Purchased skin: {skinToPurchase.skinName}");
            }
            else
            {
                Debug.Log("Purchase transaction failed.");
            }
        }
    }

    public void EquipSkin(int skinIndex)
    {
        Debug.Log($"Equipping skin at index {skinIndex}");
        if (skinIndex < 0 || skinIndex >= availableSkins.Count)
        {
            Debug.LogWarning($"Invalid skin index: {skinIndex}");
            return;
        }
        ChessSkin skin = availableSkins[skinIndex];
        Debug.Log($"Selected skin: {skin.skinName}");

        // If not owned, purchase it.
        if (!ownedSkinIds.Contains(skin.skinId))
        {
            if (purchaseHandler.PurchaseSkin(skin.skinId, skin.price))
            {
                LoadPlayerData();
                UpdateCurrencyDisplay();
                PopulateStoreItems();
                if (!string.IsNullOrEmpty(skin.firstSkinId) && skin.firstSkinId != "Default")
                    materialDownloader.DownloadFullSkin(skin.firstSkinId);
                if (!string.IsNullOrEmpty(skin.secondSkinId) && skin.secondSkinId != "Default")
                    materialDownloader.DownloadFullSkin(skin.secondSkinId);
                Debug.Log($"After purchase, credits: £{purchaseHandler.GetPlayerCredits()}");
            }
            else
            {
                Debug.Log($"Not enough currency to purchase skin! Price: £{skin.price}, Current: £{purchaseHandler.GetPlayerCredits()}");
                return;
            }
        }
        else
        {
            // Deduct equip cost each time even if owned.
            if (purchaseHandler.DeductCredits(skin.price))
                Debug.Log($"Deducted £{skin.price} for equipping {skin.skinName}");
            else
            {
                Debug.Log($"Not enough credits to equip skin! Price: £{skin.price}, Current: {purchaseHandler.GetPlayerCredits()}");
                return;
            }
        }

        // Optional: re-download files if missing.
        if (!string.IsNullOrEmpty(skin.firstSkinId) && skin.firstSkinId != "Default")
        {
            if (!AreLocalFilesPresent(skin.firstSkinId))
            {
                Debug.Log($"Re-downloading {skin.firstSkinId} as local files are missing.");
                materialDownloader.DownloadFullSkin(skin.firstSkinId);
            }
        }
        if (!string.IsNullOrEmpty(skin.secondSkinId) && skin.secondSkinId != "Default")
        {
            if (!AreLocalFilesPresent(skin.secondSkinId))
            {
                Debug.Log($"Re-downloading {skin.secondSkinId} as local files are missing.");
                materialDownloader.DownloadFullSkin(skin.secondSkinId);
            }
        }

        if (skin.skinId == "BlackXWhite")
        {
            ApplyDefaultMaterials();
        }
        else
        {
            if (!string.IsNullOrEmpty(skin.firstSkinId) && skin.firstSkinId != "Default")
                materialDownloader.ApplySkinToPieces(skin.firstSkinId, Side.White);
            if (!string.IsNullOrEmpty(skin.secondSkinId) && skin.secondSkinId != "Default")
                materialDownloader.ApplySkinToPieces(skin.secondSkinId, Side.Black);
        }

        currentEquippedSkinId = skin.skinId;
        SavePlayerData();
        UpdateCurrencyDisplay();
        Debug.Log($"Equipped skin: {skin.skinName}");
    }

    private bool AreLocalFilesPresent(string skinName)
    {
        string[] pieceTypes = { "Pawn", "Rook", "Knight", "Bishop", "Queen", "King" };
        foreach (string pieceType in pieceTypes)
        {
            if (!materialDownloader.IsMaterialDownloaded(skinName, pieceType))
                return false;
        }
        return true;
    }

    private void ApplyDefaultMaterials()
    {
        Debug.Log("Applying default black and white materials");
        VisualPiece[] pieces = FindObjectsOfType<VisualPiece>();
        foreach (VisualPiece piece in pieces)
        {
            Renderer renderer = piece.GetComponent<Renderer>();
            if (renderer == null) continue;
            Material defaultMaterial = new Material(Shader.Find("Standard"));
            defaultMaterial.color = piece.PieceColor == Side.White ? Color.white : Color.black;
            renderer.material = defaultMaterial;
            Debug.Log($"Applied default material to {piece.name}");
        }
    }

    private void UpdateCurrencyDisplay()
    {
        int currentCredits = purchaseHandler.GetPlayerCredits();
        if (playerCurrencyText != null)
        {
            playerCurrencyText.text = $"£ {currentCredits}";
            Debug.Log("Updated currency display: £ " + currentCredits);
        }
    }

    private void SavePlayerData()
    {
        // Save owned skins locally.
        string ownedSkinsList = string.Join(",", ownedSkinIds);
        PlayerPrefs.SetString("OwnedSkins", ownedSkinsList);
        // Save credits from purchaseHandler.
        PlayerPrefs.SetInt("PlayerCredits", purchaseHandler.GetPlayerCredits());
        PlayerPrefs.SetString("EquippedSkin", currentEquippedSkinId);
        PlayerPrefs.Save();
        Debug.Log("Player data saved.");
    }

    private void LoadPlayerData()
    {
        if (PlayerPrefs.HasKey("OwnedSkins"))
        {
            string savedSkins = PlayerPrefs.GetString("OwnedSkins");
            if (!string.IsNullOrEmpty(savedSkins))
                ownedSkinIds = new List<string>(savedSkins.Split(','));
        }
        if (PlayerPrefs.HasKey("PlayerCredits"))
        {
            int credits = PlayerPrefs.GetInt("PlayerCredits");
            Debug.Log("Loaded PlayerCredits: " + credits);
        }
        else
        {
            Debug.Log("PlayerCredits not found, defaulting to 200");
        }
        if (PlayerPrefs.HasKey("EquippedSkin"))
            currentEquippedSkinId = PlayerPrefs.GetString("EquippedSkin");

        Debug.Log($"Player data loaded. Owned skins: {ownedSkinIds.Count}");
    }

    // NEW: Button method to add 50 currency.
    public void AddFiftyCurrency()
    {
        purchaseHandler.AddCurrency(50);
        UpdateCurrencyDisplay();
    }
}