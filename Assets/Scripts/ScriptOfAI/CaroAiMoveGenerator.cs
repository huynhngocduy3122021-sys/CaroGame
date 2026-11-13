using System.Collections.Generic;

/// <summary>
/// Sinh các nước đi ứng viên ở khu vực gần quân đã có.
/// Cách này giúp AI không phải duyệt toàn bộ 10.000 ô trên bàn 100x100.
/// </summary>
public static class CaroAiMoveGenerator
{
    /// <summary>
    /// Lấy các ô trống nằm trong bán kính quanh những quân đã đặt.
    /// Nếu bàn cờ trống, ưu tiên ô trung tâm; nếu không có ứng viên thì dùng toàn bộ ô trống.
    /// </summary>
    public static List<int> Generate(CaroAiBoard board, int radius)
    {
        var candidates = new List<int>();
        var seen = new bool[board.CellCount];

        // Nước mở đầu ở trung tâm giúp giảm nhánh tìm kiếm và tạo thế cân bằng.
        if (board.MoveCount == 0)
        {
            int centerX = board.Width / 2;
            int centerY = board.Height / 2;
            candidates.Add(board.ToIndex(centerX, centerY));
            return candidates;
        }

        radius = radius < 1 ? 1 : radius;

        // Quét quanh từng quân đã có để tạo tập nước đi chiến thuật gần khu vực giao tranh.
        for (int index = 0; index < board.CellCount; index++)
        {
            if (board.Cells[index] == CaroAiRules.Empty)
            {
                continue;
            }

            int pieceX = index % board.Width;
            int pieceY = index / board.Width;

            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    int candidateX = pieceX + offsetX;
                    int candidateY = pieceY + offsetY;

                    if (!board.IsInside(candidateX, candidateY))
                    {
                        continue;
                    }

                    int candidateIndex = board.ToIndex(candidateX, candidateY);
                    if (!board.IsEmpty(candidateIndex) || seen[candidateIndex])
                    {
                        continue;
                    }

                    seen[candidateIndex] = true;
                    candidates.Add(candidateIndex);
                }
            }
        }

        // Trường hợp đặc biệt: không tìm được vùng lân cận thì cho phép mọi ô trống.
        if (candidates.Count == 0)
        {
            for (int index = 0; index < board.CellCount; index++)
            {
                if (board.IsEmpty(index))
                {
                    candidates.Add(index);
                }
            }
        }

        return candidates;
    }
}
