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
    
    // References to materials for each piece type
    public Material whitePawnMaterial;
    public Material whiteRookMaterial;
    public Material whiteKnightMaterial;
    public Material whiteBishopMaterial;
    public Material whiteQueenMaterial;
    public Material whiteKingMaterial;
    
    public Material blackPawnMaterial;
    public Material blackRookMaterial;
    public Material blackKnightMaterial;
    public Material blackBishopMaterial;
    public Material blackQueenMaterial;
    public Material blackKingMaterial;
}

    [Header("UI References")]
    [SerializeField] private GameObject storePanel;
    [SerializeField] private Transform skinItemsContainer;
    [SerializeField] private GameObject skinItemPrefab;
    [SerializeField] private Button closeStoreButton;
    [SerializeField] private Text playerCurrencyText;

    [Header("Store Configuration")]
    [SerializeField] private List<ChessSkin> availableSkins = new List<ChessSkin>();
    
    // Player currency (for demo purposes)
    private int playerCurrency = 1000;
    
    // List of skins the player owns
    private List<string> ownedSkinIds = new List<string>();

    private void Start()
    {
        // Initialize store UI
        if (closeStoreButton != null)
            closeStoreButton.onClick.AddListener(CloseStore);
            
        // Hide store on startup
        if (storePanel != null)
            storePanel.SetActive(false);
            
        // Add default skin to owned skins
        ownedSkinIds.Add("default");
        
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
    if (skinIndex < 0 || skinIndex >= availableSkins.Count) {
        Debug.LogWarning($"Invalid skin index: {skinIndex}");
        return;
    }
    
    // Get the skin by index
    ChessSkin skin = availableSkins[skinIndex];
    
    // Check if player owns this skin
    if (!ownedSkinIds.Contains(skin.skinId)) {
        // Player doesn't own this skin yet - need to purchase it
        if (playerCurrency >= 50) {
            // Deduct currency
            playerCurrency -= 50;
            
            // Add skin to owned skins
            ownedSkinIds.Add(skin.skinId);
            
            // Update currency display
            UpdateCurrencyDisplay();
            
            // Now apply the skin
            ApplySkinToPieces(skin.skinId);
            
            Debug.Log($"Purchased and equipped skin: {skin.skinName}");
        } else {
            Debug.Log("Not enough currency to purchase this skin!");
            // Show message to player
        }
    } else {
        // Already owns the skin, just equip it
        ApplySkinToPieces(skin.skinId);
        Debug.Log($"Equipped skin: {skin.skinName}");
    }
}

private void ApplySkinToPieces(string skinId)
{
    Debug.Log($"Attempting to apply skin: {skinId}");
    
    // Save currently equipped skin
    PlayerPrefs.SetString("EquippedSkinId", skinId);
    
    // Find the selected skin
    ChessSkin skin = availableSkins.Find(s => s.skinId == skinId);
    if (skin == null)
    {
        Debug.LogError($"No skin found with ID: {skinId}");
        return;
    }
    
    // Find all pieces on the board
    VisualPiece[] pieces = FindObjectsOfType<VisualPiece>();
    
    Debug.Log($"Found {pieces.Length} visual pieces to update");
    
    foreach (VisualPiece piece in pieces)
    {
        // Get the renderer component
        Renderer renderer = piece.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning($"No renderer found for piece: {piece.name}");
            continue;
        }
        
        // Determine which material to use based on piece type and color
        Material newMaterial = null;
        string pieceName = piece.name.ToLower();
        
        Debug.Log($"Processing piece: {pieceName}, Color: {piece.PieceColor}");
        
        if (piece.PieceColor == Side.White)
        {
            if (pieceName.Contains("pawn"))
                newMaterial = skin.whitePawnMaterial;
            else if (pieceName.Contains("rook"))
                newMaterial = skin.whiteRookMaterial;
            else if (pieceName.Contains("knight"))
                newMaterial = skin.whiteKnightMaterial;
            else if (pieceName.Contains("bishop"))
                newMaterial = skin.whiteBishopMaterial;
            else if (pieceName.Contains("queen"))
                newMaterial = skin.whiteQueenMaterial;
            else if (pieceName.Contains("king"))
                newMaterial = skin.whiteKingMaterial;
        }
        else // Black pieces
        {
            if (pieceName.Contains("pawn"))
                newMaterial = skin.blackPawnMaterial;
            else if (pieceName.Contains("rook"))
                newMaterial = skin.blackRookMaterial;
            else if (pieceName.Contains("knight"))
                newMaterial = skin.blackKnightMaterial;
            else if (pieceName.Contains("bishop"))
                newMaterial = skin.blackBishopMaterial;
            else if (pieceName.Contains("queen"))
                newMaterial = skin.blackQueenMaterial;
            else if (pieceName.Contains("king"))
                newMaterial = skin.blackKingMaterial;
        }
        
        // Apply the material if found
        if (newMaterial != null)
        {
            Debug.Log($"Applying material to {pieceName}");
            renderer.material = newMaterial;
        }
        else
        {
            Debug.LogWarning($"No material found for piece: {pieceName}");
        }
    }
    
    Debug.Log("Skin application completed");


}
}
