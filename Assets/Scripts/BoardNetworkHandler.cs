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
    
    // Track whose turn it is
    private NetworkVariable<int> currentTurn = new NetworkVariable<int>((int)Side.White, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    
    // Track player assignments (white/black)
    private Dictionary<ulong, Side> playerSides = new Dictionary<ulong, Side>();

    private void Awake()
    {
        // Setup singleton
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
            // Server initializes the game
            InitializeGame();
            
            // Listen for player connections
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
    
    // Setup initial game state
    private void InitializeGame()
    {
        if (!IsServer) return;
        
        // Start a new game
        GameManager.Instance.StartNewGame();
        
        // Host is white
        AssignPlayerSide(NetworkManager.Singleton.LocalClientId);
        
        // White goes first
        currentTurn.Value = (int)Side.White;
    }
    
    // Handle new player connection
    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        
        Debug.Log($"Client {clientId} connected to server");
        
        // Assign a side to the new player
        AssignPlayerSide(clientId);
        
        // Send current game state if in progress
        if (GameManager.Instance.HalfMoveTimeline.Count > 0)
        {
            Debug.Log($"Game in progress. Syncing state to client {clientId}");
            
            // Get current game state
            string gameState = GameManager.Instance.SerializeGame();
            
            // Target just this client
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };
            
            // Send the game state
            SendCurrentGameStateClientRpc(gameState, clientRpcParams);
        }
    }
    
    // Handle player disconnect
    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        
        if (playerSides.ContainsKey(clientId))
        {
            Side disconnectedSide = playerSides[clientId];
            Debug.Log($"Client {clientId} ({disconnectedSide}) disconnected");
            
            // Keep side assignment for reconnection
            Debug.Log($"Preserving player side assignment for reconnection");
        }
    }
    
    // Assign player to white or black
    public void AssignPlayerSide(ulong clientId)
    {
        if (!IsServer) return;
        
        // Host is always White, client is Black
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Host is White
            playerSides[clientId] = Side.White;
            Debug.Log($"Host (client {clientId}) assigned as White");
        }
        else
        {
            // Client is Black
            playerSides[clientId] = Side.Black;
            Debug.Log($"Client {clientId} assigned as Black");
        }
        
        // Tell all clients about assignments
        UpdatePlayerAssignmentsClientRpc(
            playerSides.Keys.ToArray(), 
            playerSides.Values.Select(s => (int)s).ToArray());
    }

    // Send current board state to client
    [ClientRpc]
    private void SendCurrentGameStateClientRpc(string gameState, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"Received game state from server: {gameState}");
        
        // Load the game state
        GameManager.Instance.LoadGame(gameState);
        
        // Update turn indicator
        Side currentSideToMove = (Side)currentTurn.Value;
        UIManager.Instance.UpdateTurnIndicator(currentSideToMove);
        
        Debug.Log("Game state successfully synchronized");
    }

    // Process move request from client
    [ServerRpc(RequireOwnership = false)]
    public void RequestMoveServerRpc(int fromFile, int fromRank, int toFile, int toRank, ServerRpcParams rpcParams = default)
    {
        // Get requesting player's ID
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        // Verify it's their turn
        if (!playerSides.TryGetValue(clientId, out Side playerSide) || playerSide != (Side)currentTurn.Value)
        {
            Debug.LogWarning($"Client {clientId} attempted to move out of turn");
            return;
        }

        Square fromSquare = new Square(fromFile, fromRank);
        Square toSquare = new Square(toFile, toRank);

        // Check if move is legal
        if (!GameManager.Instance.TryGetLegalMove(fromSquare, toSquare, out Movement move))
        {
            Debug.LogWarning("Invalid move requested: " + fromSquare + " to " + toSquare);
            return;
        }
        
        Debug.Log($"Server executing move from {fromSquare} to {toSquare}");
        
        // Execute the move on server
        if (GameManager.Instance.TryExecuteMove(move))
        {
            // Remove captured piece if any
            BoardManager.Instance.TryDestroyVisualPiece(toSquare);
            
            // Move the piece visually
            GameObject pieceGO = BoardManager.Instance.GetPieceGOAtPosition(fromSquare);
            if (pieceGO != null)
            {
                GameObject destSquareGO = BoardManager.Instance.GetSquareGOByPosition(toSquare);
                pieceGO.transform.SetParent(destSquareGO.transform);
                pieceGO.transform.localPosition = Vector3.zero;
                
                Debug.Log($"Server moved piece from {fromSquare} to {toSquare}");
            }
            
            // Tell all clients about the move
            UpdateBoardClientRpc(fromFile, fromRank, toFile, toRank);
            
            // Check for game end
            GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
            if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
            {
                // Game ended
                GameEndedClientRpc(latestHalfMove.CausedCheckmate, playerSide == Side.White ? 1 : 0);
            }
            else
            {
                // Switch turns
                currentTurn.Value = currentTurn.Value == (int)Side.White ? (int)Side.Black : (int)Side.White;
                // Tell clients about turn change
                NotifyTurnChangeClientRpc(currentTurn.Value);
            }
        }
    }

    // Update all clients about a move
    [ClientRpc]
    private void UpdateBoardClientRpc(int fromFile, int fromRank, int toFile, int toRank)
    {
        Square fromSquare = new Square(fromFile, fromRank);
        Square toSquare = new Square(toFile, toRank);
        
        Debug.Log($"[ClientRPC] Received move from {fromSquare} to {toSquare} on {(NetworkManager.Singleton.IsHost ? "HOST" : "CLIENT")}");
        
        // Server already did this
        if (NetworkManager.Singleton.IsServer)
        {
            Debug.Log("Server already processed this move, skipping duplicate execution");
            return;
        }
        
        // For clients: 
        // Remove any piece at destination
        BoardManager.Instance.TryDestroyVisualPiece(toSquare);
        
        // Move the piece
        GameObject pieceGO = BoardManager.Instance.GetPieceGOAtPosition(fromSquare);
        if (pieceGO != null)
        {
            // Get destination square
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
    
    // Tell clients whose turn it is
    [ClientRpc]
    private void NotifyTurnChangeClientRpc(int sideToMove)
    {
        Side currentSide = (Side)sideToMove;
        Debug.Log($"It's now {currentSide}'s turn");
        
        // Update UI
        UIManager.Instance.UpdateTurnIndicator(currentSide);
    }
    
    // Tell clients who is playing which side
    [ClientRpc]
    private void UpdatePlayerAssignmentsClientRpc(ulong[] clientIds, int[] sides)
    {
        // Update local records
        for (int i = 0; i < clientIds.Length; i++)
        {
            Debug.Log($"Client {clientIds[i]} is playing as {(Side)sides[i]}");
            // Could update UI here
        }
        
        // Find local player's side
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
    
    // Tell clients the game is over
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
        
        // Disable pieces to prevent more moves
        BoardManager.Instance.SetActiveAllPieces(false);
        
        // Show restart option
        UIManager.Instance.ShowRestartButton(true);
    }

    // Handle player resigning
    [ServerRpc(RequireOwnership = false)]
    public void ResignGameServerRpc(ServerRpcParams rpcParams = default)
    {
        // Get player ID
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        if (!playerSides.TryGetValue(clientId, out Side resigningSide))
        {
            Debug.LogWarning($"Client {clientId} tried to resign but isn't assigned a side");
            return;
        }
        
        Debug.Log($"Player {clientId} ({resigningSide}) is resigning");
        
        // Other player wins
        Side winningSide = resigningSide == Side.White ? Side.Black : Side.White;
        
        // Tell clients about resignation
        GameResignedClientRpc((int)resigningSide, (int)winningSide);
    }

    // Tell clients about resignation
    [ClientRpc]
    private void GameResignedClientRpc(int resigningSideValue, int winningSideValue)
    {
        Side resigningSide = (Side)resigningSideValue;
        Side winningSide = (Side)winningSideValue;
        
        // Show message
        UIManager.Instance.DisplayGameOverMessage($"{resigningSide} resigned. {winningSide} wins!");
        
        // Disable pieces
        BoardManager.Instance.SetActiveAllPieces(false);
        
        // Show restart option
        UIManager.Instance.ShowRestartButton(true);
    }

    // Handle restart request
    [ServerRpc(RequireOwnership = false)]
    public void RequestRestartServerRpc(ServerRpcParams rpcParams = default)
    {
        // Could add confirmation logic here
        RestartGameServerRpc();
    }

    // Actually restart the game
    [ServerRpc]
    public void RestartGameServerRpc()
    {
        if (!IsServer) return;
        
        Debug.Log("Restarting game for all clients");
        
        // Reset game
        GameManager.Instance.StartNewGame();
        
        // White goes first
        currentTurn.Value = (int)Side.White;
        
        // Tell clients to restart
        RestartGameClientRpc();
    }

    // Tell clients to restart
    [ClientRpc]
    private void RestartGameClientRpc()
    {
        Debug.Log("Received game restart command");
        
        // Reset game
        GameManager.Instance.StartNewGame();
        
        // Reset UI
        UIManager.Instance.DisplayGameOverMessage(""); // Clear message
        UIManager.Instance.ShowRestartButton(false);
        UIManager.Instance.UpdateTurnIndicator(Side.White);
        
        // Enable appropriate pieces
        if (NetworkManager.Singleton.IsHost)
        {
            // Host is White
            BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(Side.White);
        }
        else
        {
            // Client is Black, check if it's their turn
            Side localPlayerSide = Side.Black; // Assume client is Black
            if (localPlayerSide == (Side)currentTurn.Value)
            {
                BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(localPlayerSide);
            }
        }
    }
    
    // Tell other players about skin change
    [ServerRpc(RequireOwnership = false)]
    public void NotifySkinEquippedServerRpc(string skinName, string playerSide, ServerRpcParams rpcParams = default)
    {
        // Get player ID
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        // Parse side
        Side playerSideEnum = Side.White; // Default
        if (playerSide == "Black")
            playerSideEnum = Side.Black;
        
        // Tell all clients
        NotifySkinEquippedClientRpc(skinName, playerSideEnum == Side.White ? 0 : 1, clientId);
    }

    // Show skin notification
    [ClientRpc]
    public void NotifySkinEquippedClientRpc(string skinName, int playerSideValue, ulong clientId)
    {
        Side playerSide = (Side)playerSideValue;
        
        // Don't show to player who equipped it
        if (clientId == NetworkManager.Singleton.LocalClientId)
            return;
        
        // Show notification
        DLCStoreManager dlcManager = DLCStoreManager.Instance;
        if (dlcManager != null)
        {
            string playerLabel = playerSide == Side.White ? "White player" : "Black player";
            dlcManager.ShowSkinNotification($"{playerLabel} equipped {skinName} skin!");
        }
    }
}