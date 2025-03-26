using System;
using System.Collections.Generic;
using UnityChess;
using UnityEngine;
using static UnityChess.SquareUtil;
using Unity.Netcode;

public class BoardManager : MonoBehaviourSingleton<BoardManager>
{
    private readonly GameObject[] allSquaresGO = new GameObject[64];
    private Dictionary<Square, GameObject> positionMap;

    private const float BoardPlaneSideLength = 14f;
    private const float BoardPlaneSideHalfLength = BoardPlaneSideLength * 0.5f;
    private const float BoardHeight = 1.6f;

    private void Awake()
    {
        GameManager.NewGameStartedEvent += OnNewGameStarted;
        GameManager.GameResetToHalfMoveEvent += OnGameResetToHalfMove;

        positionMap = new Dictionary<Square, GameObject>(64);
        Transform boardTransform = transform;
        Vector3 boardPosition = boardTransform.position;

        // Create 64 squares.
        for (int file = 1; file <= 8; file++)
        {
            for (int rank = 1; rank <= 8; rank++)
            {
                GameObject squareGO = new GameObject(SquareToString(file, rank))
                {
                    transform =
                    {
                        position = new Vector3(
                            boardPosition.x + FileOrRankToSidePosition(file),
                            boardPosition.y + BoardHeight,
                            boardPosition.z + FileOrRankToSidePosition(rank)
                        ),
                        parent = boardTransform
                    },
                    tag = "Square"
                };

                positionMap.Add(new Square(file, rank), squareGO);
                allSquaresGO[(file - 1) * 8 + (rank - 1)] = squareGO;
            }
        }
    }

    // Add this near the top of the class
// Add near the top of the BoardManager class with other fields
private Side localPlayerSide = Side.None;

// Add this new method
public void SetLocalPlayerSide(Side side)
{
    localPlayerSide = side;
    Debug.Log($"BoardManager: Local player is now playing as {side}");
}

// Find the existing method and replace its implementation with this:
public void EnsureOnlyPiecesOfSideAreEnabled(Side side)
// Don't create a new method! Find the existing method and replace its contents with this:
{
    VisualPiece[] visualPieces = GetComponentsInChildren<VisualPiece>(true);
    
    // In network games, we should only enable pieces for the side controlled by this client
    bool isNetworkGame = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    
    foreach (VisualPiece vp in visualPieces)
    {
        Piece piece = GameManager.Instance.CurrentBoard[vp.CurrentSquare];
        
        if (isNetworkGame)
        {
            // In network game, check if this piece belongs to the local player's side
            bool isLocalPlayerPiece = (vp.PieceColor == localPlayerSide);
            bool isCurrentTurn = (side == localPlayerSide);
            
            // Only enable pieces if it's local player's turn AND the piece belongs to them
            vp.enabled = isLocalPlayerPiece && isCurrentTurn && GameManager.Instance.HasLegalMoves(piece);
        }
        else
        {
            // Original behavior for single-player
            vp.enabled = (vp.PieceColor == side && GameManager.Instance.HasLegalMoves(piece));
        }
    }
}

    private void OnNewGameStarted()
    {
        Debug.Log($"[OnNewGameStarted] On client {Unity.Netcode.NetworkManager.Singleton.LocalClientId}");
        ClearBoard();
        foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
        {
            CreateAndPlacePieceGO(piece, square);
        }
        EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
    }

    private void OnGameResetToHalfMove()
    {
        ClearBoard();
        foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
        {
            CreateAndPlacePieceGO(piece, square);
        }
        GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
        if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
            SetActiveAllPieces(false);
        else
            EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
    }

    // Clears all VisualPiece GameObjects from the board.
    private void ClearBoard()
    {
        VisualPiece[] visualPieces = GetComponentsInChildren<VisualPiece>(true);
        foreach (VisualPiece piece in visualPieces)
        {
            DestroyImmediate(piece.gameObject);
        }
    }

    // Converts file/rank (1..8) to an x/z offset from the board's center.
    private static float FileOrRankToSidePosition(int index)
    {
        float t = (index - 1) / 7f;
        return Mathf.Lerp(-BoardPlaneSideHalfLength, BoardPlaneSideHalfLength, t);
    }

    public void CreateAndPlacePieceGO(Piece piece, Square position)
    {
        string modelName = $"{piece.Owner} {piece.GetType().Name}";
        GameObject pieceGO = Instantiate(
            Resources.Load("PieceSets/Marble/" + modelName) as GameObject,
            positionMap[position].transform
        );
    }

    // Moves a piece from one square to another.
public void MovePiece(Square fromSquare, Square toSquare)
{
    GameObject pieceGO = GetPieceGOAtPosition(fromSquare);
    if (pieceGO == null)
    {
        Debug.LogWarning($"[MovePiece] No piece found at {fromSquare}");
        return;
    }

    GameObject destinationSquare = GetSquareGOByPosition(toSquare);
    if (destinationSquare == null)
    {
        Debug.LogWarning($"[MovePiece] No square found at {toSquare}");
        return;
    }

    // First destroy any piece at the destination
    TryDestroyVisualPiece(toSquare);
    
    // Then move the piece
    pieceGO.transform.SetParent(destinationSquare.transform);
    pieceGO.transform.localPosition = Vector3.zero;
    
    Debug.Log($"[MovePiece] Moved piece from {fromSquare} to {toSquare}");
}


    public void CastleRook(Square rookPosition, Square endSquare)
    {
        GameObject rookGO = GetPieceGOAtPosition(rookPosition);
        rookGO.transform.SetParent(GetSquareGOByPosition(endSquare).transform);
        rookGO.transform.localPosition = Vector3.zero;
    }

    public void SetActiveAllPieces(bool active)
    {
        VisualPiece[] visualPieces = GetComponentsInChildren<VisualPiece>(true);
        foreach (VisualPiece vp in visualPieces)
        {
            vp.enabled = active;
        }
    }



    public void TryDestroyVisualPiece(Square position)
    {
        VisualPiece vp = positionMap[position].GetComponentInChildren<VisualPiece>();
        if (vp != null)
        {
            DestroyImmediate(vp.gameObject);
        }
    }

    public GameObject GetPieceGOAtPosition(Square position)
    {
        GameObject square = GetSquareGOByPosition(position);
        return (square.transform.childCount == 0) ? null : square.transform.GetChild(0).gameObject;
    }

    public GameObject GetSquareGOByPosition(Square position)
    {
        return Array.Find(allSquaresGO, go => go.name == SquareToString(position));
    }

    public void GetSquareGOsWithinRadius(List<GameObject> squareGOs, Vector3 positionWS, float radius)
    {
        float radiusSqr = radius * radius;
        foreach (GameObject squareGO in allSquaresGO)
        {
            if ((squareGO.transform.position - positionWS).sqrMagnitude < radiusSqr)
            {
                squareGOs.Add(squareGO);
            }
        }
    }
}