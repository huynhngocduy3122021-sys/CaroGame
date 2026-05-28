using UnityEngine;
using System;
using Unity.Netcode;


public class GameManager : NetworkBehaviour
{
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
    }
    public event EventHandler OnCurrentPlayerType;
    public enum PlayerType
    {
        None,
        Cross,
        Circle,
    }

    private PlayerType localPlayerType; // xác định loại người chơi hiện tại (Cross hoặc Circle) dựa trên client ID của người chơi khi họ kết nối vào game, để đảm bảo rằng mỗi người chơi sẽ có một loại quân cờ riêng biệt và không bị trùng lặp với người chơi khác.
    private NetworkVariable<PlayerType> currentPlayerType = new NetworkVariable<PlayerType>(PlayerType.None , NetworkVariableReadPermission.Everyone , NetworkVariableWritePermission.Server); // sử dụng NetworkVariable để đồng bộ hóa trạng thái lượt chơi hiện tại giữa server và tất cả client, đảm bảo rằng mọi người chơi đều biết ai đang là người chơi hiện tại và có thể cập nhật giao diện người dùng hoặc thực hiện các hành động khác dựa trên thông tin này.
    private PlayerType[,] playerTypeArrray;// mảng 2 chiều để lưu trữ loại quân cờ (Cross hoặc Circle) đã được đặt ở mỗi vị trí trên bàn cờ, giúp theo dõi trạng thái của bàn cờ và xác định xem có ai đã thắng hay chưa dựa trên các quân cờ đã được đặt.

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.Log("Duplicate GameManager found. Destroying the new one.");        
            
        }
        Instance = this;

        playerTypeArrray = new PlayerType[30, 18];
    }

    public override void OnNetworkSpawn()
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
       }
       currentPlayerType.OnValueChanged += (PlayerType oldPlayertype , PlayerType newPlayerType) => {
            OnCurrentPlayerType?.Invoke(this, EventArgs.Empty);
       };
    }

    private void NetworkManager_OnClientConnectedCallback(ulong obj)
    {
        if(NetworkManager.Singleton.ConnectedClientsList.Count == 2) // khi có đủ 2 người chơi kết nối vào game thì phát event onGameStarted để bắt đầu game
        {
            // stated game
            currentPlayerType.Value = PlayerType.Cross; // đặt lượt chơi đầu tiên là Cross
            TriggerOnGameStartedRpc();
        }
    }
    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerOnGameStartedRpc()
    {
         OnGameStarted?.Invoke(this, EventArgs.Empty);
    }
    [Rpc(SendTo.Server)] // hàm này sẽ được gọi từ client và thực thi trên server để xử lý logic liên quan đến việc click vào điểm trên bàn cờ, sau đó phát event để các client khác có thể cập nhật giao diện tương ứng
    public void clickedOnGripPositionRpc(int x , int y , PlayerType playerType)

    {
        Debug.Log("clickedOnGripPosition:" + x + ", " + y);
        if(playerType != currentPlayerType.Value)
        {
            Debug.Log("It's not your turn!");
            return;
        }

            if(playerTypeArrray[x, y] != PlayerType.None)
            {
                Debug.Log("This position is already occupied!");
                return;
            }

            playerTypeArrray[x, y] = playerType; // cập nhật mảng để đánh dấu vị trí đã được đặt quân cờ
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
            checkX < 30 &&
            checkY >= 0 &&
            checkY < 18 &&
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

        if(
            checkLine(x, y, 1, 0, playerType) || // Kiểm tra hàng ngang
            checkLine(x, y, 0, 1, playerType) || // Kiểm tra hàng dọc
            checkLine(x, y, 1, 1, playerType) || // Kiểm tra đường chéo chính
            checkLine(x, y, 1, -1, playerType)   // Kiểm tra đường chéo phụ
        )
        {
            Debug.Log(playerType + " wins!");
            currentPlayerType.Value = PlayerType.None; // Đặt lượt chơi về None để kết thúc game
            OnGameWin?.Invoke(this, new OnGameWinEventArgs { centerGridposition = new Vector2Int(x, y) });
        }
    }
 
    public PlayerType GetLocalPlayerType()
    {
        return localPlayerType;
    }
    public PlayerType GetCurrentPlayerType()
    {
        return currentPlayerType.Value;
    }
}
