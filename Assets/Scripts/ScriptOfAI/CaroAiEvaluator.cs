using System.Collections.Generic;

/// <summary>
/// Bộ đánh giá dựa trên mẫu nước đi, dùng cho sắp xếp ứng viên và các nút lá Alpha-Beta.
/// AI đánh giá cả khả năng tấn công, phòng thủ và thưởng thêm cho thế hai mũi đe dọa.
/// </summary>
public static class CaroAiEvaluator
{
    // Bốn hướng tạo thành chuỗi Caro cần được chấm điểm.
    private static readonly int[] DirectionX = { 1, 0, 1, 1 };
    private static readonly int[] DirectionY = { 0, 1, 1, -1 };

    /// <summary>
    /// Đánh giá toàn bộ trạng thái theo góc nhìn của một người chơi.
    /// Điểm tấn công của AI được trừ điểm đe dọa của đối thủ.
    /// </summary>
    public static int EvaluateBoard(CaroAiBoard board, int player, int radius)
    {
        List<int> candidates = CaroAiMoveGenerator.Generate(board, radius);
        if (candidates.Count == 0)
        {
            return 0;
        }

        int opponent = -player;
        int bestAttack = 0;
        int bestDefense = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            int index = candidates[i];
            int attack = ScoreMove(board, index, player);
            int defense = ScoreMove(board, index, opponent);

            if (attack > bestAttack)
            {
                bestAttack = attack;
            }

            if (defense > bestDefense)
            {
                bestDefense = defense;
            }
        }

        return bestAttack - (bestDefense * 11 / 10);
    }

    /// <summary>Chấm điểm nếu người chơi đặt quân vào một ô cụ thể.</summary>
    public static int ScoreMove(CaroAiBoard board, int index, int player)
    {
        if (!board.IsEmpty(index))
        {
            return int.MinValue / 4;
        }

        int x = index % board.Width;
        int y = index / board.Width;
        int totalScore = 0;
        int threatDirections = 0;

        for (int direction = 0; direction < DirectionX.Length; direction++)
        {
            int count = 1;
            count += CountDirection(board, x, y, DirectionX[direction], DirectionY[direction], player);
            count += CountDirection(board, x, y, -DirectionX[direction], -DirectionY[direction], player);

            int openEnds = 0;
            if (IsOpenEnd(board, x, y, DirectionX[direction], DirectionY[direction], player))
            {
                openEnds++;
            }

            if (IsOpenEnd(board, x, y, -DirectionX[direction], -DirectionY[direction], player))
            {
                openEnds++;
            }

            int lineScore = ScorePattern(count, openEnds);
            totalScore += lineScore;

            if (count >= 3 && openEnds > 0)
            {
                threatDirections++;
            }
        }

        // Hai hướng đe dọa cùng lúc thường tạo nước thắng bắt buộc ở lượt sau.
        if (threatDirections >= 2)
        {
            totalScore += 250000;
        }

        return totalScore;
    }

    /// <summary>Chuyển độ dài chuỗi và số đầu mở thành điểm chiến thuật.</summary>
    private static int ScorePattern(int count, int openEnds)
    {
        if (count >= CaroAiRules.DefaultWinLength)
        {
            return 10000000;
        }

        switch (count)
        {
            case 4:
                return openEnds == 2 ? 100000 : openEnds == 1 ? 10000 : 0;
            case 3:
                return openEnds == 2 ? 5000 : openEnds == 1 ? 500 : 0;
            case 2:
                return openEnds == 2 ? 100 : openEnds == 1 ? 10 : 0;
            default:
                return 0;
        }
    }

    /// <summary>Đếm số quân liên tiếp của người chơi theo một hướng.</summary>
    private static int CountDirection(
        CaroAiBoard board,
        int x,
        int y,
        int directionX,
        int directionY,
        int player)
    {
        int count = 0;
        int currentX = x + directionX;
        int currentY = y + directionY;

        while (board.IsInside(currentX, currentY) &&
               board.Cells[board.ToIndex(currentX, currentY)] == player)
        {
            count++;
            currentX += directionX;
            currentY += directionY;
        }

        return count;
    }

    /// <summary>Kiểm tra đầu chuỗi sau nhóm quân có còn là ô trống hay không.</summary>
    private static bool IsOpenEnd(
        CaroAiBoard board,
        int x,
        int y,
        int directionX,
        int directionY,
        int player)
    {
        int currentX = x + directionX;
        int currentY = y + directionY;

        while (board.IsInside(currentX, currentY) &&
               board.Cells[board.ToIndex(currentX, currentY)] == player)
        {
            currentX += directionX;
            currentY += directionY;
        }

        return board.IsInside(currentX, currentY) &&
               board.IsEmpty(board.ToIndex(currentX, currentY));
    }
}
