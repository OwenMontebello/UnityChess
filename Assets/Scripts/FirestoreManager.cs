using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

#if ENABLE_FIREBASE
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
#endif

public class FirestoreManager : MonoBehaviour
{
    public bool IsInitialized { get; private set; }
    
    // Debug options
    [SerializeField] private bool showDebugUI = true;
    
    // Callback for async operations
    public delegate void FirestoreCallback(Dictionary<string, object> data, bool success);
    
#if ENABLE_FIREBASE
    private FirebaseFirestore db;
    private DocumentReference statsRef;
#endif
    
    private void Awake()
    {
        // Keep between scenes
        DontDestroyOnLoad(gameObject);
        StartCoroutine(InitializeFirestore());
    }
    
    // Show debug controls in game
    private void OnGUI()
    {
        if (!showDebugUI) return;
        
        GUILayout.BeginArea(new Rect(10, Screen.height - 150, 300, 140));
        GUILayout.Box($"Firestore Status: {(IsInitialized ? "Connected" : "Not Connected")}");
        
        if (GUILayout.Button("Force Initialize"))
        {
            StartCoroutine(InitializeFirestore());
        }
        
        if (GUILayout.Button("Send Test Data"))
        {
            Dictionary<string, object> testData = new Dictionary<string, object>
            {
                { "totalMatches", 5 },
                { "totalPurchases", 3 },
                { "lastMatchResult", "Test Result" },
                { "skinPurchases", new Dictionary<string, object> { { "TestSkin", 2 } } },
                { "lastUpdated", DateTime.UtcNow.ToString() }
            };
            
            SaveGameStats(testData);
            Debug.Log("Test data sent to Firestore");
        }
        
        if (GUILayout.Button("Test Data Retrieval"))
        {
            GetGameStats((data, success) => {
                if (success)
                {
                    Debug.Log("Data retrieved successfully: " + string.Join(", ", data.Keys));
                    foreach (var key in data.Keys)
                    {
                        Debug.Log($"{key}: {data[key]}");
                    }
                }
                else
                {
                    Debug.LogError("Failed to retrieve data");
                }
            });
        }
        
        GUILayout.EndArea();
    }
    
    // Connect to Firestore
    private IEnumerator InitializeFirestore()
    {
        IsInitialized = false;
        
#if ENABLE_FIREBASE
        Debug.Log("Starting Firestore initialization");
        
        yield return new WaitForSeconds(1f);
        
        // Check Firebase availability
        if (FirebaseApp.DefaultInstance == null)
        {
            Debug.Log("Firebase App not initialized. Attempting to initialize...");
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
                var dependencyStatus = task.Result;
                if (dependencyStatus == DependencyStatus.Available)
                {
                    Debug.Log("Firebase dependencies available");
                }
                else
                {
                    Debug.LogError($"Firebase dependencies not available: {dependencyStatus}");
                }
            });
            
            yield return new WaitForSeconds(2f);
        }
        
        if (FirebaseApp.DefaultInstance != null)
        {
            Debug.Log("Firebase App initialized. Getting Firestore...");
            try
            {
                db = FirebaseFirestore.DefaultInstance;
                
                if (db != null)
                {
                    Debug.Log("Firestore instance obtained. Creating reference...");
                    statsRef = db.Collection("gameStats").Document("globalStats");
                    IsInitialized = true;
                    Debug.Log("Firestore fully initialized!");
                    
                    // Create document if missing
                    EnsureDocumentExists();
                }
                else
                {
                    Debug.LogError("Failed to get Firestore instance");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during Firestore initialization: {e.Message}\n{e.StackTrace}");
            }
        }
        else
        {
            Debug.LogError("Firebase App still not initialized after waiting");
        }
#else
        Debug.LogWarning("Firebase is not enabled in this build");
        yield return null;
#endif
    }
    
#if ENABLE_FIREBASE
    // Create initial document if needed
    private void EnsureDocumentExists()
    {
        statsRef.GetSnapshotAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                Debug.LogError("Error checking document: " + task.Exception);
                return;
            }
            
            DocumentSnapshot snapshot = task.Result;
            if (!snapshot.Exists)
            {
                Debug.Log("Document doesn't exist. Creating initial document...");
                Dictionary<string, object> initialData = new Dictionary<string, object>
                {
                    { "totalMatches", 0 },
                    { "totalPurchases", 0 },
                    { "lastMatchResult", "None" },
                    { "skinPurchases", new Dictionary<string, object>() },
                    { "created", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
                };
                
                statsRef.SetAsync(initialData).ContinueWithOnMainThread(setTask => {
                    if (setTask.IsFaulted)
                    {
                        Debug.LogError("Failed to create initial document: " + setTask.Exception);
                    }
                    else
                    {
                        Debug.Log("Initial document created successfully");
                    }
                });
            }
            else
            {
                Debug.Log("Document already exists");
            }
        });
    }
#endif
    
    // Load game stats from cloud
    public void GetGameStats(FirestoreCallback callback)
    {
#if ENABLE_FIREBASE
        if (!IsInitialized || statsRef == null)
        {
            Debug.LogWarning("Firestore not initialized, cannot get stats");
            callback?.Invoke(null, false);
            return;
        }
        
        Debug.Log("Attempting to get game stats from Firestore");
        try
        {
            statsRef.GetSnapshotAsync().ContinueWithOnMainThread(task => 
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("Error getting stats: " + task.Exception);
                    callback?.Invoke(null, false);
                    return;
                }
                
                DocumentSnapshot snapshot = task.Result;
                if (snapshot.Exists)
                {
                    Dictionary<string, object> data = snapshot.ToDictionary();
                    Debug.Log($"Retrieved document with {data.Count} fields");
                    callback?.Invoke(data, true);
                }
                else
                {
                    Debug.Log("No document found, creating one");
                    EnsureDocumentExists();
                    callback?.Invoke(new Dictionary<string, object>(), false);
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception getting game stats: {e.Message}\n{e.StackTrace}");
            callback?.Invoke(null, false);
        }
#else
        Debug.LogWarning("Firebase is not enabled, cannot get game stats");
        callback?.Invoke(null, false);
#endif
    }
    
    // Save game stats to cloud
    public void SaveGameStats(Dictionary<string, object> data)
    {
#if ENABLE_FIREBASE
        if (!IsInitialized || statsRef == null)
        {
            Debug.LogWarning("Firestore not initialized, cannot save game stats");
            return;
        }
        
        Debug.Log($"Saving data to Firestore with {data.Count} fields");
        foreach (var key in data.Keys)
        {
            Debug.Log($"  Field: {key}, Value: {data[key]}");
        }
        
        try
        {
            statsRef.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task => 
            {
                if (task.IsFaulted)
                {
                    Debug.LogError($"Error saving stats: {task.Exception}");
                    return;
                }
                
                Debug.Log("Successfully saved stats to Firestore");
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception saving game stats: {e.Message}\n{e.StackTrace}");
        }
#else
        Debug.LogWarning("Firebase is not enabled, cannot save game stats");
#endif
    }
}