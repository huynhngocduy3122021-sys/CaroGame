using UnityEngine;
using System;
using Unity.Netcode;



public class GameManager : NetworkBehaviour
{
    public const int BoardPointsX = 31;
    public const int BoardPointsY = 19;

    public static GameManager Instance { get; private set; }

    // tạo 1 cái event để phát tham số x và y khi có 1 điểm bị click
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

    private PlayerType localPlayerType; // xác định loại người chơi hiện tại (Cross hoặc Circle) dựa trên client ID của người chơi khi họ kết nối vào game, để đảm bảo rằng mỗi người chơi sẽ có một loại quân cờ riêng biệt và không bị trùng lặp với người chơi khác.
    private NetworkVariable<PlayerType> currentPlayerType = new NetworkVariable<PlayerType>(PlayerType.None , NetworkVariableReadPermission.Everyone , NetworkVariableWritePermission.Server); // sử dụng NetworkVariable để đồng bộ hóa trạng thái lượt chơi hiện tại giữa server và tất cả client, đảm bảo rằng mọi người chơi đều biết ai đang là người chơi hiện tại và có thể cập nhật giao diện người dùng hoặc thực hiện các hành động khác dựa trên thông tin này.
    private PlayerType[,] playerTypeArrray;// mảng 2 chiều để lưu trữ loại quân cờ (Cross hoặc Circle) đã được đặt ở mỗi vị trí trên bàn cờ, giúp theo dõi trạng thái của bàn cờ và xác định xem có ai đã thắng hay chưa dựa trên các quân cờ đã được đặt.
    private int moveCount = 0;
    private ulong circleClientId = ulong.MaxValue;
    private bool isAIGame;

    private NetworkVariable<int> playerCrossScore = new NetworkVariable<int>();
    private NetworkVariable<int> playerCircleScore = new NetworkVariable<int>();

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("Duplicate GameManager found. Destroying the new one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        playerTypeArrray = new PlayerType[BoardPointsX, BoardPointsY];
    }

