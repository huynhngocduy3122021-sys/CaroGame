/// <summary>
/// Các luật xác định, dùng chung cho AI tìm kiếm.
/// Việc xác thực nước đi trong game vẫn do GameManager/MatchRules quyết định cuối cùng.
/// </summary>
public static class CaroAiRules
{
    // Quy ước giá trị lưu trong CaroAiBoard.
    public const int Empty = 0;
    public const int AiPlayer = 1;
    public const int OpponentPlayer = -1;
    public const int DefaultWinLength = 5;

    // Bốn hướng cần kiểm tra: ngang, dọc và hai đường chéo.
    private static readonly int[] DirectionX = { 1, 0, 1, 1 };
    private static readonly int[] DirectionY = { 0, 1, 1, -1 };

    /// <summary>Thử đặt một quân rồi kiểm tra nước đó có tạo thành chuỗi thắng không.</summary>
    public static bool IsWinningMove(
        CaroAiBoard board,
        int index,
        int player,
        int winLength = DefaultWinLength)
    {
        if (!board.IsEmpty(index))
        {
            return false;
        }

        board.ApplyIndex(index, player);
        try
        {
            return HasWinningLineAt(board, index, player, winLength);
        }
        finally
        {
            board.UndoIndex(index);
        }
    }

    /// <summary>Kiểm tra chuỗi liên tiếp đi qua ô vừa đặt có đủ số quân thắng hay không.</summary>
    public static bool HasWinningLineAt(
        CaroAiBoard board,
        int index,
        int player,
        int winLength = DefaultWinLength)
    {
        if (index < 0 || index >= board.CellCount || board.Cells[index] != player)
        {
            return false;
        }

        int x = index % board.Width;
        int y = index / board.Width;

        for (int direction = 0; direction < DirectionX.Length; direction++)
        {
            int count = 1;
            count += CountDirection(board, x, y, DirectionX[direction], DirectionY[direction], player);
            count += CountDirection(board, x, y, -DirectionX[direction], -DirectionY[direction], player);

            if (count >= winLength)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Đếm số quân liên tiếp theo một hướng, bắt đầu từ ô kế bên.</summary>
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
}
