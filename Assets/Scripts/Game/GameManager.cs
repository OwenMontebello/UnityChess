using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityChess;
using UnityEngine;
using Unity.Netcode;

public class GameManager : MonoBehaviourSingleton<GameManager>
{
    public static event Action NewGameStartedEvent;
    public static event Action GameEndedEvent;
    public static event Action GameResetToHalfMoveEvent;
    public static event Action MoveExecutedEvent;

    public Board CurrentBoard
    {
        get
        {
            game.BoardTimeline.TryGetCurrent(out Board currentBoard);
            return currentBoard;
        }
    }

    public Side SideToMove
    {
        get
        {
            game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions);
            return currentConditions.SideToMove;
        }
    }

    public Side StartingSide => game.ConditionsTimeline[0].SideToMove;
    public Timeline<HalfMove> HalfMoveTimeline => game.HalfMoveTimeline;
    public int LatestHalfMoveIndex => game.HalfMoveTimeline.HeadIndex;

    public int FullMoveNumber => StartingSide switch
    {
        Side.White => LatestHalfMoveIndex / 2 + 1,
        Side.Black => (LatestHalfMoveIndex + 1) / 2 + 1,
        _ => -1
    };

    private bool isWhiteAI;
    private bool isBlackAI;

    public List<(Square, Piece)> CurrentPieces
    {
        get
        {
            currentPiecesBacking.Clear();
            for (int file = 1; file <= 8; file++)
            {
                for (int rank = 1; rank <= 8; rank++)
                {
                    Piece piece = CurrentBoard[file, rank];
                    if (piece != null)
                        currentPiecesBacking.Add((new Square(file, rank), piece));
                }
            }
            return currentPiecesBacking;
        }
    }
    private readonly List<(Square, Piece)> currentPiecesBacking = new List<(Square, Piece)>();

    [SerializeField] private UnityChessDebug unityChessDebug;
    private Game game;
    private FENSerializer fenSerializer;
    private PGNSerializer pgnSerializer;
    private CancellationTokenSource promotionUITaskCancellationTokenSource;
    private ElectedPiece userPromotionChoice = ElectedPiece.None;
    private Dictionary<GameSerializationType, IGameSerializer> serializersByType;
    private GameSerializationType selectedSerializationType = GameSerializationType.FEN;

    public void Start()
    {
        // Subscribe to the event triggered when a piece is moved visually
        VisualPiece.VisualPieceMoved += OnPieceMoved;

        // Initialize FEN/PGN
        serializersByType = new Dictionary<GameSerializationType, IGameSerializer>
        {
            [GameSerializationType.FEN] = new FENSerializer(),
            [GameSerializationType.PGN] = new PGNSerializer()
        };

        StartNewGame();

#if DEBUG_VIEW
        unityChessDebug.gameObject.SetActive(true);
        unityChessDebug.enabled = true;
#endif
    }

    public async void StartNewGame()
    {
        game = new Game();
        NewGameStartedEvent?.Invoke();
    }

    public string SerializeGame()
    {
        return serializersByType.TryGetValue(selectedSerializationType, out IGameSerializer serializer)
            ? serializer?.Serialize(game)
            : null;
    }

    public void LoadGame(string serializedGame)
    {
        game = serializersByType[selectedSerializationType].Deserialize(serializedGame);
        NewGameStartedEvent?.Invoke();
    }

    public void ResetGameToHalfMoveIndex(int halfMoveIndex)
    {
        if (!game.ResetGameToHalfMoveIndex(halfMoveIndex)) return;

        UIManager.Instance.SetActivePromotionUI(false);
        promotionUITaskCancellationTokenSource?.Cancel();
        GameResetToHalfMoveEvent?.Invoke();
    }

    // --- PUBLIC WRAPPERS FOR BOARDNETWORKHANDLER ---

    /// <summary>
    /// Public method so BoardNetworkHandler can call this to see if a move is valid
    /// </summary>
    public bool TryGetLegalMove(Square fromSquare, Square toSquare, out Movement move)
    {
        return game.TryGetLegalMove(fromSquare, toSquare, out move);
    }

    /// <summary>
    /// Public method so BoardNetworkHandler can call this to actually execute the move
    /// </summary>
    public bool TryExecuteMove(Movement move)
    {
        if (!game.TryExecuteMove(move))
        {
            return false;
        }

        HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);

        if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
        {
            BoardManager.Instance.SetActiveAllPieces(false);
            GameEndedEvent?.Invoke();
        }
        else
        {
            BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
        }

        MoveExecutedEvent?.Invoke();
        return true;
    }

    // --- SPECIAL MOVE BEHAVIOR ---

    private async Task<bool> TryHandleSpecialMoveBehaviourAsync(SpecialMove specialMove)
    {
        switch (specialMove)
        {
            case CastlingMove castlingMove:
                BoardManager.Instance.CastleRook(castlingMove.RookSquare, castlingMove.GetRookEndSquare());
                return true;

            case EnPassantMove enPassantMove:
                BoardManager.Instance.TryDestroyVisualPiece(enPassantMove.CapturedPawnSquare);
                return true;

            case PromotionMove { PromotionPiece: null } promotionMove:
            {
                // Show the promotion UI
                UIManager.Instance.SetActivePromotionUI(true);
                BoardManager.Instance.SetActiveAllPieces(false);

                // Cancel any previous promotion tasks
                promotionUITaskCancellationTokenSource?.Cancel();
                promotionUITaskCancellationTokenSource = new CancellationTokenSource();

                // Correct usage of Task.Run with a lambda
                ElectedPiece choice = await Task.Run(
                    () => GetUserPromotionPieceChoice(),
                    promotionUITaskCancellationTokenSource.Token
                );

                UIManager.Instance.SetActivePromotionUI(false);
                BoardManager.Instance.SetActiveAllPieces(true);

                if (promotionUITaskCancellationTokenSource == null ||
                    promotionUITaskCancellationTokenSource.Token.IsCancellationRequested)
                {
                    return false;
                }

                // Actually set the promotion piece
                promotionMove.SetPromotionPiece(PromotionUtil.GeneratePromotionPiece(choice, SideToMove));
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
                BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);

                promotionUITaskCancellationTokenSource = null;
                return true;
            }

            case PromotionMove promotionMove:
            {
                // If we already have a PromotionPiece set
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
                BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);
                return true;
            }

            default:
                return false;
        }
    }

    private ElectedPiece GetUserPromotionPieceChoice()
    {
        // Busy-wait until userPromotionChoice is set
        while (userPromotionChoice == ElectedPiece.None) { }
        ElectedPiece result = userPromotionChoice;
        userPromotionChoice = ElectedPiece.None;
        return result;
    }

    public void ElectPiece(ElectedPiece choice)
    {
        userPromotionChoice = choice;
    }

    // --- MAIN VISUAL PIECE MOVED HANDLER ---

    private async void OnPieceMoved(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null)
    {
        Square endSquare = new Square(closestBoardSquareTransform.name);

        // If netcode is running, call BoardNetworkHandler's server RPC instead
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            int fromFile = movedPieceInitialSquare.File;
            int fromRank = movedPieceInitialSquare.Rank;
            int toFile   = endSquare.File;
            int toRank   = endSquare.Rank;

            BoardNetworkHandler.Instance.RequestMoveServerRpc(fromFile, fromRank, toFile, toRank);
            return;
        }

        // Otherwise, single-player logic:
        if (!game.TryGetLegalMove(movedPieceInitialSquare, endSquare, out Movement move))
        {
            // Invalid move => reset piece position
            movedPieceTransform.position = movedPieceTransform.parent.position;
            return;
        }

        // If it's a promotion move, set the piece if needed
        if (move is PromotionMove pm && pm.PromotionPiece == null)
        {
            // We'll do the async approach for user selection
            UIManager.Instance.SetActivePromotionUI(true);
            BoardManager.Instance.SetActiveAllPieces(false);

            promotionUITaskCancellationTokenSource?.Cancel();
            promotionUITaskCancellationTokenSource = new System.Threading.CancellationTokenSource();

            ElectedPiece choice = await Task.Run(
                () => GetUserPromotionPieceChoice(),
                promotionUITaskCancellationTokenSource.Token
            );

            UIManager.Instance.SetActivePromotionUI(false);
            BoardManager.Instance.SetActiveAllPieces(true);

            if (promotionUITaskCancellationTokenSource == null ||
                promotionUITaskCancellationTokenSource.Token.IsCancellationRequested)
            {
                // Cancelled => do nothing
                return;
            }

            pm.SetPromotionPiece(PromotionUtil.GeneratePromotionPiece(choice, SideToMove));
            BoardManager.Instance.TryDestroyVisualPiece(pm.Start);
            BoardManager.Instance.TryDestroyVisualPiece(pm.End);
            BoardManager.Instance.CreateAndPlacePieceGO(pm.PromotionPiece, pm.End);
        }

        // If it's not a special move, or if the special move was handled, we can do the rest
        if ((move is not SpecialMove specialMove || await TryHandleSpecialMoveBehaviourAsync(specialMove))
            && TryExecuteMove(move))
        {
            if (move is not SpecialMove)
            {
                BoardManager.Instance.TryDestroyVisualPiece(move.End);
            }
            if (move is PromotionMove)
            {
                movedPieceTransform = BoardManager.Instance.GetPieceGOAtPosition(move.End).transform;
            }

            // Re-parent the piece to the new square
            movedPieceTransform.parent = closestBoardSquareTransform;
            movedPieceTransform.position = closestBoardSquareTransform.position;
        }
    }

    public bool HasLegalMoves(Piece piece)
    {
        return game.TryGetLegalMovesForPiece(piece, out _);
    }
}