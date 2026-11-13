using System.Collections.Generic;

/// <summary>Phân tích các nước thắng, nước chặn và thế cờ có một ô khuyết.</summary>
public static class CaroThreatAnalyzer
{
    private static readonly int[] DirectionX = { 1, 0, 1, 1 };
    private static readonly int[] DirectionY = { 0, 1, 1, -1 };

    /// <summary>Liệt kê mọi ô thắng ngay của một bên trong tập ứng viên.</summary>
    public static List<int> FindImmediateWins(CaroAiBoard board, List<int> candidates, int player, int winLength)
    {
        var wins = new List<int>();
        foreach (int index in candidates)
        {
            if (CaroAiRules.IsWinningMove(board, index, player, winLength))
            {
                wins.Add(index);
            }
        }
        return wins;
    }

    /// <summary>Tìm hai đầu trống của chuỗi ba hở liên tiếp để AI ưu tiên phòng thủ.</summary>
    public static List<int> FindOpenThreeBlocks(CaroAiBoard board, List<int> candidates, int opponent)
    {
        var blocks = new List<int>();
        foreach (int index in candidates)
        {
            int x = index % board.Width;
            int y = index / board.Width;
            for (int direction = 0; direction < DirectionX.Length; direction++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int dx = DirectionX[direction] * sign;
                    int dy = DirectionY[direction] * sign;
                    int count = 0;
                    int px = x + dx;
                    int py = y + dy;
                    while (board.IsInside(px, py) && board.Cells[board.ToIndex(px, py)] == opponent)
                    {
                        count++;
                        px += dx;
                        py += dy;
                    }
                    if (count >= 3 && board.IsEmpty(px, py))
                    {
                        blocks.Add(index);
                        goto NextCandidate;
                    }
                }
            }
            NextCandidate:;
        }
        return blocks;
    }

    /// <summary>Chấm nước tạo các cửa sổ năm ô có bốn quân hoặc ba quân, kể cả chuỗi khuyết.</summary>
    public static int ScoreThreat(CaroAiBoard board, int index, int player, int winLength)
    {
        if (!board.IsEmpty(index)) return 0;

        int x = index % board.Width;
        int y = index / board.Width;
        int fourWays = 0;
        int threeWays = 0;
        for (int direction = 0; direction < DirectionX.Length; direction++)
        {
            int best = 0;
            for (int offset = -(winLength - 1); offset <= 0; offset++)
            {
                int own = 0;
                int empty = 0;
                bool blocked = false;
                for (int step = 0; step < winLength; step++)
                {
                    int px = x + (offset + step) * DirectionX[direction];
                    int py = y + (offset + step) * DirectionY[direction];
                    if (!board.IsInside(px, py)) { blocked = true; break; }
                    int value = px == x && py == y ? player : board.Cells[board.ToIndex(px, py)];
                    if (value == -player) { blocked = true; break; }
                    if (value == player) own++;
                    else empty++;
                }
                if (!blocked && empty > 0 && own > best) best = own;
            }
            if (best >= winLength - 1) fourWays++;
            else if (best >= winLength - 2) threeWays++;
        }

        if (fourWays >= 2) return 300000;
        if (fourWays == 1) return 30000;
        if (threeWays >= 2) return 10000;
        return threeWays * 1000;
    }
}
