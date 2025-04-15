using System.Collections.Generic;
using UnityChess;
using UnityEngine;
using UnityEngine.UI;

public class UnityChessDebug : MonoBehaviourSingleton<UnityChessDebug>
{
    // Debug board and text size settings
    [SerializeField] private GameObject debugBoard = null;
    [SerializeField] private int fontSize = 20;

    // Initialize text components for all squares
    private void Awake()
    {
        for (int file = 1; file <= 8; file++)
        {
            for (int rank = 1; rank <= 8; rank++)
            {
                Transform squareTransform = debugBoard.transform.Find($"{SquareUtil.SquareToString(file, rank)}");
                Text squareText = squareTransform.GetComponentInChildren<Text>();
                squareText.fontSize = fontSize;
                squareText.resizeTextForBestFit = false;
            }
        }
    }

    // Refresh debug view every frame
    private void Update()
    {
        UpdateBoardDebugView(GameManager.Instance.CurrentBoard);
    }

    // Print all legal moves to console
    public static void ShowLegalMovesInLog(ICollection<Movement> legalMoves)
    {
        string debugMessage = $"# of valid moves: {legalMoves?.Count ?? 0}\n";
        if (legalMoves != null)
        {
            foreach (Movement validMove in legalMoves)
            {
                debugMessage += $"{validMove}\n";
            }
        }

        Debug.LogWarning(debugMessage);
    }

    // Update visual representation of board
    private void UpdateBoardDebugView(Board board)
    {
        for (int file = 1; file <= 8; file++)
        {
            for (int rank = 1; rank <= 8; rank++)
            {
                Piece piece = board[file, rank];
                Transform squareTransform = debugBoard.transform.Find($"{SquareUtil.SquareToString(file, rank)}");

                Text squareText = squareTransform.GetComponentInChildren<Text>();

                Image squareBackground = squareTransform.GetComponent<Image>();
                switch (piece)
                {
                    // Black pieces with white text
                    case { Owner: Side.Black }:
                        squareBackground.color = Color.black;
                        squareText.color = Color.white;
                        squareText.text = piece.ToTextArt();
                        break;
                    // White pieces with black text
                    case { Owner: Side.White }:
                        squareBackground.color = Color.white;
                        squareText.color = Color.black;
                        squareText.text = piece.ToTextArt();
                        break;
                    // Empty squares are gray
                    default:
                        squareBackground.color = Color.gray;
                        squareText.text = string.Empty;
                        break;
                }
            }
        }
    }
}