    public override void OnNetworkSpawn() // hàm này sẽ được gọi khi GameManager được spawn trên mạng, nó sẽ thiết lập loại người chơi cho mỗi client dựa trên client ID và đăng ký sự kiện để bắt đầu game khi đủ người chơi kết nối vào.
    {
       Debug.Log("GameManager spawned for client: " + NetworkManager.Singleton.LocalClientId);
       if(NetworkManager.Singleton.LocalClientId == 0)
       {
            localPlayerType = PlayerType.Cross;
       }
       else
       {
            localPlayerType = PlayerType.Circle;
       }

       if(IsServer)
       {
            NetworkManager.Singleton.OnClientConnectedCallback += NetworkManager_OnClientConnectedCallback;
            TryStartGame();
       }
       currentPlayerType.OnValueChanged += (PlayerType oldPlayertype , PlayerType newPlayerType) => {
            OnCurrentPlayerType?.Invoke(this, EventArgs.Empty);
       };

        playerCrossScore.OnValueChanged += (int oldScore , int newScore) =>
        {
            OnScoreChanged?.Invoke(this,EventArgs.Empty);
        };
        playerCircleScore.OnValueChanged += (int oldScore, int newScore) =>
        {
            OnScoreChanged?.Invoke(this, EventArgs.Empty);
        };

        OnScoreChanged?.Invoke(this, EventArgs.Empty);


    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= NetworkManager_OnClientConnectedCallback;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void NetworkManager_OnClientConnectedCallback(ulong obj)// hàm này sẽ được gọi mỗi khi có một client mới kết nối vào game, nó sẽ kiểm tra xem đã đủ 2 người chơi kết nối chưa và nếu có thì phát event để bắt đầu game.
    {
        TryStartGame();
    }

    private void TryStartGame()
    {
        if (isAIGame)
        {
            return;
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
    private void TriggerOnGameStartedRpc()
    {
         OnGameStarted?.Invoke(this, EventArgs.Empty);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)] // hàm này sẽ được gọi từ client và thực thi trên server để xử lý logic liên quan đến việc click vào điểm trên bàn cờ, sau đó phát event để các client khác có thể cập nhật giao diện tương ứng
    public void clickedOnGripPositionRpc(int x, int y, RpcParams rpcParams = default)

    {
        Debug.Log("clickedOnGripPosition:" + x + ", " + y);
        if (x < 0 || x >= BoardPointsX || y < 0 || y >= BoardPointsY)
        {
            Debug.LogWarning($"Rejected out-of-range move ({x}, {y}).");
            return;
        }

        PlayerType playerType = GetPlayerTypeForClient(rpcParams.Receive.SenderClientId);
        if (playerType == PlayerType.None)
        {
            Debug.LogWarning($"Rejected move from unassigned client {rpcParams.Receive.SenderClientId}.");
            return;
        }

        TryPlaceMove(x, y, playerType);
    }

    private bool TryPlaceMove(int x, int y, PlayerType playerType)
    {
        if (x < 0 || x >= BoardPointsX || y < 0 || y >= BoardPointsY)
        {
            Debug.LogWarning($"Rejected out-of-range move ({x}, {y}).");
            return false;
        }

        if(playerType != currentPlayerType.Value)
        {
            Debug.Log("It's not your turn!");
            return false;
        }

        if(playerTypeArrray[x, y] != PlayerType.None)
        {
            Debug.Log("This position is already occupied!");
            return false;
        }

        playerTypeArrray[x, y] = playerType;
        moveCount++;
        OnGripPositionClicked?.Invoke(this, new OnGripPositionClickedEventArgs {
             x = x, y = y, playerType = playerType });

        // Chuyển lượt chơi sau khi một điểm được click
        switch(currentPlayerType.Value)
        {
            case PlayerType.Cross:
                currentPlayerType.Value = PlayerType.Circle;
                break;
            case PlayerType.Circle:
                currentPlayerType.Value = PlayerType.Cross;
                break;
        }
        testWinner(x, y); // kiểm tra xem sau khi đặt quân cờ vào vị trí (x, y) thì có người chơi nào thắng hay không

        return true;
    }
   private bool checkLine(int x , int y ,int dirX , int dirY ,PlayerType playerType)
    {
        int count = 1; // Đếm số quân cờ liên tiếp, bắt đầu với 1 vì đã có quân cờ tại (x, y)
        count += CountInDirection(x, y, dirX, dirY, playerType); // Đếm quân cờ theo hướng (dirX, dirY)
        count += CountInDirection(x, y, -dirX, -dirY, playerType); // Đếm quân cờ theo hướng ngược lại (-dirX, -dirY)
        return count >= 5; // Trả về true nếu có ít nhất 5 quân cờ liên tiếp
    }

    private int CountInDirection(int x, int y, int dirX, int dirY, PlayerType playerType)
    {
        int count = 0;
       int checkX = x + dirX;
        int checkY = y + dirY;

        while(
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
    private void testWinner(int x , int y){
        PlayerType playerType = playerTypeArrray[x, y];
        if(playerType == PlayerType.None)
        {
            return;
        }

        if(checkLine(x, y, 1, 0, playerType)) // Kiểm tra hàng ngang
        {
            win(GetWinCenter(x, y, 1, 0, playerType), Orientation.Horizontal);
            
        } else if(checkLine(x, y, 0, 1, playerType)) // Kiểm tra hàng dọc
        {
            win(GetWinCenter(x, y, 0, 1, playerType), Orientation.Vertical);
        }
        else if(checkLine(x, y, 1, 1, playerType)) // Kiểm tra đường chéo chính
        {
            win(GetWinCenter(x, y, 1, 1, playerType), Orientation.DiagonalA);
        }
        else if(checkLine(x, y, 1, -1, playerType)) // Kiểm tra đường chéo phụ
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
         

            PlayerType winnerType = playerTypeArrray[centerPos.x , centerPos.y];
            switch (winnerType)
        {
            case PlayerType.Cross:
                playerCrossScore.Value++;
                break;
            case PlayerType.Circle:
                playerCircleScore.Value++;
                break;
        }

            currentPlayerType.Value = PlayerType.None; // Đặt lượt chơi về None để kết thúc game
            TriggerOnGameWinRpc(centerPos, orientation, winnerType); // Phát event để thông báo cho các client khác về việc có người chơi đã thắng và vị trí trung tâm của đường thắng cùng với hướng của đường thắng
           
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
         OnGameWin?.Invoke(this, new OnGameWinEventArgs { 
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

    // Đi về một phía
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

    // Đi về phía ngược lại
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

    return new Vector2Int( 
        (minX + maxX) / 2,
        (minY + maxY) / 2
    );
}
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
public void RematchRpc(RpcParams rpcParams = default)
    {
        if (GetPlayerTypeForClient(rpcParams.Receive.SenderClientId) == PlayerType.None)
        {
            Debug.LogWarning($"Rejected rematch request from unassigned client {rpcParams.Receive.SenderClientId}.");
            return;
        }

        for(int x = 0 ; x < playerTypeArrray.GetLength(0) ; x++)
        {
            for(int y = 0 ; y < playerTypeArrray.GetLength(1) ; y++)
            {
                playerTypeArrray[x, y] = PlayerType.None;
            }
        }
        moveCount = 0;
        currentPlayerType.Value = PlayerType.Cross; // Đặt lượt chơi đầu tiên là Cross
        TriggerOnRematchRpc();
    }
    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerOnRematchRpc()
    {
        OnRematch?.Invoke(this, EventArgs.Empty);
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

    public void StartAIGame()
    {
        if (!IsServer)
        {
            Debug.LogError("Only the server can start an AI game.");
            return;
        }

        isAIGame = true;
        circleClientId = ulong.MaxValue;
        localPlayerType = PlayerType.Cross;
        currentPlayerType.Value = PlayerType.Cross;
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

    private PlayerType GetPlayerTypeForClient(ulong clientId)
    {
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
}
