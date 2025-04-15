using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityChess;

public class AnalyticsDashboard : MonoBehaviour
{
    // UI elements for displaying stats
    [Header("UI Components")]
    [SerializeField] private GameObject dashboardPanel;
    [SerializeField] private Text totalMatchesText;
    [SerializeField] private Text totalPurchasesText; 
    [SerializeField] private Text topSkinText;
    [SerializeField] private Text lastMatchResultText;
    [SerializeField] private Button closeDashboardButton;
    [SerializeField] private Button refreshDataButton;
    
    // Local stats tracking
    private int totalMatches = 0;
    private int totalPurchases = 0;
    private Dictionary<string, int> skinPurchaseCounts = new Dictionary<string, int>();
    private string lastMatchResult = "None";
    
    // Cloud data connection
    private FirestoreManager firestoreManager;
    
    private void Start()
    {
        Debug.Log("AnalyticsDashboard: Start called");
        
        // Set up button listeners
        if (closeDashboardButton != null)
            closeDashboardButton.onClick.AddListener(CloseDashboard);
            
        if (refreshDataButton != null)
            refreshDataButton.onClick.AddListener(RefreshData);
            
        // Hide dashboard initially
        if (dashboardPanel != null)
            dashboardPanel.SetActive(false);
        
        // Find or create Firestore connection
        firestoreManager = FindObjectOfType<FirestoreManager>();
        if (firestoreManager == null && FirebaseAnalyticsManager.Instance != null)
        {
            // Only create if Firebase exists
            GameObject firestoreObj = new GameObject("FirestoreManager");
            firestoreManager = firestoreObj.AddComponent<FirestoreManager>();
        }
            
        // Load fallback data
        LoadStatsFromPlayerPrefs();
            
        // Subscribe to game events
        SafeSubscribeToEvents();
        
        // Initial UI update
        UpdateUI();
        
        // Try cloud data first
        if (firestoreManager != null && firestoreManager.IsInitialized)
        {
            firestoreManager.GetGameStats(OnFirestoreDataReceived);
        }
        
        Debug.Log("AnalyticsDashboard: Start completed successfully");
    }
    
    // Process cloud data when received
    private void OnFirestoreDataReceived(Dictionary<string, object> data, bool success)
    {
        if (success && data != null)
        {
            Debug.Log("Successfully received Firestore data");
            
            // Update local values from cloud data
            if (data.TryGetValue("totalMatches", out object matchesObj) && matchesObj is long matchesLong)
                totalMatches = (int)matchesLong;
                
            if (data.TryGetValue("totalPurchases", out object purchasesObj) && purchasesObj is long purchasesLong)
                totalPurchases = (int)purchasesLong;
                
            if (data.TryGetValue("lastMatchResult", out object lastMatchObj) && lastMatchObj is string lastMatchStr)
                lastMatchResult = lastMatchStr;
            
            // Update skin purchase data
            skinPurchaseCounts.Clear();
            if (data.TryGetValue("skinPurchases", out object skinObj) && skinObj is Dictionary<string, object> skinDict)
            {
                foreach (var pair in skinDict)
                {
                    if (pair.Value is long countLong)
                        skinPurchaseCounts[pair.Key] = (int)countLong;
                }
            }
            
            // Refresh UI with new data
            UpdateUI();
            
            // Save as backup
            SaveStatsToPlayerPrefs();
        }
        else
        {
            Debug.LogWarning("Failed to get data from Firestore, using local data");
        }
    }
    
    // Safely connect to game events
    private void SafeSubscribeToEvents()
    {
        try
        {
            GameManager.NewGameStartedEvent += OnMatchStarted;
            GameManager.GameEndedEvent += OnMatchEnded;
            Debug.Log("Successfully subscribed to game events");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error subscribing to events: " + e.Message);
        }
    }
    
    // Clean up event connections
    private void OnDestroy()
    {
        try
        {
            GameManager.NewGameStartedEvent -= OnMatchStarted;
            GameManager.GameEndedEvent -= OnMatchEnded;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error unsubscribing from events: " + e.Message);
        }
    }
    
    // Track when a match begins
    private void OnMatchStarted()
    {
        totalMatches++;
        SaveStats();
        UpdateUI();
    }
    
