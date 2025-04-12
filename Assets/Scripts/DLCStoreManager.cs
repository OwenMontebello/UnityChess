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
    
    // New UI elements for skin notification
    [Header("Skin Notification")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private Text notificationText;
    [SerializeField] private float notificationDuration = 3f;

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
    
    // Coroutine reference for notification
    private Coroutine notificationCoroutine;

    private void Start()
    {
        // Optionally clear old data for testing:
        // PlayerPrefs.DeleteKey("PlayerCredits");

        purchaseHandler = new PurchaseTransactionHandler();

        if (closeStoreButton != null)
            closeStoreButton.onClick.AddListener(CloseStore);
        if (storePanel != null)
            storePanel.SetActive(false);
            
        // Initialize notification panel
        if (notificationPanel != null)
            notificationPanel.SetActive(false);

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
        
        // Uncomment this line to create test buttons
        // TestPlayerNotifications();
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
        
        // Debug log for notification
        Debug.Log($"About to show notification for skin: {skin.skinName}");

        // Get player side info for clearer messaging
        string playerSideStr = "You";
        Side playerSide = Side.White; // Default

        // Try to get the player's side from UIManager if available
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

        // Show notification locally with clearer message
        string localMessage = $"{playerSideStr} equipped the {skin.skinName} skin!";
        ShowSkinNotification(localMessage);
        
        // Send notification to all clients if in network mode
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("NetworkManager detected, sending network notification");
            
            // Use the player side we just determined
            string sideStr = playerSide.ToString();
            Debug.Log($"Sending skin notification over network. Skin: {skin.skinName}, Side: {sideStr}");
            
            if (NetworkManager.Singleton.IsServer)
            {
                // If server, broadcast to all clients
                Debug.Log("Broadcasting from server directly");
                BoardNetworkHandler.Instance.NotifySkinEquippedClientRpc(skin.skinName, playerSide == Side.White ? 0 : 1, NetworkManager.Singleton.LocalClientId);
            }
            else
            {
                // If client, request server to broadcast
                Debug.Log("Requesting server to broadcast");
                BoardNetworkHandler.Instance.NotifySkinEquippedServerRpc(skin.skinName, sideStr);
            }
        }
        
        Debug.Log($"Equipped skin: {skin.skinName}");
    }
    
    // Show notification locally on this client
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
        
        // Instead of trying to use SkinNotificationPanel component,
        // we'll handle the notification directly in the DLCStoreManager
        // which is guaranteed to be active
        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
            Debug.Log("Stopped existing notification coroutine");
        }
        
        Debug.Log("Starting notification coroutine from DLCStoreManager");
        notificationCoroutine = StartCoroutine(ShowNotificationCoroutine(skinName));
    }

    private IEnumerator ShowNotificationCoroutine(string skinName)
    {
        Debug.Log($"ShowNotificationCoroutine started for: {skinName}");
        
        // Format the message to include player information
        string playerInfo = "";
        
        // Check if we're in network mode
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // Determine if this is a local notification or from another player
            if (skinName.StartsWith("You equipped"))
            {
                // This is a local notification, use as is
                notificationText.text = skinName;
            }
            else
            {
                // This is a remote player notification, use the passed message
                notificationText.text = skinName;
            }
        }
        else
        {
            // Single player mode
            notificationText.text = skinName;
        }
        
        // Show notification
        notificationPanel.SetActive(true);
        Debug.Log($"Notification panel activated with message: {notificationText.text}");
        
        // Wait for duration
        yield return new WaitForSeconds(notificationDuration);
        
        // Hide notification
        notificationPanel.SetActive(false);
        notificationCoroutine = null;
        Debug.Log("Notification panel hidden");
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
    
    // Helper method to get skin index by ID
    public int GetSkinIndex(string skinId)
    {
        for (int i = 0; i < availableSkins.Count; i++)
        {
            if (availableSkins[i].skinId == skinId)
                return i;
        }
        return -1;
    }
    
    // Test method to manually trigger notification
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
    
    // Method to test both types of notifications
    public void TestPlayerNotifications()
    {
        // Create a test button in the UI if not already present
        if (GameObject.Find("TestNotificationButton") == null)
        {
            // Find canvas
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError("Canvas not found for test buttons");
                return;
            }
            
            // Create button for local player notification
            GameObject localButtonObj = new GameObject("TestLocalNotification");
            localButtonObj.transform.SetParent(canvas.transform, false);
            
            RectTransform localRt = localButtonObj.AddComponent<RectTransform>();
            localRt.anchoredPosition = new Vector2(100, 50);
            localRt.sizeDelta = new Vector2(160, 30);
            
            Image localImg = localButtonObj.AddComponent<Image>();
            localImg.color = new Color(0.2f, 0.3f, 0.8f);
            
            Button localButton = localButtonObj.AddComponent<Button>();
            
            // Add text
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
            
            // Create button for remote player notification
            GameObject remoteButtonObj = new GameObject("TestRemoteNotification");
            remoteButtonObj.transform.SetParent(canvas.transform, false);
            
            RectTransform remoteRt = remoteButtonObj.AddComponent<RectTransform>();
            remoteRt.anchoredPosition = new Vector2(100, 0);
            remoteRt.sizeDelta = new Vector2(160, 30);
            
            Image remoteImg = remoteButtonObj.AddComponent<Image>();
            remoteImg.color = new Color(0.8f, 0.3f, 0.2f);
            
            Button remoteButton = remoteButtonObj.AddComponent<Button>();
            
            // Add text
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