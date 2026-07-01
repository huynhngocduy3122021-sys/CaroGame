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
    private const int PointsX = GameManager.BoardPointsX;
    private const int PointsY = GameManager.BoardPointsY;
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

        if (!gameManager.IsAIGame())
        {
            Debug.Log("CaroGameplayAgent: Chế độ AI không kích hoạt, tắt script AI.");
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
        var sb = new System.Text.StringBuilder("AI Observations: ");
        for (int y = 0; y < PointsY; y++)
        {
            for (int x = 0; x < PointsX; x++)
            {
                sensor.AddObservation(board[x, y]);
                if (board[x, y] != 0)
                {
                    sb.Append($"({x},{y})={board[x, y]} ");
                }
            }
        }
        Debug.Log(sb.ToString());
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

        if (gameManager == null ||
            gameManager.GetCurrentPlayerType() != GameManager.PlayerType.Circle)
        {
            return;
        }

        int action = actions.DiscreteActions[0];
        int x = action % PointsX;
        int y = action / PointsX;

        // Heuristic override to force blocking player's 3-in-a-row, 4-in-a-row, or taking immediate wins
        if (TryGetBestHeuristicMove(out int heuristicX, out int heuristicY))
        {
            x = heuristicX;
            y = heuristicY;
            Debug.Log($"[AI Heuristic Override] Playing ({x}, {y}) to block or win.");
        }
        else
        {
            if (!IsEmpty(x, y))
            {
                if (!TryFindAllowedCell(out x, out y))
                {
                    return;
                }
            }
        }

        if (!gameManager.TryPlaceAIMove(x, y))
        {
            Debug.LogWarning($"AI move ({x}, {y}) was rejected.");
        }
    }

    private bool TryGetBestHeuristicMove(out int bestX, out int bestY)
    {
        bestX = -1;
        bestY = -1;
        int highestScore = -1;

        for (int y = 0; y < PointsY; y++)
        {
            for (int x = 0; x < PointsX; x++)
            {
                if (board[x, y] != 0) continue;

                int aiScore = GetCellScoreForPlayer(x, y, 1);
                int playerScore = GetCellScoreForPlayer(x, y, -1);

                int cellScore = 0;

                // 1. AI immediate win (5-in-a-row)
                if (aiScore >= 100000)
                {
                    cellScore = 1000000;
                }
                // 2. Block player immediate win (5-in-a-row)
                else if (playerScore >= 100000)
                {
                    cellScore = 500000;
                }
                // 3. Block player open 4-in-a-row
                else if (playerScore >= 10000)
                {
                    cellScore = 200000;
                }
                // 4. AI open 4-in-a-row
                else if (aiScore >= 10000)
                {
                    cellScore = 100000;
                }
                // 5. Block player closed 4-in-a-row or open 3-in-a-row (very high threat)
                else if (playerScore >= 500)
                {
                    cellScore = 50000;
                }
                // 6. AI closed 4-in-a-row or open 3-in-a-row
                else if (aiScore >= 500)
                {
                    cellScore = 20000;
                }

                if (cellScore > 0)
                {
                    int combinedScore = cellScore + aiScore + playerScore;
                    if (combinedScore > highestScore)
                    {
                        highestScore = combinedScore;
                        bestX = x;
                        bestY = y;
                    }
                }
            }
        }

        return highestScore > 0;
    }

    private int GetCellScoreForPlayer(int x, int y, int player)
    {
        int maxScore = 0;
        int[] dirsX = { 1, 0, 1, 1 };
        int[] dirsY = { 0, 1, 1, -1 };

        for (int i = 0; i < 4; i++)
        {
            int dx = dirsX[i];
            int dy = dirsY[i];

            int count = 1;
            int openEnds = 0;

            // Positive direction
            int curX = x + dx;
            int curY = y + dy;
            while (curX >= 0 && curX < PointsX && curY >= 0 && curY < PointsY && board[curX, curY] == player)
            {
                count++;
                curX += dx;
                curY += dy;
            }
            if (curX >= 0 && curX < PointsX && curY >= 0 && curY < PointsY && board[curX, curY] == 0)
            {
                openEnds++;
            }

            // Negative direction
            curX = x - dx;
            curY = y - dy;
            while (curX >= 0 && curX < PointsX && curY >= 0 && curY < PointsY && board[curX, curY] == player)
            {
                count++;
                curX -= dx;
                curY -= dy;
            }
            if (curX >= 0 && curX < PointsX && curY >= 0 && curY < PointsY && board[curX, curY] == 0)
            {
                openEnds++;
            }

            int score = 0;
            if (count >= 5) score = 100000;
            else if (count == 4)
            {
                if (openEnds == 2) score = 10000;
                else if (openEnds == 1) score = 1000;
            }
            else if (count == 3)
            {
                if (openEnds == 2) score = 500;
                else if (openEnds == 1) score = 50;
            }
            else if (count == 2)
            {
                if (openEnds == 2) score = 10;
                else if (openEnds == 1) score = 1;
            }

            if (score > maxScore)
            {
                maxScore = score;
            }
        }

        return maxScore;
    }

    // OnMovePlaced: Cập nhật nước đi của người chơi hoặc AI vào bảng dữ liệu nội bộ của Agent để đồng bộ trạng thái
    private void OnMovePlaced(
        object sender,
        GameManager.OnGripPositionClickedEventArgs eventArgs)
    {
        if (!IsInBounds(eventArgs.x, eventArgs.y))
        {
            Debug.LogWarning($"Ignoring out-of-range board update ({eventArgs.x}, {eventArgs.y}).");
            return;
        }

        board[eventArgs.x, eventArgs.y] =
            eventArgs.playerType == GameManager.PlayerType.Circle ? 1 : -1;

        if (eventArgs.playerType == GameManager.PlayerType.Cross &&
            !waitingForDecision)
        {
            waitingForDecision = true;
            StartCoroutine(RequestDecisionAfterGameManagerSettles());
        }
    }

    private System.Collections.IEnumerator RequestDecisionAfterGameManagerSettles()
    {
        yield return null;

        if (gameManager == null ||
            gameManager.GetCurrentPlayerType() != GameManager.PlayerType.Circle)
        {
            waitingForDecision = false;
            yield break;
        }

        RequestDecision();
    }

    // OnRematch: Khởi động lại trạng thái bàn cờ của AI khi người chơi chọn chơi ván mới
    private void OnRematch(object sender, System.EventArgs eventArgs)
    {
        System.Array.Clear(board, 0, board.Length);
        waitingForDecision = false;
    }

    private bool IsEmpty(int x, int y)
    {
        return IsInBounds(x, y) &&
               board[x, y] == 0;
    }

    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < PointsX &&
               y >= 0 && y < PointsY;
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
