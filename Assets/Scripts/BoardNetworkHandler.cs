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
        
        Debug.Log($"Client {clientId} connected to server");
        
        // Assign a side to the newly connected client
        AssignPlayerSide(clientId);
        
        // If game is in progress (has moves), send the current state to the client
        if (GameManager.Instance.HalfMoveTimeline.Count > 0)
        {
            Debug.Log($"Game in progress. Syncing state to client {clientId}");
            
            // Get the current game state as a FEN string
            string gameState = GameManager.Instance.SerializeGame();
            
            // Create client RPC params to target only this client
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };
            
            // Send the current game state to this client only
            SendCurrentGameStateClientRpc(gameState, clientRpcParams);
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        
        if (playerSides.ContainsKey(clientId))
        {
            Side disconnectedSide = playerSides[clientId];
            Debug.Log($"Client {clientId} ({disconnectedSide}) disconnected");
            
            // Don't remove from playerSides to preserve assignment for reconnection
            // Instead, mark as disconnected but maintain the side assignment
            Debug.Log($"Preserving player side assignment for reconnection");
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

    [ClientRpc]
    private void SendCurrentGameStateClientRpc(string gameState, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"Received game state from server: {gameState}");
        
        // Load the game state (this uses the FEN string)
        GameManager.Instance.LoadGame(gameState);
        
        // Update turn indicator
        Side currentSideToMove = (Side)currentTurn.Value;
        UIManager.Instance.UpdateTurnIndicator(currentSideToMove);
        
        Debug.Log("Game state successfully synchronized");
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
            return;
        }

        Square fromSquare = new Square(fromFile, fromRank);
        Square toSquare = new Square(toFile, toRank);

        // Use the public wrapper from GameManager to get the legal move
        if (!GameManager.Instance.TryGetLegalMove(fromSquare, toSquare, out Movement move))
        {
            Debug.LogWarning("Invalid move requested: " + fromSquare + " to " + toSquare);
            return;
        }
        
        Debug.Log($"Server executing move from {fromSquare} to {toSquare}");
        
        // Execute the move on the server
        if (GameManager.Instance.TryExecuteMove(move))
        {
            // First destroy any piece at destination
            BoardManager.Instance.TryDestroyVisualPiece(toSquare);
            
            // Then move the piece visually
            GameObject pieceGO = BoardManager.Instance.GetPieceGOAtPosition(fromSquare);
            if (pieceGO != null)
            {
                GameObject destSquareGO = BoardManager.Instance.GetSquareGOByPosition(toSquare);
                pieceGO.transform.SetParent(destSquareGO.transform);
                pieceGO.transform.localPosition = Vector3.zero;
                
                Debug.Log($"Server moved piece from {fromSquare} to {toSquare}");
            }
            
            // Broadcast the move to all clients
            UpdateBoardClientRpc(fromFile, fromRank, toFile, toRank);
            
            // Check for game end conditions
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
        Square fromSquare = new Square(fromFile, fromRank);
        Square toSquare = new Square(toFile, toRank);
        
        Debug.Log($"[ClientRPC] Received move from {fromSquare} to {toSquare} on {(NetworkManager.Singleton.IsHost ? "HOST" : "CLIENT")}");
        
        // Skip move processing for the server that already executed it
        if (NetworkManager.Singleton.IsServer)
        {
            Debug.Log("Server already processed this move, skipping duplicate execution");
            return;
        }
        
        // For clients: 
        // 1. First make sure any piece at destination is destroyed
        BoardManager.Instance.TryDestroyVisualPiece(toSquare);
        
        // 2. Then move the piece from source to destination
        GameObject pieceGO = BoardManager.Instance.GetPieceGOAtPosition(fromSquare);
        if (pieceGO != null)
        {
            // Get the destination square transform
            GameObject destSquareGO = BoardManager.Instance.GetSquareGOByPosition(toSquare);
            
            // Move the piece
            pieceGO.transform.SetParent(destSquareGO.transform);
            pieceGO.transform.localPosition = Vector3.zero;
            
            Debug.Log($"Client moved piece from {fromSquare} to {toSquare}");
        }
        else
        {
            Debug.LogError($"No piece found at source position {fromSquare}");
        }
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
}