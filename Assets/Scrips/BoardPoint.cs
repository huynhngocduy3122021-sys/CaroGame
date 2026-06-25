using UnityEngine;

public class BoardPoint : MonoBehaviour
{
    public int x;
    public int y;

    void OnMouseDown()
    {
        // Kiểm tra điều kiện: Nếu game chưa chính thức bắt đầu HOẶC mình là Khán giả (None) thì không làm gì cả
        if (!GameManager.Instance.IsGameActive() || GameManager.Instance.GetLocalPlayerType() == GameManager.PlayerType.None)
        {
            return;
        }

        Debug.Log("clicked on point (" + x + ", " + y + ")");
        GameManager.Instance.clickedOnGripPositionRpc(x, y, GameManager.Instance.GetLocalPlayerType());
    }
}