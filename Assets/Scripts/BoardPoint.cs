using UnityEngine;
using System;

public class BoardPoint : MonoBehaviour
{
    public int x;
    public int y;

    // Sự kiện tĩnh để GameManager lắng nghe tất cả các điểm trên bàn cờ.
    public static event Action<int, int> OnPointClicked;

    private const float ClickThresholdPixels = 10f;
    private Vector3 pointerDownPosition;
    private bool pointerIsDown;

    private void OnMouseDown()
    {
        // Ghi lại vị trí bắt đầu. Chưa phát nước đi vì người chơi có thể đang kéo camera.
        pointerDownPosition = Input.mousePosition;
        pointerIsDown = true;
    }

    private void OnMouseUp()
    {
        if (!pointerIsDown)
        {
            return;
        }

        pointerIsDown = false;

        // Nếu khoảng cách lớn hơn ngưỡng thì đây là thao tác kéo camera, không phải click ô.
        if (Vector3.Distance(pointerDownPosition, Input.mousePosition) > ClickThresholdPixels)
        {
            return;
        }

        // Chỉ phát sự kiện khi người chơi click thật vào ô, kèm theo tọa độ (x, y).
        OnPointClicked?.Invoke(x, y);
    }

    private void OnDisable()
    {
        pointerIsDown = false;
    }
}
