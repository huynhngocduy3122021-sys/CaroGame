using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

/// <summary>
/// SCRIPT DÙNG ĐỂ ĐÁNH VỚI PLAYER (CHƠI GAME THỰC TẾ)
/// Class này kết nối trực tiếp với GameManager của trò chơi để thu thập nước đi của Player,
/// đưa vào Model đã huấn luyện và thực hiện nước đi của AI lên bàn cờ thật trong game.
/// </summary>
[RequireComponent(typeof(BehaviorParameters))]
public class CaroGameplayAgent : Agent
{
    private const int PointsX = 31;
    private const int PointsY = 19;
    public const int TotalPoints = PointsX * PointsY;

    private readonly int[,] board = new int[PointsX, PointsY];
    private GameManager gameManager;
    private bool initialized;
    private bool waitingForDecision;

    public void Configure(GameManager manager)
    {
        gameManager = manager;
    }

    // Initialize: Kết nối AI với GameManager của scene và đăng ký các sự kiện khi người chơi đánh hoặc chơi lại
    public override void Initialize()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (gameManager == null)
        {
            Debug.LogError("CaroGameplayAgent requires a GameManager.");
            enabled = false;
            return;
        }

        gameManager.OnGripPositionClicked += OnMovePlaced;
        gameManager.OnRematch += OnRematch;
        initialized = true;
    }

    // CollectObservations: Thu thập trạng thái bàn cờ hiện tại từ GameManager để làm đầu vào cho Model AI dự đoán
    public override void CollectObservations(VectorSensor sensor)
    {
        for (int y = 0; y < PointsY; y++)
        {
            for (int x = 0; x < PointsX; x++)
            {
                sensor.AddObservation(board[x, y]);
            }
        }
    }

    // WriteDiscreteActionMask: Chặn các ô đã có quân cờ (không cho phép AI chọn đánh vào những ô này)
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        bool[,] allowedMoves = BuildAllowedMoveMask();

        for (int y = 0; y < PointsY; y++)
        {
            for (int x = 0; x < PointsX; x++)
            {
                if (!allowedMoves[x, y])
                {
                    actionMask.SetActionEnabled(0, y * PointsX + x, false);
                }
            }
        }
    }

    // OnActionReceived: Nhận nước đi do Model quyết định và gọi GameManager thực hiện vẽ quân cờ của AI lên màn hình
    public override void OnActionReceived(ActionBuffers actions)
    {
        waitingForDecision = false;

        int action = actions.DiscreteActions[0];
        int x = action % PointsX;
        int y = action / PointsX;

        if (!IsEmpty(x, y))
        {
            if (!TryFindAllowedCell(out x, out y))
            {
                return;
            }
        }

        if (!gameManager.TryPlaceAIMove(x, y))
        {
            Debug.LogWarning($"AI move ({x}, {y}) was rejected.");
        }
    }

    // OnMovePlaced: Cập nhật nước đi của người chơi hoặc AI vào bảng dữ liệu nội bộ của Agent để đồng bộ trạng thái
    private void OnMovePlaced(
        object sender,
        GameManager.OnGripPositionClickedEventArgs eventArgs)
    {
        board[eventArgs.x, eventArgs.y] =
            eventArgs.playerType == GameManager.PlayerType.Circle ? 1 : -1;

        if (eventArgs.playerType == GameManager.PlayerType.Cross &&
            gameManager.GetCurrentPlayerType() != GameManager.PlayerType.None &&
            !waitingForDecision)
        {
            waitingForDecision = true;
            RequestDecision();
        }
    }

    // OnRematch: Khởi động lại trạng thái bàn cờ của AI khi người chơi chọn chơi ván mới
    private void OnRematch(object sender, System.EventArgs eventArgs)
    {
        System.Array.Clear(board, 0, board.Length);
        waitingForDecision = false;
    }

    private bool IsEmpty(int x, int y)
    {
        return x >= 0 && x < PointsX &&
               y >= 0 && y < PointsY &&
               board[x, y] == 0;
    }

    private bool[,] BuildAllowedMoveMask()
    {
        var allowed = new bool[PointsX, PointsY];
        bool hasPlacedPiece = false;
        int allowedCount = 0;

        for (int y = 0; y < PointsY && !hasPlacedPiece; y++)
        {
            for (int x = 0; x < PointsX; x++)
            {
                if (board[x, y] != 0)
                {
                    hasPlacedPiece = true;
                    break;
                }
            }
        }

        if (!hasPlacedPiece)
        {
            int centerX = PointsX / 2;
            int centerY = PointsY / 2;

            for (int y = centerY - 2; y <= centerY + 2; y++)
            {
                for (int x = centerX - 2; x <= centerX + 2; x++)
                {
                    if (IsEmpty(x, y))
                    {
                        allowed[x, y] = true;
                        allowedCount++;
                    }
                }
            }
        }
        else
        {
            for (int pieceY = 0; pieceY < PointsY; pieceY++)
            {
                for (int pieceX = 0; pieceX < PointsX; pieceX++)
                {
                    if (board[pieceX, pieceY] == 0)
                    {
                        continue;
                    }

                    for (int offsetY = -2; offsetY <= 2; offsetY++)
                    {
                        for (int offsetX = -2; offsetX <= 2; offsetX++)
                        {
                            int candidateX = pieceX + offsetX;
                            int candidateY = pieceY + offsetY;

                            if (IsEmpty(candidateX, candidateY) &&
                                !allowed[candidateX, candidateY])
                            {
                                allowed[candidateX, candidateY] = true;
                                allowedCount++;
                            }
                        }
                    }
                }
            }
        }

        if (allowedCount == 0)
        {
            for (int y = 0; y < PointsY; y++)
            {
                for (int x = 0; x < PointsX; x++)
                {
                    allowed[x, y] = board[x, y] == 0;
                }
            }
        }

        return allowed;
    }

    private bool TryFindAllowedCell(out int emptyX, out int emptyY)
    {
        bool[,] allowedMoves = BuildAllowedMoveMask();

        for (int y = 0; y < PointsY; y++)
        {
            for (int x = 0; x < PointsX; x++)
            {
                if (allowedMoves[x, y])
                {
                    emptyX = x;
                    emptyY = y;
                    return true;
                }
            }
        }

        emptyX = -1;
        emptyY = -1;
        return false;
    }

    private void OnDestroy()
    {
        if (!initialized || gameManager == null)
        {
            return;
        }

        gameManager.OnGripPositionClicked -= OnMovePlaced;
        gameManager.OnRematch -= OnRematch;
    }
}
