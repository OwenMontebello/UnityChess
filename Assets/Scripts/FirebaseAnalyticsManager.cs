using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using UnityChess;

public class FirestoreAnalyticsManager : MonoBehaviourSingleton<FirestoreAnalyticsManager>
{
    // Firestore database instance
    private FirebaseFirestore db;
    // Flag to check if Firebase is initialized
    private bool isInitialized = false;
    
    // Collection names in Firestore
    private const string MATCHES_COLLECTION = "matches";
    private const string PURCHASES_COLLECTION = "purchases";
    
    // Device ID for identifying the current user
    private string deviceId;
    
    // Reference to the analytics dashboard
    private AnalyticsDashboard dashboard;
    
    private void Start()
    {
        deviceId = SystemInfo.deviceUniqueIdentifier;
        
        // Find the analytics dashboard
        dashboard = FindObjectOfType<AnalyticsDashboard>();
        if (dashboard == null)
        {
            Debug.LogWarning("AnalyticsDashboard not found in scene");
        }
        
        InitializeFirebase();
    }
    
    private void InitializeFirebase()
    {
        Debug.Log("Initializing Firebase Firestore...");
        
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                // Initialize Firestore
                db = FirebaseFirestore.DefaultInstance;
                isInitialized = true;
                Debug.Log("Firebase Firestore initialized successfully!");
                
                // Subscribe to game events to track data
                SubscribeToGameEvents();
            }
            else
            {
                Debug.LogError($"Could not resolve Firebase dependencies: {dependencyStatus}");
            }
        });
    }
    
    private void SubscribeToGameEvents()
    {
        // Subscribe to game events to automatically log data
        GameManager.NewGameStartedEvent += OnMatchStarted;
        GameManager.GameEndedEvent += OnMatchEnded;
        
        // Try to find DLCAnalyticsIntegration and subscribe to its events
        var dlcAnalytics = FindObjectOfType<DLCAnalyticsIntegration>();
        if (dlcAnalytics != null)
        {
            dlcAnalytics.LogSkinPurchase += OnSkinPurchased;
            Debug.Log("Successfully subscribed to DLCAnalyticsIntegration events");
        }
        else
        {
            Debug.LogWarning("DLCAnalyticsIntegration not found");
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (GameManager.Instance != null)
        {
            GameManager.NewGameStartedEvent -= OnMatchStarted;
            GameManager.GameEndedEvent -= OnMatchEnded;
        }
        
        var dlcAnalytics = FindObjectOfType<DLCAnalyticsIntegration>();
        if (dlcAnalytics != null)
        {
            dlcAnalytics.LogSkinPurchase -= OnSkinPurchased;
        }
    }
    
    #region Event Logging
    
    private string currentMatchId;
    private DateTime matchStartTime;
    
    private void OnMatchStarted()
    {
        if (!isInitialized) return;
        
        // Generate a unique match ID and record start time
        currentMatchId = Guid.NewGuid().ToString();
        matchStartTime = DateTime.UtcNow;
        
        // Create match start data
        Dictionary<string, object> matchData = new Dictionary<string, object>
        {
            { "matchId", currentMatchId },
            { "playerId", deviceId },
            { "startTime", matchStartTime },
            { "status", "in_progress" },
            { "isNetworkGame", Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening },
            { "playerSide", GameManager.Instance.StartingSide.ToString() }
        };
        
        // Add to Firestore
        db.Collection(MATCHES_COLLECTION).Document(currentMatchId)
            .SetAsync(matchData)
            .ContinueWithOnMainThread(task => {
                if (task.IsCompleted)
                    Debug.Log($"Match {currentMatchId} start data saved to Firestore");
                else if (task.IsFaulted)
                    Debug.LogError($"Error saving match data: {task.Exception}");
            });
        
        Debug.Log($"Match started: {currentMatchId}");
    }
    
    private void OnMatchEnded()
    {
        if (!isInitialized || string.IsNullOrEmpty(currentMatchId)) return;
        
        string result = "unknown";
        string winningSide = null;
        
        // Determine the result
        if (GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestMove))
        {
            if (latestMove.CausedCheckmate)
            {
                result = "checkmate";
                winningSide = latestMove.Piece.Owner.ToString();
            }
            else if (latestMove.CausedStalemate)
            {
                result = "stalemate";
            }
        }
        
        // Calculate match duration
        TimeSpan duration = DateTime.UtcNow - matchStartTime;
        
        // Final match data
        Dictionary<string, object> endMatchData = new Dictionary<string, object>
        {
            { "status", "completed" },
            { "endTime", DateTime.UtcNow },
            { "durationSeconds", duration.TotalSeconds },
            { "result", result },
            { "moveCount", GameManager.Instance.LatestHalfMoveIndex }
        };
        
        if (winningSide != null)
        {
            endMatchData.Add("winningSide", winningSide);
        }
        
        // Update match record
        db.Collection(MATCHES_COLLECTION).Document(currentMatchId)
            .UpdateAsync(endMatchData)
            .ContinueWithOnMainThread(task => {
                if (task.IsCompleted)
                    Debug.Log($"Match {currentMatchId} end data saved to Firestore");
                else if (task.IsFaulted)
                    Debug.LogError($"Error updating match end data: {task.Exception}");
            });
        
        Debug.Log($"Match ended: {currentMatchId}, Result: {result}, Winner: {winningSide}");
        
        // Update the last match result
        string resultText = result == "checkmate" 
            ? $"{winningSide} won by checkmate" 
            : (result == "stalemate" ? "Draw by stalemate" : "Unknown result");
            
        // Store the last match result for the dashboard to access
        if (dashboard != null)
        {
            // Use a method instead of directly accessing fields
            dashboard.OnMatchEnded(); // This will update the dashboard's internal data
        }
        
        // Reset match tracking data
        currentMatchId = null;
    }
    
    private void OnSkinPurchased(string skinId, string skinName, int price)
    {
        if (!isInitialized) return;
        
        string purchaseId = Guid.NewGuid().ToString();
        
        Dictionary<string, object> purchaseData = new Dictionary<string, object>
        {
            { "purchaseId", purchaseId },
            { "playerId", deviceId },
            { "skinId", skinId },
            { "skinName", skinName },
            { "price", price },
            { "timestamp", DateTime.UtcNow }
        };
        
        db.Collection(PURCHASES_COLLECTION).Document(purchaseId)
            .SetAsync(purchaseData)
            .ContinueWithOnMainThread(task => {
                if (task.IsCompleted)
                {
                    Debug.Log($"Purchase of {skinName} recorded in Firestore");
                    
                    // Notify the dashboard about the new purchase
                    if (dashboard != null)
                    {
                        dashboard.OnSkinPurchased(skinName);
                    }
                }
                else if (task.IsFaulted)
                {
                    Debug.LogError($"Error recording purchase: {task.Exception}");
                }
            });
    }
    
    #endregion
    
    #region Analytics Data Retrieval
    
    public void RefreshDashboardData()
    {
        if (!isInitialized || dashboard == null) return;
        
        Debug.Log("Refreshing analytics data from Firestore...");
        
        // Retrieve total match count
        db.Collection(MATCHES_COLLECTION)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task => {
                if (task.IsCompleted)
                {
                    int totalMatches = task.Result.Count;
                    Debug.Log($"Found {totalMatches} total matches in Firestore");
                    
                    // Update the dashboard through a public method
                    dashboard.UpdateTotalMatches(totalMatches);
                }
                else if (task.IsFaulted)
                {
                    Debug.LogError($"Error retrieving match data: {task.Exception}");
                }
            });
        
        // Retrieve total purchase count
        db.Collection(PURCHASES_COLLECTION)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task => {
                if (task.IsCompleted)
                {
                    int totalPurchases = task.Result.Count;
                    Debug.Log($"Found {totalPurchases} total purchases in Firestore");
                    
                    // Update the dashboard through a public method
                    dashboard.UpdateTotalPurchases(totalPurchases);
                }
                else if (task.IsFaulted)
                {
                    Debug.LogError($"Error retrieving purchase data: {task.Exception}");
                }
            });
        
        // Find most popular skin
        db.Collection(PURCHASES_COLLECTION)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task => {
                if (task.IsCompleted)
                {
                    Dictionary<string, int> skinCounts = new Dictionary<string, int>();
                    
                    foreach (DocumentSnapshot doc in task.Result.Documents)
                    {
                        if (doc.TryGetValue("skinName", out object skinNameObj) && skinNameObj is string skinName)
                        {
                            if (!skinCounts.ContainsKey(skinName))
                                skinCounts[skinName] = 0;
                            
                            skinCounts[skinName]++;
                        }
                    }
                    
                    // Find most popular skin
                    string topSkin = "None";
                    int topCount = 0;
                    
                    foreach (var pair in skinCounts)
                    {
                        if (pair.Value > topCount)
                        {
                            topSkin = pair.Key;
                            topCount = pair.Value;
                        }
                    }
                    
                    Debug.Log($"Most popular skin: {topSkin} with {topCount} purchases");
                    
                    // Update the dashboard through a public method
                    dashboard.UpdateTopSkin(topSkin, topCount);
                }
                else if (task.IsFaulted)
                {
                    Debug.LogError($"Error retrieving skin popularity data: {task.Exception}");
                }
            });
    }
    
    #endregion
}