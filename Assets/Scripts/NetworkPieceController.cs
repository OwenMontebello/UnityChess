using UnityEngine;
using UnityChess;
using Unity.Netcode;

public class NetworkPieceController : MonoBehaviour
{
    private Side localPlayerSide = Side.None;
    
    private void Start()
    {
        // Subscribe to turn change events
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnLocalPlayerSideAssigned += SetLocalPlayerSide;
            UIManager.Instance.OnTurnChanged += CheckPieceInteraction;
        }
    }
    
    private void OnDestroy()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnLocalPlayerSideAssigned -= SetLocalPlayerSide;
            UIManager.Instance.OnTurnChanged -= CheckPieceInteraction;
        }
    }

    public void SetLocalPlayerSide(Side side)
    {
        localPlayerSide = side;
        Debug.Log($"NetworkPieceController: Local player is now {side}");
        
        // Check current game state immediately after side assignment
        if (GameManager.Instance != null)
        {
            CheckPieceInteraction(GameManager.Instance.SideToMove);
        }
    }
    
    public void CheckPieceInteraction(Side currentTurn)
    {
        // Add this near the beginning of the method
Debug.Log($"Current turn: {currentTurn}, Local player: {localPlayerSide}, Can move: {currentTurn == localPlayerSide}");
        // Only apply special handling in networked games
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;
            
        Debug.Log($"Checking piece interaction. Current turn: {currentTurn}, Local player: {localPlayerSide}");
        
        VisualPiece[] pieces = FindObjectsOfType<VisualPiece>(true);
        foreach (VisualPiece piece in pieces)
        {
            // If it's our turn and our piece, enable it
            bool shouldBeEnabled = (currentTurn == localPlayerSide && piece.PieceColor == localPlayerSide);
            
            if (piece.enabled != shouldBeEnabled)
            {
                piece.enabled = shouldBeEnabled;
                Debug.Log($"Setting {piece.PieceColor} piece at {piece.CurrentSquare} to {(shouldBeEnabled ? "enabled" : "disabled")}");
            }
        }
    }
}