using System.Collections.Generic;
using System.Diagnostics;

/// <summary>
/// Thuật toán tìm kiếm Negamax/Alpha-Beta tăng dần độ sâu cho game Caro.
/// Thời gian tìm kiếm được giới hạn để chạy ổn định trên PC và mobile.
/// </summary>
public sealed class CaroAlphaBetaSearch
{
    // Điểm thắng rất lớn để AI luôn ưu tiên thắng hoặc chặn nước thua ngay lập tức.
    private const int WinScore = 100000000;

    // Các tham số giới hạn phạm vi và số nhánh tìm kiếm.
    private readonly int winLength;
    private readonly int candidateRadius;
    private readonly int maxCandidates;
    private Stopwatch stopwatch;
    private bool timedOut;
    private int rootPlayer;
    private int timeLimitMilliseconds;

    /// <summary>Khởi tạo bộ tìm kiếm với luật thắng và giới hạn nhánh.</summary>
    public CaroAlphaBetaSearch(
        int winLength = CaroAiRules.DefaultWinLength,
        int candidateRadius = 2,
        int maxCandidates = 32)
    {
        this.winLength = winLength;
        this.candidateRadius = candidateRadius < 1 ? 1 : candidateRadius;
        this.maxCandidates = maxCandidates < 1 ? 1 : maxCandidates;
    }

    /// <summary>
    /// Tìm nước đi tốt nhất trong thời gian cho phép.
    /// Thuật toán ưu tiên thắng ngay, chặn thắng ngay rồi mới tìm kiếm nhiều độ sâu.
    /// </summary>
    public int FindBestMove(
        CaroAiBoard board,
        int player,
        int timeLimitMs,
        int requestedDepth)
    {
        if (board.MoveCount == 0)
        {
            return board.ToIndex(board.Width / 2, board.Height / 2);
        }

        rootPlayer = player;
        timeLimitMilliseconds = timeLimitMs < 1 ? 1 : timeLimitMs;
        stopwatch = Stopwatch.StartNew();
        timedOut = false;

        // Lấy các nước đi gần khu vực có quân để giảm số nhánh cần duyệt.
        List<int> candidates = CaroAiMoveGenerator.Generate(board, candidateRadius);
        if (candidates.Count == 0)
        {
            return -1;
        }

        // Luôn xử lý nước thắng trực tiếp trước khi chạy tìm kiếm sâu.
        int winningMove = FindImmediateMove(board, candidates, player);
        if (winningMove >= 0)
        {
            return winningMove;
        }

        // Nếu đối thủ có đúng một nước thắng kế tiếp, phải chặn nước đó ngay.
        List<int> forcedBlocks = FindImmediateMoves(board, candidates, -player);
        if (forcedBlocks.Count == 1)
        {
            return forcedBlocks[0];
        }

        List<int> ordered = OrderAndLimit(board, forcedBlocks.Count > 1 ? forcedBlocks : candidates, player);
        if (ordered.Count == 0)
        {
            return -1;
        }

        int bestMove = ordered[0];
        int maxDepth = requestedDepth < 1 ? 1 : requestedDepth;

        // Tìm từ độ sâu thấp lên cao; nếu hết thời gian thì giữ kết quả sâu nhất đã hoàn tất.
        for (int depth = 1; depth <= maxDepth; depth++)
        {
            int localBestMove = bestMove;
            int localBestScore = int.MinValue;
            timedOut = false;

            for (int i = 0; i < ordered.Count; i++)
            {
                if (IsTimeUp())
                {
                    timedOut = true;
                    break;
                }

                int move = ordered[i];
                board.ApplyIndex(move, player);

                int score;
                // Đặt thử nước đi của người chơi gốc rồi đánh giá nhánh còn lại.
                if (CaroAiRules.HasWinningLineAt(board, move, player, winLength))
                {
                    score = WinScore + depth;
                }
                else
                {
                    score = AlphaBeta(board, depth - 1, -player, int.MinValue + 1, int.MaxValue - 1);
                }

                board.UndoIndex(move);

                if (timedOut)
                {
                    break;
                }

                if (score > localBestScore)
                {
                    localBestScore = score;
                    localBestMove = move;
                }
            }

            if (timedOut)
            {
                break;
            }

            bestMove = localBestMove;
        }

        return bestMove;
    }

