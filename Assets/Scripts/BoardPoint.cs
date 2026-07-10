using UnityEngine;
using System;

public class BoardPoint : MonoBehaviour
{
    public int x;
    public int y;

    // Tạo một sự kiện tĩnh (static event). 
    // Dùng static để GameManager có thể lắng nghe TẤT CẢ các ô cờ trên bàn mà không cần tìm từng ô một.
    public static event Action<int, int> OnPointClicked;

    void OnMouseDown()
    {
        // Khi bị click, chỉ đơn giản là phát ra sự kiện kèm theo tọa độ (x, y)
        OnPointClicked?.Invoke(x, y);
    }
}