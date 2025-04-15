using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using UnityChess;
using System.Collections;
using Unity.Netcode;
using System.Reflection;

public class DLCStoreManager : MonoBehaviourSingleton<DLCStoreManager>
{
    // Data structure for chess skins
    [System.Serializable]
    public class ChessSkin
    {
        public string skinId;
        public string skinName;
        public string skinDescription;
        public int price = 50;
        public Sprite previewImage;
        public string firstSkinId;  // For White pieces
        public string secondSkinId; // For Black pieces
    }

    // UI elements
    [Header("UI References")]
    [SerializeField] private GameObject storePanel;
    [SerializeField] private GameObject GameAnalyticsui;
    [SerializeField] private Transform skinItemsContainer;
    [SerializeField] private GameObject skinItemPrefab;
    [SerializeField] private Button closeStoreButton;
    [SerializeField] private Text playerCurrencyText;
    
    // Notification UI
    [Header("Skin Notification")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private Text notificationText;
    [SerializeField] private float notificationDuration = 3f;

    // Available skins data
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

    // Skin loading system
    [Header("Dynamic Skin Loading")]
    [SerializeField] private FirebaseMaterialDownloader materialDownloader;

    // Player inventory
    private List<string> ownedSkinIds = new List<string>();
    private string currentEquippedSkinId = "BlackXWhite";

    // Player economy
    private PurchaseTransactionHandler purchaseHandler;
    
    // Notification animation
    private Coroutine notificationCoroutine;

    private void Start()
    {
        // Setup economy system
        purchaseHandler = new PurchaseTransactionHandler();

        // Setup UI
        if (closeStoreButton != null)
            closeStoreButton.onClick.AddListener(CloseStore);
        if (storePanel != null)
            storePanel.SetActive(false);
        if (GameAnalyticsui != null)
            GameAnalyticsui.SetActive(false);
        // Hide notification initially
        if (notificationPanel != null)
            notificationPanel.SetActive(false);

        // Default skin is always owned
        ownedSkinIds.Add("BlackXWhite");

        // Find material downloader
        if (materialDownloader == null)
        {
            materialDownloader = FindObjectOfType<FirebaseMaterialDownloader>();
            if (materialDownloader == null)
                Debug.LogError("FirebaseMaterialDownloader not found!");
        }

        // Load saved data
        LoadPlayerData();
        UpdateCurrencyDisplay();
        PopulateStoreItems();
    }

    // Show the skin store
    public void OpenStore()
    {
        storePanel.SetActive(true);
        UpdateCurrencyDisplay();
    }

    // Show analytics dashboard
    public void OpenGameAnalyticsui()
    {
        GameAnalyticsui.SetActive(true);
    }

    // Hide the skin store
    public void CloseStore()
    {
        storePanel.SetActive(false);
    }

    // Fill store with skin items
    private void PopulateStoreItems()
    {
        if (skinItemsContainer != null)
        {
            // Clear existing items
            foreach (Transform child in skinItemsContainer)
                Destroy(child.gameObject);

            // Create item for each skin
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

    // Buy a skin
    public void PurchaseSkin(string skinId)
    {
        ChessSkin skinToPurchase = availableSkins.FirstOrDefault(s => s.skinId == skinId);
        if (skinToPurchase != null && !ownedSkinIds.Contains(skinId))
        {
            if (purchaseHandler.PurchaseSkin(skinId, skinToPurchase.price))
            {
                // Update UI and data
                LoadPlayerData();
                UpdateCurrencyDisplay();
                PopulateStoreItems();

                // Download skin files
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

    // Apply a skin to pieces
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

        // Buy if not owned
        if (!ownedSkinIds.Contains(skin.skinId))
        {
            if (purchaseHandler.PurchaseSkin(skin.skinId, skin.price))
            {
                // Update UI and data
                LoadPlayerData();
                UpdateCurrencyDisplay();
                PopulateStoreItems();
                // Download skin files
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
            // Pay to equip
            if (purchaseHandler.DeductCredits(skin.price))
                Debug.Log($"Deducted £{skin.price} for equipping {skin.skinName}");
            else
            {
                Debug.Log($"Not enough credits to equip skin! Price: £{skin.price}, Current: {purchaseHandler.GetPlayerCredits()}");
                return;
            }
        }

        // Download missing files
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

        // Apply the skin
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

        // Save equipped skin
        currentEquippedSkinId = skin.skinId;
        SavePlayerData();
        UpdateCurrencyDisplay();
        
        // Setup notification
        Debug.Log($"About to show notification for skin: {skin.skinName}");

        // Get player side for message
        string playerSideStr = "You";
        Side playerSide = Side.White; // Default

        // Try to get player side
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && UIManager.Instance != null)
        {
            FieldInfo fieldInfo = typeof(UIManager).GetField("localPlayerSide", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo != null)
            {
                playerSide = (Side)fieldInfo.GetValue(UIManager.Instance);
                playerSideStr = playerSide.ToString();
                Debug.Log($"Retrieved player side: {playerSide}");
            }
        }

        // Show local notification
        string localMessage = $"{playerSideStr} equipped the {skin.skinName} skin!";
        ShowSkinNotification(localMessage);
        
        // Show to other players if networked
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("NetworkManager detected, sending network notification");
            
            // Use player side
            string sideStr = playerSide.ToString();
            Debug.Log($"Sending skin notification over network. Skin: {skin.skinName}, Side: {sideStr}");
            
            if (NetworkManager.Singleton.IsServer)
            {
                // Server broadcasts directly
                Debug.Log("Broadcasting from server directly");
                BoardNetworkHandler.Instance.NotifySkinEquippedClientRpc(skin.skinName, playerSide == Side.White ? 0 : 1, NetworkManager.Singleton.LocalClientId);
            }
            else
            {
                // Client asks server to broadcast
                Debug.Log("Requesting server to broadcast");
                BoardNetworkHandler.Instance.NotifySkinEquippedServerRpc(skin.skinName, sideStr);
            }
        }
        
        Debug.Log($"Equipped skin: {skin.skinName}");
    }
    
    // Show notification to local player
    public void ShowSkinNotification(string skinName)
    {
        Debug.Log($"DLCStoreManager.ShowSkinNotification called with: {skinName}");
        
        if (notificationPanel == null)
        {
            Debug.LogError("Notification panel is NULL! Please assign it in the inspector.");
            return;
        }
        
        if (notificationText == null)
        {
            Debug.LogError("Notification text is NULL! Please assign it in the inspector.");
            return;
        }
        
        // Handle notification in DLCStoreManager
        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
            Debug.Log("Stopped existing notification coroutine");
        }
        
        Debug.Log("Starting notification coroutine from DLCStoreManager");
        notificationCoroutine = StartCoroutine(ShowNotificationCoroutine(skinName));
    }

    // Notification animation
    private IEnumerator ShowNotificationCoroutine(string skinName)
    {
        Debug.Log($"ShowNotificationCoroutine started for: {skinName}");
        
        // Format message based on context
        string playerInfo = "";
        
        // Check for network mode
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // Local vs remote notification
            if (skinName.StartsWith("You equipped"))
            {
                // Local notification
                notificationText.text = skinName;
            }
            else
            {
                // Remote notification
                notificationText.text = skinName;
            }
        }
        else
        {
            // Single player
            notificationText.text = skinName;
        }
        
        // Show notification
        notificationPanel.SetActive(true);
        Debug.Log($"Notification panel activated with message: {notificationText.text}");
        
        // Display duration
        yield return new WaitForSeconds(notificationDuration);
        
        // Hide notification
        notificationPanel.SetActive(false);
        notificationCoroutine = null;
        Debug.Log("Notification panel hidden");
    }
    
    // Check if skin files exist
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

    // Apply original materials
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

    // Update coin display
    private void UpdateCurrencyDisplay()
    {
        int currentCredits = purchaseHandler.GetPlayerCredits();
        if (playerCurrencyText != null)
        {
            playerCurrencyText.text = $"£ {currentCredits}";
            Debug.Log("Updated currency display: £ " + currentCredits);
        }
    }

    // Save player inventory
    private void SavePlayerData()
    {
        // Save owned skins
        string ownedSkinsList = string.Join(",", ownedSkinIds);
        PlayerPrefs.SetString("OwnedSkins", ownedSkinsList);
        // Save currency
        PlayerPrefs.SetInt("PlayerCredits", purchaseHandler.GetPlayerCredits());
        PlayerPrefs.SetString("EquippedSkin", currentEquippedSkinId);
        PlayerPrefs.Save();
        Debug.Log("Player data saved.");
    }

    // Load player inventory
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

    // Add free currency
    public void AddFiftyCurrency()
    {
        purchaseHandler.AddCurrency(50);
        UpdateCurrencyDisplay();
    }
    
    // Find skin index by ID
    public int GetSkinIndex(string skinId)
    {
        for (int i = 0; i < availableSkins.Count; i++)
        {
            if (availableSkins[i].skinId == skinId)
                return i;
        }
        return -1;
    }
    
    // Test notification system
    public void TestNotification()
    {
        Debug.Log("Testing notification");
        
        if (notificationPanel != null && notificationText != null)
        {
            ShowSkinNotification("Test Notification");
        }
        else
        {
            Debug.LogError($"Cannot show notification. Panel: {notificationPanel != null}, Text: {notificationText != null}");
        }
    }
    
    // Create test notification buttons
    public void TestPlayerNotifications()
    {
        // Create test button if needed
        if (GameObject.Find("TestNotificationButton") == null)
        {
            // Find canvas
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError("Canvas not found for test buttons");
                return;
            }
            
            // Create local test button
            GameObject localButtonObj = new GameObject("TestLocalNotification");
            localButtonObj.transform.SetParent(canvas.transform, false);
            
            RectTransform localRt = localButtonObj.AddComponent<RectTransform>();
            localRt.anchoredPosition = new Vector2(100, 50);
            localRt.sizeDelta = new Vector2(160, 30);
            
            Image localImg = localButtonObj.AddComponent<Image>();
            localImg.color = new Color(0.2f, 0.3f, 0.8f);
            
            Button localButton = localButtonObj.AddComponent<Button>();
            
            // Add button text
            GameObject localTxtObj = new GameObject("Text");
            localTxtObj.transform.SetParent(localButtonObj.transform, false);
            
            RectTransform localTxtRT = localTxtObj.AddComponent<RectTransform>();
            localTxtRT.anchorMin = Vector2.zero;
            localTxtRT.anchorMax = Vector2.one;
            localTxtRT.sizeDelta = Vector2.zero;
            
            Text localTxt = localTxtObj.AddComponent<Text>();
            localTxt.text = "Test Local";
            localTxt.alignment = TextAnchor.MiddleCenter;
            localTxt.color = Color.white;
            
            localButton.onClick.AddListener(() => {
                ShowSkinNotification("You equipped the Test Skin!");
            });
            
            // Create remote test button
            GameObject remoteButtonObj = new GameObject("TestRemoteNotification");
            remoteButtonObj.transform.SetParent(canvas.transform, false);
            
            RectTransform remoteRt = remoteButtonObj.AddComponent<RectTransform>();
            remoteRt.anchoredPosition = new Vector2(100, 0);
            remoteRt.sizeDelta = new Vector2(160, 30);
            
            Image remoteImg = remoteButtonObj.AddComponent<Image>();
            remoteImg.color = new Color(0.8f, 0.3f, 0.2f);
            
            Button remoteButton = remoteButtonObj.AddComponent<Button>();
            
            // Add button text
            GameObject remoteTxtObj = new GameObject("Text");
            remoteTxtObj.transform.SetParent(remoteButtonObj.transform, false);
            
            RectTransform remoteTxtRT = remoteTxtObj.AddComponent<RectTransform>();
            remoteTxtRT.anchorMin = Vector2.zero;
            remoteTxtRT.anchorMax = Vector2.one;
            remoteTxtRT.sizeDelta = Vector2.zero;
            
            Text remoteTxt = remoteTxtObj.AddComponent<Text>();
            remoteTxt.text = "Test Remote";
            remoteTxt.alignment = TextAnchor.MiddleCenter;
            remoteTxt.color = Color.white;
            
            remoteButton.onClick.AddListener(() => {
                ShowSkinNotification("White player equipped the Test Skin!");
            });
            
            Debug.Log("Created test notification buttons");
        }
    }
}