    /// <summary>Đệ quy Alpha-Beta để loại bỏ các nhánh chắc chắn không tốt hơn.</summary>
    private int AlphaBeta(
        CaroAiBoard board,
        int depth,
        int player,
        int alpha,
        int beta)
    {
        if (IsTimeUp())
        {
            timedOut = true;
            return 0;
        }

        // Khi hết độ sâu, dùng heuristic thay vì mở rộng thêm cây tìm kiếm.
        if (depth <= 0)
        {
            return CaroAiEvaluator.EvaluateBoard(board, rootPlayer, candidateRadius);
        }

        List<int> candidates = CaroAiMoveGenerator.Generate(board, candidateRadius);
        if (candidates.Count == 0)
        {
            return 0;
        }

        List<int> ordered = OrderAndLimit(board, candidates, player);
        bool maximizing = player == rootPlayer;
        int bestScore = maximizing ? int.MinValue : int.MaxValue;

        for (int i = 0; i < ordered.Count; i++)
        {
            if (IsTimeUp())
            {
                timedOut = true;
                break;
            }

            int move = ordered[i];
            board.ApplyIndex(move, player);

            int score;
            if (CaroAiRules.HasWinningLineAt(board, move, player, winLength))
            {
                score = player == rootPlayer ? WinScore - depth : -WinScore + depth;
            }
            else
            {
                score = AlphaBeta(board, depth - 1, -player, alpha, beta);
            }

            board.UndoIndex(move);

            if (timedOut)
            {
                break;
            }

            if (maximizing)
            {
                if (score > bestScore)
                {
                    bestScore = score;
                }

                if (bestScore > alpha)
                {
                    alpha = bestScore;
                }
            }
            else
            {
                if (score < bestScore)
                {
                    bestScore = score;
                }

                if (bestScore < beta)
                {
                    beta = bestScore;
                }
            }

            // Cắt nhánh khi khoảng Alpha-Beta đã giao nhau.
            if (beta <= alpha)
            {
                break;
            }
        }

        return bestScore;
    }

    /// <summary>Sắp xếp ứng viên theo điểm tấn công/phòng thủ và cắt bớt nhánh.</summary>
    private List<int> OrderAndLimit(CaroAiBoard board, List<int> candidates, int player)
    {
        var ordered = new List<int>(candidates);
        ordered.Sort((left, right) =>
        {
            int leftScore = ScoreCandidate(board, left, player);
            int rightScore = ScoreCandidate(board, right, player);
            return rightScore.CompareTo(leftScore);
        });

        if (ordered.Count > maxCandidates)
        {
            ordered.RemoveRange(maxCandidates, ordered.Count - maxCandidates);
        }

        return ordered;
    }

    /// <summary>Điểm dùng để ưu tiên những nước vừa có lợi cho AI vừa có tác dụng phòng thủ.</summary>
    private int ScoreCandidate(CaroAiBoard board, int index, int player)
    {
        int attack = CaroAiEvaluator.ScoreMove(board, index, player);
        int defense = CaroAiEvaluator.ScoreMove(board, index, -player);
        return attack + defense / 2;
    }

    /// <summary>Tìm nước thắng trực tiếp đầu tiên trong tập ứng viên.</summary>
    private int FindImmediateMove(CaroAiBoard board, List<int> candidates, int player)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if (CaroAiRules.IsWinningMove(board, candidates[i], player, winLength))
            {
                return candidates[i];
            }
        }

        return -1;
    }

    /// <summary>Lấy toàn bộ nước có thể tạo thắng ngay, phục vụ việc bắt buộc chặn.</summary>
    private List<int> FindImmediateMoves(CaroAiBoard board, List<int> candidates, int player)
    {
        var winningMoves = new List<int>();

        for (int i = 0; i < candidates.Count; i++)
        {
            if (CaroAiRules.IsWinningMove(board, candidates[i], player, winLength))
            {
                winningMoves.Add(candidates[i]);
            }
        }

        return winningMoves;
    }

    /// <summary>Kiểm tra bộ tìm kiếm đã vượt quá ngân sách thời gian hay chưa.</summary>
    private bool IsTimeUp()
    {
        return stopwatch != null && stopwatch.ElapsedMilliseconds >= timeLimitMilliseconds;
    }
}
