using UnityEngine;
using System;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    public const int BoardPointsX = 31;
    public const int BoardPointsY = 19;

    public static GameManager Instance { get; private set; }

    public event EventHandler<OnGripPositionClickedEventArgs> OnGripPositionClicked;
    public class OnGripPositionClickedEventArgs : EventArgs
    {
        public int x;
        public int y;
        public PlayerType playerType;
    }

    public event EventHandler OnGameStarted;
    public event EventHandler<OnGameWinEventArgs> OnGameWin;
    public class OnGameWinEventArgs : EventArgs
    {
        public Vector2Int centerGridposition;
        public Orientation orientation;
        public PlayerType playerWinType;
    }

    public event EventHandler OnCurrentPlayerType;
    public event EventHandler OnRematch;
    public event EventHandler OnScoreChanged;
    public event EventHandler OnGameReturnedToLobby;
    public event EventHandler OnLobbyPlayersChanged;

    public enum PlayerType
    {
        None,
        Cross,
        Circle,
    }

    public enum Orientation
    {
        Horizontal,
        Vertical,
        DiagonalA,
        DiagonalB,
    }

    private PlayerType localPlayerType = PlayerType.None;
    private NetworkVariable<PlayerType> currentPlayerType =
        new NetworkVariable<PlayerType>(
            PlayerType.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private NetworkVariable<int> playerCrossScore = new NetworkVariable<int>();
    private NetworkVariable<int> playerCircleScore = new NetworkVariable<int>();
    private NetworkVariable<bool> isGameStarted =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    private NetworkVariable<int> lobbyPlayerCount =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private PlayerType[,] playerTypeArrray;
    private int moveCount;
    private ulong circleClientId = ulong.MaxValue;
    private bool isAIGame;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate GameManager found. Destroying the new one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        playerTypeArrray = new PlayerType[BoardPointsX, BoardPointsY];
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log("GameManager spawned for client: " + NetworkManager.Singleton.LocalClientId);

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += Server_OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback += Server_OnClientDisconnectedCallback;
            AssignConnectedPlayerTypes();
            RefreshLobbyPlayerCount();
        }

        lobbyPlayerCount.OnValueChanged += LobbyPlayerCount_OnValueChanged;
        currentPlayerType.OnValueChanged += CurrentPlayerType_OnValueChanged;
        playerCrossScore.OnValueChanged += Score_OnValueChanged;
        playerCircleScore.OnValueChanged += Score_OnValueChanged;
        isGameStarted.OnValueChanged += IsGameStarted_OnValueChanged;

        OnScoreChanged?.Invoke(this, EventArgs.Empty);
        OnLobbyPlayersChanged?.Invoke(this, EventArgs.Empty);
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= Server_OnClientConnectedCallback;
                NetworkManager.Singleton.OnClientDisconnectCallback -= Server_OnClientDisconnectedCallback;
            }
        }

        lobbyPlayerCount.OnValueChanged -= LobbyPlayerCount_OnValueChanged;
        currentPlayerType.OnValueChanged -= CurrentPlayerType_OnValueChanged;
        playerCrossScore.OnValueChanged -= Score_OnValueChanged;
        playerCircleScore.OnValueChanged -= Score_OnValueChanged;
        isGameStarted.OnValueChanged -= IsGameStarted_OnValueChanged;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Server_OnClientConnectedCallback(ulong clientId)
    {
        AssignConnectedPlayerTypes();
        RefreshLobbyPlayerCount();
    }

    private void Server_OnClientDisconnectedCallback(ulong clientId)
    {
        AssignConnectedPlayerTypes();
        RefreshLobbyPlayerCount();

        if (isGameStarted.Value && !isAIGame && GetLobbyPlayerCount() < 2)
        {
            ReturnToLobbyAfterPlayerExit();
        }
    }

    private void AssignConnectedPlayerTypes()
    {
        if (!IsServer || NetworkManager.Singleton == null)
        {
            return;
        }

        circleClientId = ulong.MaxValue;

        int playerIndex = 0;
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            PlayerType assignedType = PlayerType.None;

            if (playerIndex == 0)
            {
                assignedType = PlayerType.Cross;
            }
            else if (playerIndex == 1)
            {
                assignedType = PlayerType.Circle;
                circleClientId = client.ClientId;
            }

            AssignPlayerTypeRpc(client.ClientId, assignedType);
            playerIndex++;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AssignPlayerTypeRpc(ulong clientId, PlayerType type)
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.LocalClientId == clientId)
        {
            localPlayerType = type;
            Debug.Log("Vai tro cua ban trong phong nay la: " + localPlayerType);
        }
    }

    private void RefreshLobbyPlayerCount()
    {
        if (!IsServer || NetworkManager.Singleton == null)
        {
            return;
        }

        lobbyPlayerCount.Value = NetworkManager.Singleton.ConnectedClientsList.Count;
        NotifyLobbyChangedRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyLobbyChangedRpc()
    {
        OnLobbyPlayersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void StartGameFromLobby()
    {
        if (!IsServer)
        {
            Debug.LogWarning("Only the host can start the game.");
            return;
        }

        if (!CanStartGameFromLobby())
        {
            Debug.Log("Chua du 2 nguoi choi chinh, khong the Start Game!");
            return;
        }

        ResetBoardState();
        isAIGame = false;
        currentPlayerType.Value = PlayerType.Cross;
        isGameStarted.Value = true;
    }

    private void ReturnToLobbyAfterPlayerExit()
    {
        ResetBoardState();
        currentPlayerType.Value = PlayerType.None;
        TriggerOnRematchRpc();
        isGameStarted.Value = false;
    }

    private void LobbyPlayerCount_OnValueChanged(int oldCount, int newCount)
    {
        OnLobbyPlayersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CurrentPlayerType_OnValueChanged(PlayerType oldPlayertype, PlayerType newPlayerType)
    {
        OnCurrentPlayerType?.Invoke(this, EventArgs.Empty);
    }

    private void Score_OnValueChanged(int oldScore, int newScore)
    {
        OnScoreChanged?.Invoke(this, EventArgs.Empty);
    }

    private void IsGameStarted_OnValueChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            OnGameStarted?.Invoke(this, EventArgs.Empty);
        }
        else if (oldValue)
        {
            OnGameReturnedToLobby?.Invoke(this, EventArgs.Empty);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void clickedOnGripPositionRpc(
        int x,
        int y,
        PlayerType requestedPlayerType,
        RpcParams rpcParams = default)
    {
        if (!isGameStarted.Value)
        {
            return;
        }

        PlayerType playerType = GetPlayerTypeForClient(rpcParams.Receive.SenderClientId);
        if (playerType == PlayerType.None || playerType != requestedPlayerType)
        {
            Debug.LogWarning(
                $"Rejected move from client {rpcParams.Receive.SenderClientId} as {requestedPlayerType}.");
            return;
        }

        TryPlaceMove(x, y, playerType);
    }

    private bool TryPlaceMove(int x, int y, PlayerType playerType)
    {
        if (!IsInBoard(x, y))
        {
            Debug.LogWarning($"Rejected out-of-range move ({x}, {y}).");
            return false;
        }

        if (playerType != currentPlayerType.Value)
        {
            Debug.Log("It's not your turn!");
            return false;
        }

        if (playerTypeArrray[x, y] != PlayerType.None)
        {
            Debug.Log("This position is already occupied!");
            return false;
        }

        playerTypeArrray[x, y] = playerType;
        moveCount++;

        OnGripPositionClicked?.Invoke(this, new OnGripPositionClickedEventArgs
        {
            x = x,
            y = y,
            playerType = playerType,
        });

        currentPlayerType.Value =
            currentPlayerType.Value == PlayerType.Cross ?
            PlayerType.Circle :
            PlayerType.Cross;

        TestWinner(x, y);
        return true;
    }

    private bool CheckLine(int x, int y, int dirX, int dirY, PlayerType playerType)
    {
        int count = 1;
        count += CountInDirection(x, y, dirX, dirY, playerType);
        count += CountInDirection(x, y, -dirX, -dirY, playerType);
        return count >= 5;
    }

    private int CountInDirection(int x, int y, int dirX, int dirY, PlayerType playerType)
    {
        int count = 0;
        int checkX = x + dirX;
        int checkY = y + dirY;

        while (IsInBoard(checkX, checkY) &&
               playerTypeArrray[checkX, checkY] == playerType)
        {
            count++;
            checkX += dirX;
            checkY += dirY;
        }

        return count;
    }

    private void TestWinner(int x, int y)
    {
        PlayerType playerType = playerTypeArrray[x, y];
        if (playerType == PlayerType.None)
        {
            return;
        }

        if (CheckLine(x, y, 1, 0, playerType))
        {
            Win(GetWinCenter(x, y, 1, 0, playerType), Orientation.Horizontal);
        }
        else if (CheckLine(x, y, 0, 1, playerType))
        {
            Win(GetWinCenter(x, y, 0, 1, playerType), Orientation.Vertical);
        }
        else if (CheckLine(x, y, 1, 1, playerType))
        {
            Win(GetWinCenter(x, y, 1, 1, playerType), Orientation.DiagonalA);
        }
        else if (CheckLine(x, y, 1, -1, playerType))
        {
            Win(GetWinCenter(x, y, 1, -1, playerType), Orientation.DiagonalB);
        }
        else
        {
            CheckDraw();
        }
    }

    private void Win(Vector2Int centerPos, Orientation orientation)
    {
        PlayerType winnerType = playerTypeArrray[centerPos.x, centerPos.y];
        switch (winnerType)
        {
            case PlayerType.Cross:
                playerCrossScore.Value++;
                break;
            case PlayerType.Circle:
                playerCircleScore.Value++;
                break;
        }

        currentPlayerType.Value = PlayerType.None;
        TriggerOnGameWinRpc(centerPos, orientation, winnerType);
    }

    private void CheckDraw()
    {
        if (moveCount < BoardPointsX * BoardPointsY)
        {
            return;
        }

        Debug.Log("Draw!");
        currentPlayerType.Value = PlayerType.None;
        TriggerOnGameWinRpc(Vector2Int.zero, Orientation.Horizontal, PlayerType.None);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerOnGameWinRpc(Vector2Int centerPos, Orientation orientation, PlayerType playerWinType)
    {
        OnGameWin?.Invoke(this, new OnGameWinEventArgs
        {
            centerGridposition = centerPos,
            orientation = orientation,
            playerWinType = playerWinType,
        });
    }

    private Vector2Int GetWinCenter(int x, int y, int dirX, int dirY, PlayerType playerType)
    {
        int minX = x;
        int minY = y;
        int maxX = x;
        int maxY = y;

        int checkX = x + dirX;
        int checkY = y + dirY;
        while (IsInBoard(checkX, checkY) &&
               playerTypeArrray[checkX, checkY] == playerType)
        {
            maxX = checkX;
            maxY = checkY;
            checkX += dirX;
            checkY += dirY;
        }

        checkX = x - dirX;
        checkY = y - dirY;
        while (IsInBoard(checkX, checkY) &&
               playerTypeArrray[checkX, checkY] == playerType)
        {
            minX = checkX;
            minY = checkY;
            checkX -= dirX;
            checkY -= dirY;
        }

        return new Vector2Int((minX + maxX) / 2, (minY + maxY) / 2);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RematchRpc(RpcParams rpcParams = default)
    {
        PlayerType playerType = GetPlayerTypeForClient(rpcParams.Receive.SenderClientId);
        if (playerType == PlayerType.None)
        {
            Debug.LogWarning($"Rejected rematch request from client {rpcParams.Receive.SenderClientId}.");
            return;
        }

        ResetBoardState();
        currentPlayerType.Value = PlayerType.Cross;
        isGameStarted.Value = true;
        TriggerOnRematchRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerOnRematchRpc()
    {
        OnRematch?.Invoke(this, EventArgs.Empty);
    }

    public void StartAIGame()
    {
        if (!IsServer)
        {
            Debug.LogError("Only the server can start an AI game.");
            return;
        }

        ResetBoardState();
        isAIGame = true;
        localPlayerType = PlayerType.Cross;
        circleClientId = ulong.MaxValue;
        currentPlayerType.Value = PlayerType.Cross;
        isGameStarted.Value = true;
    }

    public bool TryPlaceAIMove(int x, int y)
    {
        if (!IsServer || !isAIGame || !isGameStarted.Value)
        {
            return false;
        }

        return TryPlaceMove(x, y, PlayerType.Circle);
    }

    public bool IsAIGame()
    {
        return isAIGame;
    }

    public bool IsGameActive()
    {
        return isGameStarted.Value;
    }

    public int GetLobbyPlayerCount()
    {
        if (NetworkManager.Singleton != null && IsServer)
        {
            return NetworkManager.Singleton.ConnectedClientsList.Count;
        }

        return lobbyPlayerCount.Value;
    }

    public bool CanStartGameFromLobby()
    {
        return IsServer && GetLobbyPlayerCount() >= 2;
    }

    public PlayerType GetLocalPlayerType()
    {
        return localPlayerType;
    }

    public PlayerType GetCurrentPlayerType()
    {
        return currentPlayerType.Value;
    }

    public int getCrossScore()
    {
        return playerCrossScore.Value;
    }

    public int getCircleScore()
    {
        return playerCircleScore.Value;
    }

    private PlayerType GetPlayerTypeForClient(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            return PlayerType.None;
        }

        if (clientId == NetworkManager.ServerClientId)
        {
            return PlayerType.Cross;
        }

        if (clientId == circleClientId)
        {
            return PlayerType.Circle;
        }

        return PlayerType.None;
    }

    private bool IsInBoard(int x, int y)
    {
        return x >= 0 && x < BoardPointsX &&
               y >= 0 && y < BoardPointsY;
    }

    private void ResetBoardState()
    {
        for (int x = 0; x < playerTypeArrray.GetLength(0); x++)
        {
            for (int y = 0; y < playerTypeArrray.GetLength(1); y++)
            {
                playerTypeArrray[x, y] = PlayerType.None;
            }
        }

        moveCount = 0;
    }
}
