using UnityEngine;
using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using System;

public class FirebaseAnalyticsManager : MonoBehaviourSingleton<FirebaseAnalyticsManager>
{
    // Settings
    [Header("Analytics Configuration")]
    [SerializeField] private bool enableAnalytics = true;
    [SerializeField] private bool logDebugMessages = true;

    // Status tracking
    private bool isInitialized = false;
    
    private void Start()
    {
        InitializeFirebase();
    }

    // Setup Firebase connection
    private void InitializeFirebase()
    {
        Debug.Log("Initializing Firebase Analytics...");

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                // Firebase ready
                isInitialized = true;
                
                // Configure analytics
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(enableAnalytics);
                
                // Set basic user data
                FirebaseAnalytics.SetUserId(SystemInfo.deviceUniqueIdentifier);
                FirebaseAnalytics.SetUserProperty("device_model", SystemInfo.deviceModel);
                FirebaseAnalytics.SetUserProperty("app_version", Application.version);
                
                Debug.Log("Firebase Analytics initialized successfully!");

                // Log app start
                LogEvent("app_start");
            }
            else
            {
                Debug.LogError($"Could not resolve Firebase dependencies: {dependencyStatus}");
            }
        });
    }

    // Send custom event to Firebase
    public void LogEvent(string eventName, Dictionary<string, object> parameters = null)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Firebase Analytics not initialized. Event not logged: " + eventName);
            return;
        }

        if (parameters == null)
        {
            if (logDebugMessages)
                Debug.Log($"[Analytics] Logging event: {eventName}");
                
            FirebaseAnalytics.LogEvent(eventName);
        }
        else
        {
            // Convert to Firebase format
            List<Parameter> firebaseParams = new List<Parameter>();
            foreach (var param in parameters)
            {
                if (param.Value is string stringValue)
                    firebaseParams.Add(new Parameter(param.Key, stringValue));
                else if (param.Value is double doubleValue)
                    firebaseParams.Add(new Parameter(param.Key, doubleValue));
                else if (param.Value is long longValue)
                    firebaseParams.Add(new Parameter(param.Key, longValue));
                else if (param.Value is int intValue)
                    firebaseParams.Add(new Parameter(param.Key, (long)intValue));
                else if (param.Value is float floatValue)
                    firebaseParams.Add(new Parameter(param.Key, (double)floatValue));
                else if (param.Value is bool boolValue)
                    firebaseParams.Add(new Parameter(param.Key, boolValue ? "true" : "false"));
                else
                    firebaseParams.Add(new Parameter(param.Key, param.Value.ToString()));
            }

            if (logDebugMessages)
            {
                string paramsString = "";
                foreach (var param in parameters)
                {
                    paramsString += $"{param.Key}={param.Value}, ";
                }
                Debug.Log($"[Analytics] Logging event: {eventName} with params: {paramsString}");
            }

            FirebaseAnalytics.LogEvent(eventName, firebaseParams.ToArray());
        }
    }

    // Track match start
    public void LogMatchStart(bool isHosting, string matchId = null)
    {
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            { "match_id", matchId ?? System.Guid.NewGuid().ToString() },
            { "is_hosting", isHosting },
            { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
        };

        LogEvent("match_start", parameters);
    }

    // Track match end
    public void LogMatchEnd(string matchId, string result, int moveCount, string winningSide = null)
    {
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            { "match_id", matchId },
            { "result", result }, // "checkmate", "stalemate", "resignation", "disconnect"
            { "move_count", moveCount },
            { "duration_seconds", Time.time }, // Approximate match duration
            { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
        };

        if (!string.IsNullOrEmpty(winningSide))
        {
            parameters.Add("winning_side", winningSide);
        }

        LogEvent("match_end", parameters);
    }

    // Track skin purchase
    public void LogSkinPurchase(string skinId, string skinName, int price)
    {
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            { "skin_id", skinId },
            { "skin_name", skinName },
            { "price", price },
            { "currency", "credits" },
            { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
        };

        LogEvent("skin_purchase", parameters);
    }

    // Track skin equipped
    public void LogSkinEquipped(string skinId, string skinName)
    {
        Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            { "skin_id", skinId },
            { "skin_name", skinName },
            { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
        };

        LogEvent("skin_equipped", parameters);
    }

    // Log app exit
    public void OnApplicationQuit()
    {
        if (isInitialized)
        {
            LogEvent("app_close");
        }
    }
}