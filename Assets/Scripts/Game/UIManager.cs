using System;
using System.Collections.Generic;
using UnityChess;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class UIManager : MonoBehaviourSingleton<UIManager>
{
    // Delegates and events for turn-based logic
    public delegate void SideAssignedHandler(Side side);
    public delegate void TurnChangedHandler(Side currentTurn);
    public event SideAssignedHandler OnLocalPlayerSideAssigned;
    public event TurnChangedHandler OnTurnChanged;

    [SerializeField] private GameObject promotionUI = null;
    [SerializeField] private Text resultText = null;
    [SerializeField] private InputField GameStringInputField = null;
    [SerializeField] private Image whiteTurnIndicator = null;
    [SerializeField] private Image blackTurnIndicator = null;
    [SerializeField] private GameObject moveHistoryContentParent = null;
    [SerializeField] private Scrollbar moveHistoryScrollbar = null;
    [SerializeField] private FullMoveUI moveUIPrefab = null;
    [SerializeField] private Text[] boardInfoTexts = null;
    [SerializeField] private Color backgroundColor = new Color(0.39f, 0.39f, 0.39f);
    [SerializeField] private Color textColor = new Color(1f, 0.71f, 0.18f);
    [SerializeField, Range(-0.25f, 0.25f)] private float buttonColorDarkenAmount = 0f;
    [SerializeField, Range(-0.25f, 0.25f)] private float moveHistoryAlternateColorDarkenAmount = 0f;
    
    // New fields for turn-based logic
    [SerializeField] private Text currentTurnText = null;
    [SerializeField] private Text gameStateText = null;
    [SerializeField] private Text localPlayerInfoText = null;

    private Timeline<FullMoveUI> moveUITimeline;
    private Color buttonColor;
    private Side localPlayerSide;

    private void Start()
    {
        GameManager.NewGameStartedEvent += OnNewGameStarted;
        GameManager.GameEndedEvent += OnGameEnded;
        GameManager.MoveExecutedEvent += OnMoveExecuted;
        GameManager.GameResetToHalfMoveEvent += OnGameResetToHalfMove;

        moveUITimeline = new Timeline<FullMoveUI>();

        foreach (Text boardInfoText in boardInfoTexts)
        {
            boardInfoText.color = textColor;
        }

        buttonColor = new Color(
            backgroundColor.r - buttonColorDarkenAmount,
            backgroundColor.g - buttonColorDarkenAmount,
            backgroundColor.b - buttonColorDarkenAmount
        );
        
        // Initialize game state text if available
        if (gameStateText != null)
        {
            gameStateText.text = "Not Connected";
            gameStateText.gameObject.SetActive(true);
        }
    }

    private void OnNewGameStarted()
    {
        UpdateGameStringInputField();
        ValidateIndicators();

        for (int i = 0; i < moveHistoryContentParent.transform.childCount; i++)
        {
            Destroy(moveHistoryContentParent.transform.GetChild(i).gameObject);
        }

        moveUITimeline.Clear();
        resultText.gameObject.SetActive(false);
        
        // Update turn text if in network mode
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            UpdateTurnIndicator(Side.White); // Default to white starts
        }
    }

    private void OnGameEnded()
    {
        GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);

        if (latestHalfMove.CausedCheckmate)
        {
            resultText.text = $"{latestHalfMove.Piece.Owner} Wins!";
        }
        else if (latestHalfMove.CausedStalemate)
        {
            resultText.text = "Draw.";
        }

        resultText.gameObject.SetActive(true);
        
        // Update game state text if available
        if (gameStateText != null)
        {
            gameStateText.text = "Game Over";
        }
    }

    private void OnMoveExecuted()
    {
        UpdateGameStringInputField();

        Side sideToMove = GameManager.Instance.SideToMove;
        whiteTurnIndicator.enabled = (sideToMove == Side.White);
        blackTurnIndicator.enabled = (sideToMove == Side.Black);

        GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove lastMove);
        AddMoveToHistory(lastMove, sideToMove.Complement());
        
        // Update turn text if available
        if (currentTurnText != null)
        {
            currentTurnText.text = $"Current Turn: {sideToMove}";
        }
    }

    private void OnGameResetToHalfMove()
    {
        UpdateGameStringInputField();
        moveUITimeline.HeadIndex = GameManager.Instance.LatestHalfMoveIndex / 2;
        ValidateIndicators();
    }

    public void SetActivePromotionUI(bool value)
    {
        promotionUI.gameObject.SetActive(value);
    }

    public void OnElectionButton(int choice)
    {
        GameManager.Instance.ElectPiece((ElectedPiece)choice);
    }

    public void ResetGameToFirstHalfMove()
    {
        GameManager.Instance.ResetGameToHalfMoveIndex(0);
    }

    public void ResetGameToPreviousHalfMove()
    {
        GameManager.Instance.ResetGameToHalfMoveIndex(Math.Max(0, GameManager.Instance.LatestHalfMoveIndex - 1));
    }

    public void ResetGameToNextHalfMove()
    {
        GameManager.Instance.ResetGameToHalfMoveIndex(Math.Min(
            GameManager.Instance.LatestHalfMoveIndex + 1,
            GameManager.Instance.HalfMoveTimeline.Count - 1
        ));
    }

    public void ResetGameToLastHalfMove()
    {
        GameManager.Instance.ResetGameToHalfMoveIndex(GameManager.Instance.HalfMoveTimeline.Count - 1);
    }

    public void StartNewGame()
    {
        GameManager.Instance.StartNewGame();
    }

    public void LoadGame()
    {
        GameManager.Instance.LoadGame(GameStringInputField.text);
    }

    private void AddMoveToHistory(HalfMove latestHalfMove, Side latestTurnSide)
    {
        RemoveAlternateHistory();

        switch (latestTurnSide)
        {
            case Side.Black:
            {
                if (moveUITimeline.HeadIndex == -1)
                {
                    FullMoveUI newFullMoveUI = Instantiate(moveUIPrefab, moveHistoryContentParent.transform);
                    moveUITimeline.AddNext(newFullMoveUI);

                    newFullMoveUI.transform.SetSiblingIndex(GameManager.Instance.FullMoveNumber - 1);
                    newFullMoveUI.backgroundImage.color = backgroundColor;
                    newFullMoveUI.whiteMoveButtonImage.color = buttonColor;
                    newFullMoveUI.blackMoveButtonImage.color = buttonColor;

                    if (newFullMoveUI.FullMoveNumber % 2 == 0)
                    {
                        newFullMoveUI.SetAlternateColor(moveHistoryAlternateColorDarkenAmount);
                    }

                    newFullMoveUI.MoveNumberText.text = $"{newFullMoveUI.FullMoveNumber}.";
                    newFullMoveUI.WhiteMoveButton.enabled = false;
                }

                moveUITimeline.TryGetCurrent(out FullMoveUI latestFullMoveUIBlack);
                latestFullMoveUIBlack.BlackMoveText.text = latestHalfMove.ToAlgebraicNotation();
                latestFullMoveUIBlack.BlackMoveButton.enabled = true;
                break;
            }
            case Side.White:
            {
                FullMoveUI newFullMoveUI = Instantiate(moveUIPrefab, moveHistoryContentParent.transform);
                newFullMoveUI.transform.SetSiblingIndex(GameManager.Instance.FullMoveNumber - 1);
                newFullMoveUI.backgroundImage.color = backgroundColor;
                newFullMoveUI.whiteMoveButtonImage.color = buttonColor;
                newFullMoveUI.blackMoveButtonImage.color = buttonColor;

                if (newFullMoveUI.FullMoveNumber % 2 == 0)
                {
                    newFullMoveUI.SetAlternateColor(moveHistoryAlternateColorDarkenAmount);
                }

                newFullMoveUI.MoveNumberText.text = $"{newFullMoveUI.FullMoveNumber}.";

                newFullMoveUI.WhiteMoveText.text = latestHalfMove.ToAlgebraicNotation();
                newFullMoveUI.BlackMoveText.text = "";
                newFullMoveUI.BlackMoveButton.enabled = false;
                newFullMoveUI.WhiteMoveButton.enabled = true;

                moveUITimeline.AddNext(newFullMoveUI);
                break;
            }
        }

        moveHistoryScrollbar.value = 0;
    }

    private void RemoveAlternateHistory()
    {
        if (!moveUITimeline.IsUpToDate)
        {
            GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove lastHalfMove);
            resultText.gameObject.SetActive(lastHalfMove.CausedCheckmate);

            List<FullMoveUI> divergentFullMoveUIs = moveUITimeline.PopFuture();
            foreach (FullMoveUI divergentFullMoveUI in divergentFullMoveUIs)
            {
                Destroy(divergentFullMoveUI.gameObject);
            }
        }
    }

    private void ValidateIndicators()
    {
        Side sideToMove = GameManager.Instance.SideToMove;
        whiteTurnIndicator.enabled = (sideToMove == Side.White);
        blackTurnIndicator.enabled = (sideToMove == Side.Black);
        
        // Update turn text if available
        if (currentTurnText != null)
        {
            currentTurnText.text = $"Current Turn: {sideToMove}";
        }
    }

    private void UpdateGameStringInputField()
    {
        GameStringInputField.text = GameManager.Instance.SerializeGame();
    }

    #region Network Connection Methods

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
        
        // Update connection status in UI
        UpdateNetworkConnectionStatus(true, NetworkManager.Singleton.IsHost);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Local client disconnected from the session.");
            UpdateNetworkConnectionStatus(false, false);
        }
    }

    public void OnHostButtonClicked()
    {
        if (!NetworkManager.Singleton.IsListening)
        {
            Debug.Log("Starting host...");
            NetworkManager.Singleton.StartHost();
            UpdateNetworkConnectionStatus(true, true);
        }
        else
        {
            Debug.LogWarning("Already hosting or connected.");
        }
    }

    public void OnJoinButtonClicked()
    {
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
        {
            Debug.Log("Joining session as client...");
            NetworkManager.Singleton.StartClient();
            UpdateNetworkConnectionStatus(true, false);
        }
        else
        {
            Debug.LogWarning("Already connected or hosting.");
        }
    }

    public void OnLeaveButtonClicked()
{
    if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost)
    {
        Debug.Log("Leaving session...");
        
        // Reset the game before disconnecting
        GameManager.Instance.StartNewGame();
        
        // Shutdown network connection
        NetworkManager.Singleton.Shutdown();
        UpdateNetworkConnectionStatus(false, false);
    }
    else
    {
        Debug.LogWarning("Not currently connected to any session.");
    }
}

    public void OnRejoinButtonClicked()
    {
        if (!NetworkManager.Singleton.IsConnectedClient && !NetworkManager.Singleton.IsHost)
        {
            Debug.Log("Attempting to rejoin session...");
            NetworkManager.Singleton.StartClient();
        }
        else
        {
            Debug.LogWarning("Already connected or hosting.");
        }
    }

    #endregion
    
    #region Turn-Based Logic Methods
    
    /// <summary>
    /// Updates the turn indicator based on whose turn it is
    /// </summary>
    public void UpdateTurnIndicator(Side currentTurn)
{
    // Add null checks for UI elements
    if (whiteTurnIndicator != null)
        whiteTurnIndicator.enabled = (currentTurn == Side.White);
    
    if (blackTurnIndicator != null)
        blackTurnIndicator.enabled = (currentTurn == Side.Black);
    
    // Update turn text if available
    if (currentTurnText != null)
    {
        currentTurnText.text = $"Current Turn: {currentTurn}";
    }
    
    // When it's your turn, show a highlight or notification
    bool isYourTurn = (localPlayerSide == currentTurn);
    if (isYourTurn)
    {
        Debug.Log("It's your turn!");
    }
    
    // Trigger the event for NetworkPieceController
    OnTurnChanged?.Invoke(currentTurn);
}

    /// <summary>
    /// Sets the local player's side (white/black)
    /// </summary>
    public void SetLocalPlayerSide(Side side)
    {
        localPlayerSide = side;
        Debug.Log($"Local player is playing as {side}");
        
        // Update UI to reflect which side the player is controlling
        if (localPlayerInfoText != null)
        {
            localPlayerInfoText.text = $"You are playing as {side}";
            localPlayerInfoText.gameObject.SetActive(true);
        }
        
        // Trigger the event for NetworkPieceController
        OnLocalPlayerSideAssigned?.Invoke(side);
    }

    /// <summary>
    /// Displays a game over message
    /// </summary>
    public void DisplayGameOverMessage(string message)
    {
        if (resultText != null)
        {
            resultText.text = message;
            resultText.gameObject.SetActive(true);
        }
        
        if (gameStateText != null)
        {
            gameStateText.text = "Game Over";
            gameStateText.gameObject.SetActive(true);
        }
        
        Debug.Log($"Game Over: {message}");
    }

    /// <summary>
    /// Updates UI to show network connection status
    /// </summary>
    public void UpdateNetworkConnectionStatus(bool isConnected, bool isHost)
    {
        // Update UI to show network status
        if (gameStateText != null)
        {
            if (isConnected)
            {
                gameStateText.text = isHost ? "Hosting Game" : "Connected as Client";
            }
            else
            {
                gameStateText.text = "Not Connected";
            }
            gameStateText.gameObject.SetActive(true);
        }
    }
    
    #endregion
}