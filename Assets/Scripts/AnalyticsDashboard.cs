using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityChess;

public class AnalyticsDashboard : MonoBehaviourSingleton<AnalyticsDashboard>
{
    [Header("UI Components")]
    [SerializeField] private GameObject dashboardPanel;
    [SerializeField] public Text totalMatchesText;
    [SerializeField] public Text totalPurchasesText;
    [SerializeField] public Text topSkinText;
    [SerializeField] public Text lastMatchResultText;
    [SerializeField] private Button closeDashboardButton;
    [SerializeField] public Button refreshDataButton;
    
    // Local analytics tracking for demonstration
    // These will be updated by Firestore
    private int totalMatches = 0;
    private int totalPurchases = 0;
    private Dictionary<string, int> skinPurchaseCounts = new Dictionary<string, int>();
    private string lastMatchResult = "None";
    
    private void Start()
    {
        if (closeDashboardButton != null)
            closeDashboardButton.onClick.AddListener(CloseDashboard);
            
        if (refreshDataButton != null)
            refreshDataButton.onClick.AddListener(RefreshData);
            
        if (dashboardPanel != null)
            dashboardPanel.SetActive(false);
            
        // Subscribe to analytics events
        SubscribeToEvents();
    }
    
    private void SubscribeToEvents()
    {
        // Listen for game events to update our dashboard
        GameManager.NewGameStartedEvent += OnMatchStarted;
        GameManager.GameEndedEvent += OnMatchEnded;
    }
    
    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        GameManager.NewGameStartedEvent -= OnMatchStarted;
        GameManager.GameEndedEvent -= OnMatchEnded;
    }
    
    private void OnMatchStarted()
    {
        totalMatches++;
        UpdateUI();
    }
    
    public void OnMatchEnded()
    {
        // Get game result
        if (GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove))
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
        
        UpdateUI();
    }
    
    // This would be called when a skin is purchased
    public void OnSkinPurchased(string skinName)
    {
        totalPurchases++;
        
        // Update skin purchase tracking
        if (!skinPurchaseCounts.ContainsKey(skinName))
            skinPurchaseCounts[skinName] = 0;
            
        skinPurchaseCounts[skinName]++;
        
        UpdateUI();
    }
    
    public void OpenDashboard()
    {
        RefreshData();
        if (dashboardPanel != null)
            dashboardPanel.SetActive(true);
    }
    
    public void CloseDashboard()
    {
        if (dashboardPanel != null)
            dashboardPanel.SetActive(false);
    }
    
    public void RefreshData()
    {
        // Try to get data from FirestoreAnalyticsManager
        var firestoreManager = FirestoreAnalyticsManager.Instance;
        if (firestoreManager != null)
        {
            Debug.Log("Refreshing data from Firestore...");
            firestoreManager.RefreshDashboardData();
        }
        else
        {
            // Fallback to local data if Firestore isn't available
            Debug.Log("FirestoreAnalyticsManager not found, using local data");
            UpdateUI();
        }
    }
    
    // Public methods for FirestoreAnalyticsManager to call
    public void UpdateTotalMatches(int count)
    {
        totalMatches = count;
        if (totalMatchesText != null)
            totalMatchesText.text = $"Total Matches: {totalMatches}";
    }

    public void UpdateTotalPurchases(int count)
    {
        totalPurchases = count;
        if (totalPurchasesText != null)
            totalPurchasesText.text = $"Total Purchases: {totalPurchases}";
    }

    public void UpdateTopSkin(string skinName, int count)
    {
        if (topSkinText != null)
            topSkinText.text = $"Most Popular Skin: {skinName} ({count})";
    }
    
    public void UpdateLastMatchResult(string result)
    {
        lastMatchResult = result;
        if (lastMatchResultText != null)
            lastMatchResultText.text = $"Last Match: {lastMatchResult}";
    }
    
    private void UpdateUI()
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
}