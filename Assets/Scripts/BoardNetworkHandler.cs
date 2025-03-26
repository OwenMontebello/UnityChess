using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using UnityChess; // Ensure this matches the namespace where Square, Movement, etc., are defined.

public class BoardNetworkHandler : NetworkBehaviour
{
    public static BoardNetworkHandler Instance;
    private BoardManager boardManager;
    
    // Track current turn with a NetworkVariable
    private NetworkVariable<int> currentTurn = new NetworkVariable<int>((int)Side.White, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    // Dictionary to track which player is playing as which side
    private Dictionary<ulong, Side> playerSides = new Dictionary<ulong, Side>();

    private void Awake()
    {
        // Set up a singleton reference.
        Instance = this;
        boardManager = GetComponent<BoardManager>();
        if (boardManager == null)
        {
            Debug.LogError("BoardNetworkHandler: No BoardManager component found on this GameObject!");
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsServer)
        {
            // If we're the server, initialize the game
            InitializeGame();
            
            // Register for client connection events
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }
    
    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
    
    private void InitializeGame()
    {
        if (!IsServer) return;
        
        // Reset the game to starting position
        GameManager.Instance.StartNewGame();
        
        // Assign the host as white initially
        AssignPlayerSide(NetworkManager.Singleton.LocalClientId);
        
        // Set initial turn to white
        currentTurn.Value = (int)Side.White;
    }
    
    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        
        // Assign a side to the newly connected client
        AssignPlayerSide(clientId);
        
        // Update client with current game state
        SyncGameStateToClientRpc(clientId);
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        
        if (playerSides.ContainsKey(clientId))
        {
            Debug.Log($"Client {clientId} ({playerSides[clientId]}) disconnected");
            // Remove from playerSides but don't reassign - allow for rejoining
            // You could add reconnection logic here
        }
    }
    
    public void AssignPlayerSide(ulong clientId)
{
    if (!IsServer) return;
    
    // Very important: Clear existing assignment logic and explicitly make host White and client Black
    if (clientId == NetworkManager.Singleton.LocalClientId)
    {
        // Host is always White
        playerSides[clientId] = Side.White;
        Debug.Log($"Host (client {clientId}) assigned as White");
    }
    else
    {
        // Any other client is Black
        playerSides[clientId] = Side.Black;
        Debug.Log($"Client {clientId} assigned as Black");
    }
    
    // Notify all clients about player assignments
    UpdatePlayerAssignmentsClientRpc(
        playerSides.Keys.ToArray(), 
        playerSides.Values.Select(s => (int)s).ToArray());
}

    /// <summary>
    /// Called by a client when a piece is moved.
    /// Runs on the server (host) when invoked by a client.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestMoveServerRpc(int fromFile, int fromRank, int toFile, int toRank, ServerRpcParams rpcParams = default)
    {
        // Get the client ID of the player making the request
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        // Verify it's this player's turn
        if (!playerSides.TryGetValue(clientId, out Side playerSide) || playerSide != (Side)currentTurn.Value)
        {
            Debug.LogWarning($"Client {clientId} attempted to move out of turn");
            // Could send feedback to the client here
            return;
        }
    
        // Convert coordinates into Square objects.
        Square fromSquare = new Square(fromFile, fromRank);
        Square toSquare = new Square(toFile, toRank);

        // Use the public wrapper from GameManager.
        if (!GameManager.Instance.TryGetLegalMove(fromSquare, toSquare, out Movement move))
        {
            Debug.LogWarning("Invalid move requested: " + fromSquare + " to " + toSquare);
            // Could send feedback to the client here
            return;
        }
        
        // Execute the move on the server.
        if (GameManager.Instance.TryExecuteMove(move))
        {
            // Update board visuals on the server.
            boardManager.MovePiece(fromSquare, toSquare);
            // Broadcast the move to all clients.
            UpdateBoardClientRpc(fromFile, fromRank, toFile, toRank);
            
            // Check if the game is over after this move
            GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
            if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
            {
                // Game ended, notify clients
                GameEndedClientRpc(latestHalfMove.CausedCheckmate, playerSide == Side.White ? 1 : 0);
            }
            else
            {
                // Switch turns after a successful move
                currentTurn.Value = currentTurn.Value == (int)Side.White ? (int)Side.Black : (int)Side.White;
                // Notify clients about turn change
                NotifyTurnChangeClientRpc(currentTurn.Value);
            }
        }
    }

    /// <summary>
    /// Called on all clients to update board visuals.
    /// </summary>
    [ClientRpc]
    private void UpdateBoardClientRpc(int fromFile, int fromRank, int toFile, int toRank)
    {
        Debug.Log($"[ClientRPC] Received move from {fromFile},{fromRank} to {toFile},{toRank} on client {NetworkManager.Singleton.LocalClientId}");
        Square fromSquare = new Square(fromFile, fromRank);
        Square toSquare = new Square(toFile, toRank);
        BoardManager.Instance.MovePiece(fromSquare, toSquare);
    }
    
    [ClientRpc]
    private void NotifyTurnChangeClientRpc(int sideToMove)
    {
        Side currentSide = (Side)sideToMove;
        Debug.Log($"It's now {currentSide}'s turn");
        
        // Update UI
        UIManager.Instance.UpdateTurnIndicator(currentSide);
    }
    
    [ClientRpc]
    private void UpdatePlayerAssignmentsClientRpc(ulong[] clientIds, int[] sides)
    {
        // Update local player assignments
        for (int i = 0; i < clientIds.Length; i++)
        {
            Debug.Log($"Client {clientIds[i]} is playing as {(Side)sides[i]}");
            // Optional: Update UI to show player roles
        }
        
        // Determine local player's side
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        for (int i = 0; i < clientIds.Length; i++)
        {
            if (clientIds[i] == localClientId)
            {
                UIManager.Instance.SetLocalPlayerSide((Side)sides[i]);
                break;
            }
        }
    }
    
    [ClientRpc]
    private void GameEndedClientRpc(bool isCheckmate, int winningSideValue)
    {
        if (isCheckmate)
        {
            Side winningSide = (Side)winningSideValue;
            UIManager.Instance.DisplayGameOverMessage($"{winningSide} wins by checkmate!");
        }
        else
        {
            UIManager.Instance.DisplayGameOverMessage("Game ended in a draw (stalemate)");
        }
    }
    
    [ClientRpc]
    private void SyncGameStateToClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;
        
        // Initialize the client with current game state
        Debug.Log("Received current game state from server");
        
        // Update turn indicator
        UIManager.Instance.UpdateTurnIndicator((Side)currentTurn.Value);
    }
}