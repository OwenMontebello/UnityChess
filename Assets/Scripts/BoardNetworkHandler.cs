using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityChess; // Ensure this matches the namespace where Square, Movement, etc., are defined.

public class BoardNetworkHandler : NetworkBehaviour
{
    public static BoardNetworkHandler Instance;
    private BoardManager boardManager;

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

    /// <summary>
    /// Called by a client when a piece is moved.
    /// Runs on the server (host) when invoked by a client.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestMoveServerRpc(int fromFile, int fromRank, int toFile, int toRank)
    {
        // Convert coordinates into Square objects.
        Square fromSquare = new Square(fromFile, fromRank);
        Square toSquare = new Square(toFile, toRank);

        // Use the public wrapper from GameManager.
        if (!GameManager.Instance.TryGetLegalMove(fromSquare, toSquare, out Movement move))
        {
            Debug.LogWarning("Invalid move requested: " + fromSquare + " to " + toSquare);
            return;
        }
        
        // Execute the move on the server.
        if (GameManager.Instance.TryExecuteMove(move))
        {
            // Update board visuals on the server.
            boardManager.MovePiece(fromSquare, toSquare);
            // Broadcast the move to all clients.
            UpdateBoardClientRpc(fromFile, fromRank, toFile, toRank);
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
}
