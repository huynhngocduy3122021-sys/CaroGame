using System;

/// <summary>
/// Trạng thái bàn cờ được dùng bởi AI tìm kiếm.
/// Giá trị của mỗi ô: 0 = ô trống, 1 = quân AI, -1 = quân đối thủ.
/// Lớp này không phụ thuộc Unity hoặc networking để có thể chạy và kiểm thử độc lập.
/// </summary>
public sealed class CaroAiBoard
{
    // Lưu bàn cờ bằng mảng một chiều; chỉ số ô được tính theo công thức y * Width + x.
    private readonly int[] cells;

    public CaroAiBoard(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Kích thước bàn cờ phải lớn hơn 0.");
        }

        Width = width;
        Height = height;
        cells = new int[width * height];
    }

    // Chiều rộng và chiều cao của bàn cờ.
    public int Width { get; }
    public int Height { get; }

    // Tổng số ô đã được đặt quân, dùng để nhận biết trạng thái bàn cờ ban đầu.
    public int MoveCount { get; private set; }

    // Tổng số ô và dữ liệu các ô để những lớp luật/tìm kiếm đọc trạng thái.
    public int CellCount => cells.Length;
    public int[] Cells => cells;

    /// <summary>Kiểm tra một tọa độ có nằm trong bàn cờ hay không.</summary>
    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    /// <summary>Đổi tọa độ 2D thành chỉ số của mảng 1D.</summary>
    public int ToIndex(int x, int y)
    {
        return y * Width + x;
    }

    /// <summary>Kiểm tra ô theo chỉ số mảng có đang trống hay không.</summary>
    public bool IsEmpty(int index)
    {
        return index >= 0 && index < cells.Length && cells[index] == 0;
    }

    /// <summary>Kiểm tra ô theo tọa độ có đang trống hay không.</summary>
    public bool IsEmpty(int x, int y)
    {
        return IsInside(x, y) && IsEmpty(ToIndex(x, y));
    }

    /// <summary>Đặt quân nếu tọa độ hợp lệ và ô chưa bị chiếm.</summary>
    public bool TryApply(int x, int y, int player)
    {
        if (!IsInside(x, y))
        {
            return false;
        }

        return TryApplyIndex(ToIndex(x, y), player);
    }

    /// <summary>Đặt quân theo chỉ số mảng nếu ô chưa bị chiếm.</summary>
    public bool TryApplyIndex(int index, int player)
    {
        if (index < 0 || index >= cells.Length || cells[index] != 0)
        {
            return false;
        }

        cells[index] = player;
        MoveCount++;
        return true;
    }

    /// <summary>
    /// Đặt quân bắt buộc. Dùng trong lúc tìm kiếm, nơi nước đi đã được kiểm tra trước.
    /// </summary>
    public void ApplyIndex(int index, int player)
    {
        if (!TryApplyIndex(index, player))
        {
            throw new InvalidOperationException("Không thể đặt quân vào ô không hợp lệ hoặc đã bị chiếm.");
        }
    }

    /// <summary>Hoàn tác một nước đi để Alpha-Beta thử nhánh khác.</summary>
    public void UndoIndex(int index)
    {
        if (index < 0 || index >= cells.Length || cells[index] == 0)
        {
            return;
        }

        cells[index] = 0;
        MoveCount--;
    }

    /// <summary>Xóa toàn bộ quân và đưa bàn cờ về trạng thái ban đầu.</summary>
    public void Clear()
    {
        Array.Clear(cells, 0, cells.Length);
        MoveCount = 0;
    }
}
