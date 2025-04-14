using UnityEngine;
using Unity.Netcode;
using System;
using UnityChess;

// This class will be attached to GameManager to integrate analytics
public class GameAnalyticsIntegration : MonoBehaviour
{
    private GameManager gameManager;
    private string currentMatchId;
    private bool isMatchInProgress = false;
    private int moveCount = 0;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        
        // Subscribe to game events
        GameManager.NewGameStartedEvent += OnNewGameStarted;
        GameManager.GameEndedEvent += OnGameEnded;
        GameManager.MoveExecutedEvent += OnMoveExecuted;
        
        Debug.Log("Game Analytics Integration initialized");
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        GameManager.NewGameStartedEvent -= OnNewGameStarted;
        GameManager.GameEndedEvent -= OnGameEnded;
        GameManager.MoveExecutedEvent -= OnMoveExecuted;
    }

    private void OnNewGameStarted()
    {
        // Generate a new match ID
        currentMatchId = Guid.NewGuid().ToString();
        moveCount = 0;
        isMatchInProgress = true;
        
        // Determine if this client is hosting
        bool isHosting = false;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            isHosting = NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;
        }
        
        // Log match start event
        // Use FirestoreAnalyticsManager instead of FirebaseAnalyticsManager
        if (FirestoreAnalyticsManager.Instance != null)
        {
            // FirestoreAnalyticsManager handles this through its own event subscriptions
            Debug.Log($"[Analytics] Match started - ID: {currentMatchId}, Hosting: {isHosting}");
        }
    }

    private void OnGameEnded()
    {
        if (!isMatchInProgress) return;
        
        isMatchInProgress = false;
        string result = "unknown";
        string winningSide = null;
        
        // Get the latest move to determine the end condition
        if (gameManager.HalfMoveTimeline.TryGetCurrent(out HalfMove latestMove))
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
        
        // Log match end event - FirestoreAnalyticsManager handles this through its own event subscriptions
        Debug.Log($"[Analytics] Match ended - ID: {currentMatchId}, Result: {result}, Moves: {moveCount}");
    }

    private void OnMoveExecuted()
    {
        moveCount++;
    }
    
    // Call this method to manually log a resignation
    public void LogResignation(Side resigningSide)
    {
        if (!isMatchInProgress) return;
        
        isMatchInProgress = false;
        string winningSide = resigningSide == Side.White ? "Black" : "White";
        
        // Use FirestoreAnalyticsManager instead of FirebaseAnalyticsManager
        Debug.Log($"[Analytics] Player resigned - ID: {currentMatchId}, Resigning: {resigningSide}, Winner: {winningSide}");
    }
}