    // Track match results
    private void OnMatchEnded()
    {
        try
        {
            if (GameManager.Instance != null && 
                GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove))
            {
                if (latestHalfMove.CausedCheckmate)
                {
                    lastMatchResult = $"{latestHalfMove.Piece.Owner} won by checkmate";
                }
                else if (latestHalfMove.CausedStalemate)
                {
                    lastMatchResult = "Draw by stalemate";
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error getting match result: " + e.Message);
            lastMatchResult = "Unknown result";
        }
        
        SaveStats();
        UpdateUI();
    }
    
// Track skin purchases
public void OnSkinPurchased(string skinName)
{
    totalPurchases++;
    
    if (!skinPurchaseCounts.ContainsKey(skinName))
        skinPurchaseCounts[skinName] = 0;
        
    skinPurchaseCounts[skinName]++;
    
    Debug.Log($"Skin purchased: {skinName}, total purchases: {totalPurchases}");
    SaveStats(); // Make sure this is being called
    UpdateUI();
}
    
    // Show the analytics panel
    public void OpenDashboard()
    {
        RefreshData();
        if (dashboardPanel != null)
            dashboardPanel.SetActive(true);
    }
    
    // Hide the analytics panel
    public void CloseDashboard()
    {
        if (dashboardPanel != null)
            dashboardPanel.SetActive(false);
    }
    
    // Get latest data
    private void RefreshData()
    {
        // Try cloud data first
        if (firestoreManager != null && firestoreManager.IsInitialized)
        {
            firestoreManager.GetGameStats(OnFirestoreDataReceived);
        }
        else
        {
            // Fallback to local data
            LoadStatsFromPlayerPrefs();
            UpdateUI();
        }
    }
    
    // Save data locally and to cloud
private void SaveStats()
{
    // Always save locally first
    SaveStatsToPlayerPrefs();
    
    // Then try to save to cloud
    if (firestoreManager != null && firestoreManager.IsInitialized)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "totalMatches", totalMatches },
            { "totalPurchases", totalPurchases },
            { "lastMatchResult", lastMatchResult },
            { "skinPurchases", skinPurchaseCounts }, // Make sure this works with Firebase
            { "lastUpdated", System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
        };
        
        Debug.Log("Saving stats to Firestore via AnalyticsDashboard");
        firestoreManager.SaveGameStats(data);
    }
    else
    {
        Debug.LogWarning("FirestoreManager not available for saving");
    }
}
    
    // Save data locally
    private void SaveStatsToPlayerPrefs()
    {
        PlayerPrefs.SetInt("TotalMatches", totalMatches);
        PlayerPrefs.SetInt("TotalPurchases", totalPurchases);
        PlayerPrefs.SetString("LastMatchResult", lastMatchResult);
        
        // Save skin purchases data
        string skinData = "";
        foreach (var pair in skinPurchaseCounts)
        {
            skinData += $"{pair.Key}:{pair.Value},";
        }
        PlayerPrefs.SetString("SkinPurchases", skinData);
        
        PlayerPrefs.Save();
    }
    
    // Load data from local storage
    private void LoadStatsFromPlayerPrefs()
    {
        totalMatches = PlayerPrefs.GetInt("TotalMatches", 0);
        totalPurchases = PlayerPrefs.GetInt("TotalPurchases", 0);
        lastMatchResult = PlayerPrefs.GetString("LastMatchResult", "None");
        
        string skinData = PlayerPrefs.GetString("SkinPurchases", "");
        skinPurchaseCounts.Clear();
        
        if (!string.IsNullOrEmpty(skinData))
        {
            string[] entries = skinData.Split(',');
            foreach (string entry in entries)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                
                string[] parts = entry.Split(':');
                if (parts.Length == 2)
                {
                    string skinName = parts[0];
                    if (int.TryParse(parts[1], out int count))
                    {
                        skinPurchaseCounts[skinName] = count;
                    }
                }
            }
        }
    }
    
    // Refresh UI with current data
    private void UpdateUI()
    {
        try
        {
            if (totalMatchesText != null)
                totalMatchesText.text = $"Total Matches: {totalMatches}";
                
            if (totalPurchasesText != null)
                totalPurchasesText.text = $"Total Purchases: {totalPurchases}";
                
            if (topSkinText != null)
            {
                string topSkin = "None";
                int topCount = 0;
                
                foreach (var skin in skinPurchaseCounts)
                {
                    if (skin.Value > topCount)
                    {
                        topSkin = skin.Key;
                        topCount = skin.Value;
                    }
                }
                
                topSkinText.text = $"Most Popular Skin: {topSkin} ({topCount})";
            }
            
            if (lastMatchResultText != null)
                lastMatchResultText.text = $"Last Match: {lastMatchResult}";
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error updating UI: " + e.Message);
        }
    }
}