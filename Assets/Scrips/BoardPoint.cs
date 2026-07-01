using UnityEngine;

public class BoardPoint : MonoBehaviour
{
    public int x;
    public int y;

    void OnMouseDown()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("BoardPoint: GameManager.Instance is null!");
            return;
        }

        Debug.Log($"BoardPoint Clicked: ({x}, {y}) | IsGameActive: {GameManager.Instance.IsGameActive()} | LocalPlayerType: {GameManager.Instance.GetLocalPlayerType()} | CurrentTurn: {GameManager.Instance.GetCurrentPlayerType()}");

        if (!GameManager.Instance.IsGameActive() || GameManager.Instance.GetLocalPlayerType() == GameManager.PlayerType.None)
        {
            Debug.Log("BoardPoint Clicked: Ignored due to game inactive or player is None.");
            return;
        }

        GameManager.Instance.clickedOnGripPositionRpc(x, y);
    }
}