using System.Collections.Generic;
using UnityChess;
using UnityEngine;
using static UnityChess.SquareUtil;

public class VisualPiece : MonoBehaviour
{
    // Delegate for handling the event when a visual piece has been moved.
    public delegate void VisualPieceMovedAction(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null);
    // Static event raised when a visual piece is moved.
    public static event VisualPieceMovedAction VisualPieceMoved;

    // The colour (side) of the piece (White or Black).
    public Side PieceColor;

    // Retrieves the current board square of the piece by converting its parent's name into a Square.
    public Square CurrentSquare => StringToSquare(transform.parent.name);

    // The radius used to detect nearby board squares for collision detection.
    private const float SquareCollisionRadius = 9f;

    // The camera used to view the board.
    private Camera boardCamera;
    // The screen-space position of the piece when it is first picked up.
    private Vector3 piecePositionSS;
    // A list to hold potential board square GameObjects that the piece might land on.
    private List<GameObject> potentialLandingSquares;
    // A cached reference to the transform of this piece.
    private Transform thisTransform;

    private void Start()
    {
        potentialLandingSquares = new List<GameObject>();
        thisTransform = transform;
        boardCamera = Camera.main;
    }

    public void OnMouseDown()
    {
        if (enabled)
        {
            piecePositionSS = boardCamera.WorldToScreenPoint(transform.position);
        }
    }

    private void OnMouseDrag()
    {
        if (enabled)
        {
            Vector3 nextPiecePositionSS = new Vector3(Input.mousePosition.x, Input.mousePosition.y, piecePositionSS.z);
            thisTransform.position = boardCamera.ScreenToWorldPoint(nextPiecePositionSS);
        }
    }

    public void OnMouseUp()
    {
        if (enabled)
        {
            potentialLandingSquares.Clear();
            BoardManager.Instance.GetSquareGOsWithinRadius(potentialLandingSquares, thisTransform.position, SquareCollisionRadius);

            if (potentialLandingSquares.Count == 0)
            {
                thisTransform.position = thisTransform.parent.position;
                return;
            }

            Transform closestSquareTransform = potentialLandingSquares[0].transform;
            float shortestDistanceFromPieceSquared = (closestSquareTransform.position - thisTransform.position).sqrMagnitude;

            for (int i = 1; i < potentialLandingSquares.Count; i++)
            {
                GameObject potentialLandingSquare = potentialLandingSquares[i];
                float distanceFromPieceSquared = (potentialLandingSquare.transform.position - thisTransform.position).sqrMagnitude;
                if (distanceFromPieceSquared < shortestDistanceFromPieceSquared)
                {
                    shortestDistanceFromPieceSquared = distanceFromPieceSquared;
                    closestSquareTransform = potentialLandingSquare.transform;
                }
            }

            VisualPieceMoved?.Invoke(CurrentSquare, thisTransform, closestSquareTransform);
        }
    }
}
