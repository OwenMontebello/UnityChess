using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Firebase.Analytics;
using System;
using UnityChess;

public class AnalyticsDashboard : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private GameObject dashboardPanel;
    [SerializeField] private Text totalMatchesText;
    [SerializeField] private Text totalPurchasesText;
    [SerializeField] private Text topSkinText;
    [SerializeField] private Text lastMatchResultText;
    [SerializeField] private Button closeDashboardButton;
    [SerializeField] private Button refreshDataButton;
    
    // Local analytics tracking for demonstration
    // In a real implementation, you would fetch this data from Firebase
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
    
    private void OnMatchEnded()
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
    
    // This would be called by DLCStoreManager when a purchase is made
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
    
    private void RefreshData()
    {
        // In a real implementation, you would fetch this data from Firebase
        // For this assignment, we'll use our locally tracked data
        
        Debug.Log("Refreshing analytics dashboard data");
        UpdateUI();
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