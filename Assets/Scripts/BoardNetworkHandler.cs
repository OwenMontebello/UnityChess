using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityChess;

public class BoardNetworkHandler : NetworkBehaviour
{
    public static BoardNetworkHandler Instance;
    private BoardManager boardManager;

    private void Awake()
    {
        // Set up a singleton reference (for simplicity)
        Instance = this;
        boardManager = GetComponent<BoardManager>();
        if (boardManager == null)
        {
            Debug.LogError("BoardNetworkHandler: No BoardManager component found on this GameObject!");
        }
    }

    /// <summary>
    /// Called by a client when a piece is moved.
    /// This method runs on the server (host) when invoked by a client.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestMoveServerRpc(int fromFile, int fromRank, int toFile, int toRank)
    {
        // Convert coordinates into your Square type.
        Square fromSquare = new Square(fromFile, fromRank);
        Square toSquare = new Square(toFile, toRank);

        // Validate the move using your game logic.
        if (!GameManager.Instance.CurrentBoard.TryGetLegalMove(fromSquare, toSquare, out Movement move))
        {
            Debug.LogWarning("Invalid move requested: " + fromSquare + " to " + toSquare);
            return;
        }
        
        // Execute the move on the server (updates game state).
        if (GameManager.Instance.TryExecuteMove(move))
        {
            // Update the board visuals on the server.
            boardManager.MovePiece(fromSquare, toSquare);
            // Broadcast the move to all clients so they update their visuals.
            UpdateBoardClientRpc(fromFile, fromRank, toFile, toRank);
        }
    }

    /// <summary>
    /// Called on all clients to update the board visuals.
    /// </summary>
    [ClientRpc]
    private void UpdateBoardClientRpc(int fromFile, int fromRank, int toFile, int toRank)
    {
        Square fromSquare = new Square(fromFile, fromRank);
        Square toSquare = new Square(toFile, toRank);
        // Update the board visuals on this client.
        BoardManager.Instance.MovePiece(fromSquare, toSquare);
    }
}

