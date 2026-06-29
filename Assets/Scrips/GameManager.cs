using UnityEngine;
using System;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    // Tạo 1 cái event để phát tham số x và y khi có 1 điểm bị click
    public event EventHandler<OnGripPositionClickedEventArgs> OnGripPositionClicked;
    public class OnGripPositionClickedEventArgs : EventArgs // EventArgs là class chứa dữ liệu của event, ở đây mình tạo 1 class con để chứa x và y
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

    // Event thông báo khi danh sách người chơi trong Lobby thay đổi (dùng cho UI Lobby)
    public event EventHandler OnLobbyPlayersChanged;

    public enum PlayerType
    {
        None,        // Được dùng làm Khán giả (Spectator / View)
        Cross,       // Người chơi 1
        Circle,      // Người chơi 2
    }
    public enum Orientation
    {
        Horizontal,
        Vertical,
        DiagonalA,
        DiagonalB,
    }

    private PlayerType localPlayerType = PlayerType.None; // Xác định loại người chơi hiện tại của Client này
    private NetworkVariable<PlayerType> currentPlayerType = new NetworkVariable<PlayerType>(PlayerType.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server); // Đồng bộ hóa trạng thái lượt chơi hiện tại
    private PlayerType[,] playerTypeArrray; // Mảng 2 chiều lưu trữ trạng thái bàn cờ
    private int moveCount = 0;
    private const int BOARD_WIDTH = 31;
    private const int BOARD_HEIGHT = 19;
    private ulong circleClientId = ulong.MaxValue;
    private bool isAIGame;

    private NetworkVariable<int> playerCrossScore = new NetworkVariable<int>();
    private NetworkVariable<int> playerCircleScore = new NetworkVariable<int>();

    // Biến mạng kiểm tra xem Game đã bắt đầu từ Lobby hay chưa
    private NetworkVariable<bool> isGameStarted = new NetworkVariable<bool>(false);
    private NetworkVariable<int> lobbyPlayerCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        Instance = this;
        playerTypeArrray = new PlayerType[BOARD_WIDTH + 1, BOARD_HEIGHT + 1];
    }

    public override void OnNetworkSpawn() // Hàm này sẽ được gọi khi GameManager được spawn trên mạng
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

        // Lắng nghe biến isGameStarted đổi màu/trạng thái để kích hoạt hiển thị bàn cờ trên tất cả các máy
        isGameStarted.OnValueChanged += IsGameStarted_OnValueChanged;

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
        // Khi có client mới kết nối, tính toán số lượng người trong phòng để phân vai trò
        int playerIndex = NetworkManager.Singleton.ConnectedClientsList.Count - 1;

        PlayerType assignedType = PlayerType.None; // Mặc định từ người thứ 3 trở đi là Khán Giả (None)

        if (playerIndex == 0) assignedType = PlayerType.Cross;
        else if (playerIndex == 1) assignedType = PlayerType.Circle;

        // Chỉ định vai trò cụ thể cho Client vừa kết nối vào
        AssignPlayerTypeRpc(clientId, assignedType);

        // Cập nhật giao diện Lobby cho toàn bộ phòng
        RefreshLobbyPlayerCount();
    }

    private void Server_OnClientDisconnectedCallback(ulong clientId)
    {
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

        if (NetworkManager.Singleton.ConnectedClientsList.Count != 2)
        {
            return;
        }

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != NetworkManager.ServerClientId)
            {
                circleClientId = client.ClientId;
                break;
            }
        }

        currentPlayerType.Value = PlayerType.Cross;
        TriggerOnGameStartedRpc();
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

    // Hàm này sẽ được gán vào Nút "START GAME" trên UI Lobby (Chỉ Host bấm được)
    public void StartGameFromLobby()
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton.ConnectedClientsList.Count >= 2)
        {
            isGameStarted.Value = true;
            currentPlayerType.Value = PlayerType.Cross; // Đặt lượt chơi đầu tiên là Cross
        }
        else
        {
            Debug.Log("Chưa đủ 2 người chơi chính, không thể Start Game!");
        }
    }

    [Rpc(SendTo.Server)]
    public void clickedOnGripPositionRpc(int x, int y, PlayerType playerType)
    {
        // Nếu Game chưa bắt đầu hoặc người bấm là Khán giả (None) thì chặn tại Server
        if (!isGameStarted.Value || playerType == PlayerType.None) return;

        Debug.Log("clickedOnGripPosition:" + x + ", " + y);
        if (playerType != currentPlayerType.Value)
        {
            Debug.Log("It's not your turn!");
            return false;
        }

        if (!IsInBoard(x, y))
        {
            Debug.Log("This position is outside the board!");
            return;
        }

        if (playerTypeArrray[x, y] != PlayerType.None)
        {
            Debug.Log("This position is already occupied!");
            return;
        }

        playerTypeArrray[x, y] = playerType;
        moveCount++;
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
            IsInBoard(checkX, checkY) &&
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
        if (moveCount >= (BOARD_WIDTH + 1) * (BOARD_HEIGHT + 1))
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
            IsInBoard(checkX, checkY) &&
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
            IsInBoard(checkX, checkY) &&
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

    [Rpc(SendTo.Server)]
    public void RematchRpc()
    {
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

    private bool IsInBoard(int x, int y)
    {
        return x >= 0 && x <= BOARD_WIDTH && y >= 0 && y <= BOARD_HEIGHT;
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
