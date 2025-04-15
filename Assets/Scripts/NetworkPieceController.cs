using UnityEngine;
using UnityChess;
using Unity.Netcode;

public class NetworkPieceController : MonoBehaviour
{
    private Side localPlayerSide = Side.None;
    
    private void Start()
    {
        // Connect to player events
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

    // Store player color
    public void SetLocalPlayerSide(Side side)
    {
        localPlayerSide = side;
        Debug.Log($"NetworkPieceController: Local player is now {side}");
        
        // Update immediately
        if (GameManager.Instance != null)
        {
            CheckPieceInteraction(GameManager.Instance.SideToMove);
        }
    }
    
    // Enable/disable pieces based on turn
    public void CheckPieceInteraction(Side currentTurn)
    {
        // Log current status
        Debug.Log($"Current turn: {currentTurn}, Local player: {localPlayerSide}, Can move: {currentTurn == localPlayerSide}");
        
        // Only in network mode
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;
            
        Debug.Log($"Checking piece interaction. Current turn: {currentTurn}, Local player: {localPlayerSide}");
        
        VisualPiece[] pieces = FindObjectsOfType<VisualPiece>(true);
        foreach (VisualPiece piece in pieces)
        {
            // Enable pieces only if it's player's turn and their color
            bool shouldBeEnabled = (currentTurn == localPlayerSide && piece.PieceColor == localPlayerSide);
            
            if (piece.enabled != shouldBeEnabled)
            {
                piece.enabled = shouldBeEnabled;
                Debug.Log($"Setting {piece.PieceColor} piece at {piece.CurrentSquare} to {(shouldBeEnabled ? "enabled" : "disabled")}");
            }
        }
    }
}