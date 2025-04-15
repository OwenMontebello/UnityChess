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
        // Get manager reference
        gameManager = GetComponent<GameManager>();
        
        // Listen for game events
        GameManager.NewGameStartedEvent += OnNewGameStarted;
        GameManager.GameEndedEvent += OnGameEnded;
        GameManager.MoveExecutedEvent += OnMoveExecuted;
        
        Debug.Log("Game Analytics Integration initialized");
    }
    
    private void OnDestroy()
    {
        // Cleanup event listeners
        GameManager.NewGameStartedEvent -= OnNewGameStarted;
        GameManager.GameEndedEvent -= OnGameEnded;
        GameManager.MoveExecutedEvent -= OnMoveExecuted;
    }

    // Track game start
    private void OnNewGameStarted()
    {
        // Generate unique match ID
        currentMatchId = Guid.NewGuid().ToString();
        moveCount = 0;
        isMatchInProgress = true;
        
        // Check if hosting
        bool isHosting = false;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            isHosting = NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;
        }
        
        // Log match start
        FirebaseAnalyticsManager.Instance.LogMatchStart(isHosting, currentMatchId);
        Debug.Log($"[Analytics] Match started - ID: {currentMatchId}, Hosting: {isHosting}");
    }

    // Track game end
    private void OnGameEnded()
    {
        if (!isMatchInProgress) return;
        
        isMatchInProgress = false;
        string result = "unknown";
        string winningSide = null;
        
        // Get game result
        if (gameManager.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove))
        {
            if (latestHalfMove.CausedCheckmate)
            {
                result = "checkmate";
                winningSide = latestHalfMove.Piece.Owner.ToString();
            }
            else if (latestHalfMove.CausedStalemate)
            {
                result = "stalemate";
            }
        }
        
        // Log match end
        FirebaseAnalyticsManager.Instance.LogMatchEnd(currentMatchId, result, moveCount, winningSide);
        Debug.Log($"[Analytics] Match ended - ID: {currentMatchId}, Result: {result}, Moves: {moveCount}");
    }

    // Count moves
    private void OnMoveExecuted()
    {
        moveCount++;
    }
    
    // Track player resignation
    public void LogResignation(Side resigningSide)
    {
        if (!isMatchInProgress) return;
        
        isMatchInProgress = false;
        string winningSide = resigningSide == Side.White ? "Black" : "White";
        
        FirebaseAnalyticsManager.Instance.LogMatchEnd(
            currentMatchId, 
            "resignation", 
            moveCount, 
            winningSide);
            
        Debug.Log($"[Analytics] Player resigned - ID: {currentMatchId}, Resigning: {resigningSide}, Winner: {winningSide}");
    }
}