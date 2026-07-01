using UnityEngine;
using System;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    public const int BoardPointsX = 31;
    public const int BoardPointsY = 19;

    public static GameManager Instance { get; private set; }

    // Sự kiện phát tọa độ (x, y) khi có ô bị click
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
        None,        // Khán giả (Spectator)
        Cross,       // Người chơi 1 (X)
        Circle,      // Người chơi 2 (O)
    }
    public enum Orientation
    {
        Horizontal,
        Vertical,
        DiagonalA,
        DiagonalB,
    }

    private PlayerType localPlayerType = PlayerType.None;
    private NetworkVariable<PlayerType> currentPlayerType = new NetworkVariable<PlayerType>(PlayerType.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private PlayerType[,] playerTypeArrray;
    private int moveCount = 0;
    private NetworkVariable<ulong> circleClientId = new NetworkVariable<ulong>(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private bool isAIGame;

    private NetworkVariable<int> playerCrossScore = new NetworkVariable<int>();
    private NetworkVariable<int> playerCircleScore = new NetworkVariable<int>();

    private NetworkVariable<bool> isGameStarted = new NetworkVariable<bool>(false);
    public NetworkVariable<Vector2Int> lastMovePosition = new NetworkVariable<Vector2Int>(new Vector2Int(-1, -1), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> lobbyPlayerCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
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

            // Host (ClientId = 0) mặc định luôn là Cross
            AssignPlayerTypeRpc(NetworkManager.Singleton.LocalClientId, PlayerType.Cross);
            RefreshLobbyPlayerCount();
        }

        lobbyPlayerCount.OnValueChanged += LobbyPlayerCount_OnValueChanged;
        currentPlayerType.OnValueChanged += CurrentPlayerType_OnValueChanged;
        playerCrossScore.OnValueChanged += Score_OnValueChanged;
        playerCircleScore.OnValueChanged += Score_OnValueChanged;
        isGameStarted.OnValueChanged += IsGameStarted_OnValueChanged;
        circleClientId.OnValueChanged += CircleClientId_OnValueChanged;

        localPlayerType = GetLocalPlayerType();
        OnScoreChanged?.Invoke(this, EventArgs.Empty);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= Server_OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback -= Server_OnClientDisconnectedCallback;
        }

        lobbyPlayerCount.OnValueChanged -= LobbyPlayerCount_OnValueChanged;
        currentPlayerType.OnValueChanged -= CurrentPlayerType_OnValueChanged;
        playerCrossScore.OnValueChanged -= Score_OnValueChanged;
        playerCircleScore.OnValueChanged -= Score_OnValueChanged;
        isGameStarted.OnValueChanged -= IsGameStarted_OnValueChanged;
        circleClientId.OnValueChanged -= CircleClientId_OnValueChanged;
    }

    private void LobbyPlayerCount_OnValueChanged(int oldCount, int newCount)
    {
        OnLobbyPlayersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CircleClientId_OnValueChanged(ulong oldVal, ulong newVal)
    {
        localPlayerType = GetLocalPlayerType();
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

    private void IsGameStarted_OnValueChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            OnGameStarted?.Invoke(this, EventArgs.Empty);
        }
        else if (oldVal)
        {
            OnGameReturnedToLobby?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Server_OnClientConnectedCallback(ulong clientId)
    {
        if (clientId != NetworkManager.ServerClientId)
        {
            if (circleClientId.Value == ulong.MaxValue)
            {
                circleClientId.Value = clientId;
                Debug.Log($"Server: Assigned circleClientId = {clientId} (Circle)");
                AssignPlayerTypeRpc(clientId, PlayerType.Circle);
            }
            else
            {
                Debug.Log($"Server: Client {clientId} joined as spectator (Circle is already {circleClientId.Value})");
                AssignPlayerTypeRpc(clientId, PlayerType.None);
            }
        }
        else
        {
            AssignPlayerTypeRpc(clientId, PlayerType.Cross);
        }
        RefreshLobbyPlayerCount();
    }

    private void Server_OnClientDisconnectedCallback(ulong clientId)
    {
        if (clientId == circleClientId.Value)
        {
            circleClientId.Value = ulong.MaxValue;
            Debug.Log("Server: Circle player disconnected, resetting circleClientId.");
        }

        if (isGameStarted.Value && NetworkManager.Singleton.ConnectedClientsList.Count < 2)
        {
            ReturnToLobbyAfterPlayerExit();
        }

        RefreshLobbyPlayerCount();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AssignPlayerTypeRpc(ulong clientId, PlayerType type)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            localPlayerType = type;
            Debug.Log("Vai trò của bạn trong phòng này là: " + localPlayerType);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerOnGameStartedRpc()
    {
        OnGameStarted?.Invoke(this, EventArgs.Empty);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyLobbyChangedRpc()
    {
        OnLobbyPlayersChanged?.Invoke(this, EventArgs.Empty);
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

    private void ReturnToLobbyAfterPlayerExit()
    {
        ResetBoardState();
        currentPlayerType.Value = PlayerType.None;
        TriggerOnRematchRpc();
        isGameStarted.Value = false;
    }

    public void StartGameFromLobby()
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton.ConnectedClientsList.Count >= 2)
        {
            isGameStarted.Value = true;
            currentPlayerType.Value = PlayerType.Cross;
        }
        else
        {
            Debug.Log("Chưa đủ 2 người chơi chính, không thể Start Game!");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void clickedOnGripPositionRpc(int x, int y, RpcParams rpcParams = default)
    {
        Debug.Log($"GameManager: clickedOnGripPositionRpc received ({x}, {y}) from client {rpcParams.Receive.SenderClientId} | isGameStarted: {isGameStarted.Value} | CurrentTurn: {currentPlayerType.Value}");

        if (!isGameStarted.Value) return;

        if (x < 0 || x >= BoardPointsX || y < 0 || y >= BoardPointsY)
        {
            Debug.LogWarning($"Rejected out-of-range move ({x}, {y}).");
            return;
        }

        PlayerType playerType = GetPlayerTypeForClient(rpcParams.Receive.SenderClientId);
        Debug.Log($"GameManager: Sender client {rpcParams.Receive.SenderClientId} resolved to playerType {playerType}");

        if (playerType == PlayerType.None)
        {
            Debug.LogWarning($"Rejected move from unassigned client {rpcParams.Receive.SenderClientId}.");
            return;
        }

        TryPlaceMove(x, y, playerType);
    }

    private bool TryPlaceMove(int x, int y, PlayerType playerType)
    {
        Debug.Log($"GameManager: TryPlaceMove({x}, {y}, {playerType}) | currentPlayerType: {currentPlayerType.Value}");

        if (x < 0 || x >= BoardPointsX || y < 0 || y >= BoardPointsY)
        {
            Debug.LogWarning($"Rejected out-of-range move ({x}, {y}).");
            return false;
        }

        if (playerType != currentPlayerType.Value)
        {
            Debug.Log($"It's not your turn! Player: {playerType} | CurrentTurn: {currentPlayerType.Value}");
            return false;
        }

        if (playerTypeArrray[x, y] != PlayerType.None)
        {
            Debug.Log("This position is already occupied!");
            return false;
        }

        playerTypeArrray[x, y] = playerType;
        moveCount++;
        lastMovePosition.Value = new Vector2Int(x, y);
        OnGripPositionClicked?.Invoke(this, new OnGripPositionClickedEventArgs
        {
            x = x,
            y = y,
            playerType = playerType
        });

        switch (currentPlayerType.Value)
        {
            case PlayerType.Cross:
                currentPlayerType.Value = PlayerType.Circle;
                break;
            case PlayerType.Circle:
                currentPlayerType.Value = PlayerType.Cross;
                break;
        }
        testWinner(x, y);

        return true;
    }

    private bool checkLine(int x, int y, int dirX, int dirY, PlayerType playerType)
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

        while (
            checkX >= 0 &&
            checkX < BoardPointsX &&
            checkY >= 0 &&
            checkY < BoardPointsY &&
            playerTypeArrray[checkX, checkY] == playerType
        )
        {
            count++;
            checkX += dirX;
            checkY += dirY;
        }

        return count;
    }

    private void testWinner(int x, int y)
    {
        PlayerType playerType = playerTypeArrray[x, y];
        if (playerType == PlayerType.None)
        {
            return;
        }

        if (checkLine(x, y, 1, 0, playerType))
        {
            win(GetWinCenter(x, y, 1, 0, playerType), Orientation.Horizontal);
        }
        else if (checkLine(x, y, 0, 1, playerType))
        {
            win(GetWinCenter(x, y, 0, 1, playerType), Orientation.Vertical);
        }
        else if (checkLine(x, y, 1, 1, playerType))
        {
            win(GetWinCenter(x, y, 1, 1, playerType), Orientation.DiagonalA);
        }
        else if (checkLine(x, y, 1, -1, playerType))
        {
            win(GetWinCenter(x, y, 1, -1, playerType), Orientation.DiagonalB);
        }
        else
        {
            checkDraw();
        }
    }

    private void win(Vector2Int centerPos, Orientation orientation)
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

    private void checkDraw()
    {
        if (moveCount >= BoardPointsX * BoardPointsY)
        {
            Debug.Log("Draw!");
            currentPlayerType.Value = PlayerType.None;
            TriggerOnGameWinRpc(Vector2Int.zero, Orientation.Horizontal, PlayerType.None);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerOnGameWinRpc(Vector2Int centerPos, Orientation orientation, PlayerType playerWinType)
    {
        OnGameWin?.Invoke(this, new OnGameWinEventArgs
        {
            centerGridposition = centerPos,
            orientation = orientation,
            playerWinType = playerWinType
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

        while (
            checkX >= 0 &&
            checkX < BoardPointsX &&
            checkY >= 0 &&
            checkY < BoardPointsY &&
            playerTypeArrray[checkX, checkY] == playerType
        )
        {
            maxX = checkX;
            maxY = checkY;
            checkX += dirX;
            checkY += dirY;
        }

        checkX = x - dirX;
        checkY = y - dirY;

        while (
            checkX >= 0 &&
            checkX < BoardPointsX &&
            checkY >= 0 &&
            checkY < BoardPointsY &&
            playerTypeArrray[checkX, checkY] == playerType
        )
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
        if (GetPlayerTypeForClient(rpcParams.Receive.SenderClientId) == PlayerType.None)
        {
            Debug.LogWarning($"Rejected rematch request from unassigned client {rpcParams.Receive.SenderClientId}.");
            return;
        }

        ResetBoardState();
        currentPlayerType.Value = PlayerType.Cross;
        TriggerOnRematchRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerOnRematchRpc()
    {
        OnRematch?.Invoke(this, EventArgs.Empty);
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
        if (isAIGame)
        {
            return PlayerType.Cross;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            return PlayerType.None;
        }

        ulong localId = NetworkManager.Singleton.LocalClientId;
        if (localId == NetworkManager.ServerClientId)
        {
            return PlayerType.Cross;
        }

        if (circleClientId.Value != ulong.MaxValue && localId == circleClientId.Value)
        {
            return PlayerType.Circle;
        }

        return PlayerType.None;
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

    public void StartAIGame()
    {
        if (!IsServer)
        {
            Debug.LogError("Only the server can start an AI game.");
            return;
        }

        isAIGame = true;
        circleClientId.Value = ulong.MaxValue;
        localPlayerType = PlayerType.Cross;
        currentPlayerType.Value = PlayerType.Cross;
        isGameStarted.Value = true;
        TriggerOnGameStartedRpc();
    }

    public bool TryPlaceAIMove(int x, int y)
    {
        if (!IsServer || !isAIGame)
        {
            return false;
        }

        return TryPlaceMove(x, y, PlayerType.Circle);
    }

    public bool IsAIGame()
    {
        return isAIGame;
    }

    public PlayerType GetPlayerTypeAtPosition(int x, int y)
    {
        if (x < 0 || x >= BoardPointsX || y < 0 || y >= BoardPointsY)
        {
            return PlayerType.None;
        }
        return playerTypeArrray[x, y];
    }

    private PlayerType GetPlayerTypeForClient(ulong clientId)
    {
        if (clientId == NetworkManager.ServerClientId)
        {
            return PlayerType.Cross;
        }

        if (clientId == circleClientId.Value)
        {
            return PlayerType.Circle;
        }

        return PlayerType.None;
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
        if (IsServer)
        {
            lastMovePosition.Value = new Vector2Int(-1, -1);
        }
    }
}
