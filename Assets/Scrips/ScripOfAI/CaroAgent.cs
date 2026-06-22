using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class CaroAgent : Agent
{
    private const int PointsX = 31;
    private const int PointsY = 19;
    private const int TotalPoints = PointsX * PointsY;
    private const int AI = 1;
    private const int Opponent = -1;

    private readonly int[,] board = new int[PointsX, PointsY];
    private int moveCount;
    private int episodeCount;
    private int bestAILine;
    private StatsRecorder statsRecorder;

    public override void Initialize()
    {
        statsRecorder = Academy.Instance.StatsRecorder;
    }

    public override void OnEpisodeBegin()
    {
        System.Array.Clear(board, 0, board.Length);
        moveCount = 0;
        bestAILine = 0;

        // Gameplay thật cho người chơi đi trước. Luân phiên thứ tự để model
        // học được cả hai phân phối trạng thái và không phụ thuộc nước mở đầu.
        bool opponentStarts = episodeCount % 2 == 1;
        episodeCount++;

        if (opponentStarts)
        {
            PlaceOpponentOpening();
        }
    }

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

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        bool[,] allowedMoves = BuildAllowedMoveMask();

        for (int y = 0; y < PointsY; y++)
        {
            for (int x = 0; x < PointsX; x++)
            {
                if (!allowedMoves[x, y])
                {
                    actionMask.SetActionEnabled(0, ToAction(x, y), false);
                }
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];
        int x = action % PointsX;
        int y = action / PointsX;

        if (!IsEmpty(x, y))
        {
            AddReward(-1f);
            statsRecorder.Add("Caro/InvalidMove", 1f);
            EndEpisode();
            return;
        }

        bool blockedImmediateLoss = IsWinningMove(x, y, Opponent);
        board[x, y] = AI;
        moveCount++;
        AddReward(-0.0005f);

        if (blockedImmediateLoss)
        {
            AddReward(0.2f);
            statsRecorder.Add("Caro/BlockedImmediateWin", 1f);
        }

        int currentBestLine = GetLongestLine(AI);
        if (currentBestLine > bestAILine)
        {
            AddReward(GetLineProgressReward(currentBestLine) -
                      GetLineProgressReward(bestAILine));
            bestAILine = currentBestLine;
        }

        if (CheckWin(x, y, AI))
        {
            AddReward(1f);
            statsRecorder.Add("Caro/AIWin", 1f);
            EndEpisode();
            return;
        }

        if (moveCount >= TotalPoints)
        {
            AddReward(0.05f);
            statsRecorder.Add("Caro/Draw", 1f);
            EndEpisode();
            return;
        }

        OpponentMove();
    }

    private void OpponentMove()
    {
        if (!TryChooseOpponentMove(out int x, out int y))
        {
            AddReward(0.05f);
            statsRecorder.Add("Caro/Draw", 1f);
            EndEpisode();
            return;
        }

        board[x, y] = Opponent;
        moveCount++;

        if (CheckWin(x, y, Opponent))
        {
            AddReward(-1f);
            statsRecorder.Add("Caro/OpponentWin", 1f);
            EndEpisode();
            return;
        }

        if (moveCount >= TotalPoints)
        {
            AddReward(0.05f);
            statsRecorder.Add("Caro/Draw", 1f);
            EndEpisode();
        }
    }

    private bool TryChooseOpponentMove(out int selectedX, out int selectedY)
    {
        // Curriculum thực sự: giai đoạn đầu đối thủ chủ yếu đánh ngẫu nhiên
        // gần các quân đã có. Khả năng thắng/chặn/heuristic tăng dần.
        const int easyPhaseSteps = 500000;
        float progress = Mathf.Clamp01(
            (Academy.Instance.StepCount - easyPhaseSteps) / 2500000f);
        float useWinningMoveChance = Mathf.Lerp(0f, 1f, progress);
        float useBlockingMoveChance = Mathf.Lerp(0f, 0.95f, progress);
        float useHeuristicChance = Mathf.Lerp(0f, 0.95f, progress);

        if (Random.value < useWinningMoveChance &&
            TryFindImmediateWinningMove(Opponent, out selectedX, out selectedY))
        {
            return true;
        }

        if (Random.value < useBlockingMoveChance &&
            TryFindImmediateWinningMove(AI, out selectedX, out selectedY))
        {
            return true;
        }

        List<Vector2Int> candidates = GetNearbyEmptyCells();
        if (candidates.Count == 0)
        {
            return TryFindAnyEmptyCell(out selectedX, out selectedY);
        }

        if (Random.value > useHeuristicChance)
        {
            Vector2Int randomMove = candidates[Random.Range(0, candidates.Count)];
            selectedX = randomMove.x;
            selectedY = randomMove.y;
            return true;
        }

        float bestScore = float.NegativeInfinity;
        Vector2Int bestMove = candidates[0];

        foreach (Vector2Int candidate in candidates)
        {
            float attackScore = EvaluateMove(candidate.x, candidate.y, Opponent);
            float defenseScore = EvaluateMove(candidate.x, candidate.y, AI);
            float centerDistance =
                Mathf.Abs(candidate.x - PointsX / 2f) +
                Mathf.Abs(candidate.y - PointsY / 2f);

            float score =
                attackScore +
                defenseScore * 0.9f -
                centerDistance * 0.002f +
                Random.Range(0f, 0.01f);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = candidate;
            }
        }

        selectedX = bestMove.x;
        selectedY = bestMove.y;
        return true;
    }

    private bool[,] BuildAllowedMoveMask()
    {
        var allowed = new bool[PointsX, PointsY];
        bool hasPlacedPiece = moveCount > 0;
        int allowedCount = 0;

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
            foreach (Vector2Int candidate in GetNearbyEmptyCells())
            {
                allowed[candidate.x, candidate.y] = true;
                allowedCount++;
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

    private float GetLineProgressReward(int lineLength)
    {
        switch (lineLength)
        {
            case 2:
                return 0.01f;
            case 3:
                return 0.04f;
            case 4:
                return 0.12f;
            case 5:
                return 0.25f;
            default:
                return 0f;
        }
    }

    private float EvaluateMove(int x, int y, int player)
    {
        board[x, y] = player;

        int longest = GetLongestLineThrough(x, y, player);
        int openEnds = CountOpenEnds(x, y, player);

        board[x, y] = 0;

        float lineScore = longest * longest;
        return lineScore + openEnds * 0.75f;
    }

    private int CountOpenEnds(int x, int y, int player)
    {
        int openEnds = 0;
        openEnds += CountOpenEndsForDirection(x, y, 1, 0, player);
        openEnds += CountOpenEndsForDirection(x, y, 0, 1, player);
        openEnds += CountOpenEndsForDirection(x, y, 1, 1, player);
        openEnds += CountOpenEndsForDirection(x, y, 1, -1, player);
        return openEnds;
    }

    private int CountOpenEndsForDirection(
        int x,
        int y,
        int dx,
        int dy,
        int player)
    {
        int openEnds = 0;

        if (IsEndOpen(x, y, dx, dy, player))
        {
            openEnds++;
        }

        if (IsEndOpen(x, y, -dx, -dy, player))
        {
            openEnds++;
        }

        return openEnds;
    }

    private bool IsEndOpen(int x, int y, int dx, int dy, int player)
    {
        int checkX = x + dx;
        int checkY = y + dy;

        while (IsInside(checkX, checkY) && board[checkX, checkY] == player)
        {
            checkX += dx;
            checkY += dy;
        }

        return IsInside(checkX, checkY) && board[checkX, checkY] == 0;
    }

    private List<Vector2Int> GetNearbyEmptyCells()
    {
        var candidates = new List<Vector2Int>();
        var added = new bool[PointsX, PointsY];

        for (int y = 0; y < PointsY; y++)
        {
            for (int x = 0; x < PointsX; x++)
            {
                if (board[x, y] == 0)
                {
                    continue;
                }

                for (int offsetY = -2; offsetY <= 2; offsetY++)
                {
                    for (int offsetX = -2; offsetX <= 2; offsetX++)
                    {
                        int candidateX = x + offsetX;
                        int candidateY = y + offsetY;

                        if (IsEmpty(candidateX, candidateY) &&
                            !added[candidateX, candidateY])
                        {
                            added[candidateX, candidateY] = true;
                            candidates.Add(new Vector2Int(candidateX, candidateY));
                        }
                    }
                }
            }
        }

        return candidates;
    }

    private bool TryFindImmediateWinningMove(
        int player,
        out int selectedX,
        out int selectedY)
    {
        var winningMoves = new List<Vector2Int>();

        for (int y = 0; y < PointsY; y++)
        {
            for (int x = 0; x < PointsX; x++)
            {
                if (IsWinningMove(x, y, player))
                {
                    winningMoves.Add(new Vector2Int(x, y));
                }
            }
        }

        if (winningMoves.Count == 0)
        {
            selectedX = -1;
            selectedY = -1;
            return false;
        }

        Vector2Int selected = winningMoves[Random.Range(0, winningMoves.Count)];
        selectedX = selected.x;
        selectedY = selected.y;
        return true;
    }

    private bool IsWinningMove(int x, int y, int player)
    {
        if (!IsEmpty(x, y))
        {
            return false;
        }

        board[x, y] = player;
        bool wins = CheckWin(x, y, player);
        board[x, y] = 0;
        return wins;
    }

    private void PlaceOpponentOpening()
    {
        int centerX = PointsX / 2;
        int centerY = PointsY / 2;
        int x = Mathf.Clamp(centerX + Random.Range(-2, 3), 0, PointsX - 1);
        int y = Mathf.Clamp(centerY + Random.Range(-2, 3), 0, PointsY - 1);

        board[x, y] = Opponent;
        moveCount = 1;
    }

    private bool TryFindAnyEmptyCell(out int selectedX, out int selectedY)
    {
        var emptyCells = new List<Vector2Int>();

        for (int y = 0; y < PointsY; y++)
        {
            for (int x = 0; x < PointsX; x++)
            {
                if (board[x, y] == 0)
                {
                    emptyCells.Add(new Vector2Int(x, y));
                }
            }
        }

        if (emptyCells.Count == 0)
        {
            selectedX = -1;
            selectedY = -1;
            return false;
        }

        Vector2Int selected = emptyCells[Random.Range(0, emptyCells.Count)];
        selectedX = selected.x;
        selectedY = selected.y;
        return true;
    }

    private int GetLongestLine(int player)
    {
        int longest = 0;

        for (int y = 0; y < PointsY; y++)
        {
            for (int x = 0; x < PointsX; x++)
            {
                if (board[x, y] == player)
                {
                    longest = Mathf.Max(
                        longest,
                        GetLongestLineThrough(x, y, player));
                }
            }
        }

        return longest;
    }

    private int GetLongestLineThrough(int x, int y, int player)
    {
        int longest = 1;
        longest = Mathf.Max(longest, CountLine(x, y, 1, 0, player));
        longest = Mathf.Max(longest, CountLine(x, y, 0, 1, player));
        longest = Mathf.Max(longest, CountLine(x, y, 1, 1, player));
        longest = Mathf.Max(longest, CountLine(x, y, 1, -1, player));
        return longest;
    }

    private int CountLine(int x, int y, int dx, int dy, int player)
    {
        return 1 +
               CountDirection(x, y, dx, dy, player) +
               CountDirection(x, y, -dx, -dy, player);
    }

    private bool CheckWin(int x, int y, int player)
    {
        return CountLine(x, y, 1, 0, player) >= 5 ||
               CountLine(x, y, 0, 1, player) >= 5 ||
               CountLine(x, y, 1, 1, player) >= 5 ||
               CountLine(x, y, 1, -1, player) >= 5;
    }

    private int CountDirection(
        int x,
        int y,
        int dx,
        int dy,
        int player)
    {
        int count = 0;
        int currentX = x + dx;
        int currentY = y + dy;

        while (IsInside(currentX, currentY) &&
               board[currentX, currentY] == player)
        {
            count++;
            currentX += dx;
            currentY += dy;
        }

        return count;
    }

    private bool IsEmpty(int x, int y)
    {
        return IsInside(x, y) && board[x, y] == 0;
    }

    private bool IsInside(int x, int y)
    {
        return x >= 0 && x < PointsX && y >= 0 && y < PointsY;
    }

    private int ToAction(int x, int y)
    {
        return y * PointsX + x;
    }
}
