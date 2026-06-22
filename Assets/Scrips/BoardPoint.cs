using UnityEngine;

public class BoardPoint : MonoBehaviour 
{
    public int x;
    public int y;

    void OnMouseDown()
    {
        Debug.Log("clicked on point (" + x + ", " + y + ")");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.clickedOnGripPositionRpc(x, y);
        }
    }
}
