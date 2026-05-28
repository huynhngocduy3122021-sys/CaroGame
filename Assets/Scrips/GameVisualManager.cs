using UnityEngine;
using Unity.Netcode;

public class GameVisualManager : NetworkBehaviour
{
    [SerializeField] private Transform crossPrefab;
    [SerializeField] private Transform circlePrefab;
    [SerializeField] private Transform linCompletePrefab;


    [SerializeField] private int width = 30;
    [SerializeField] private int height = 18;
    [SerializeField] private float cellSize = 0.45f;
    [SerializeField] private float cameraSize = 7.5f;
    [SerializeField] private float topUiRatio = 0.1f;
    

    private void Start()
    {
       GameManager.Instance.OnGripPositionClicked += GameManager_OnGripPositionClicked;
       GameManager.Instance.OnGameWin += GameManager_OnGameWin;
    }
    private void GameManager_OnGameWin(object sender, GameManager.OnGameWinEventArgs e)
    {
       
       Transform lineCompleteTransform = Instantiate(linCompletePrefab, GetGripPosition(e.centerGridposition.x, e.centerGridposition.y), Quaternion.identity);
         lineCompleteTransform.GetComponent<NetworkObject>().Spawn(true);
      
    }
    private void GameManager_OnGripPositionClicked(object sender, GameManager.OnGripPositionClickedEventArgs e)
    {
        Debug.Log("GameManager_OnGripPositionClicked:" + e.x + ", " + e.y);
       SpawObjectRpc(e.x, e.y , e.playerType);
    }

    [Rpc(SendTo.Server)] // Nói đơn giản là Remote Procedure Call là cách mà Client và server gọi hàm từ xa qua mạng
    private void SpawObjectRpc(int x, int y , GameManager.PlayerType playerType ) // hàm này sẽ được gọi từ client và thực thi trên server để tạo ra đối tượng quân cờ tương ứng với người chơi đã click vào vị trí trên bàn cờ
    {
        Debug.Log("SpawObject:" + x + ", " + y);
        Transform preFabs;
        switch(playerType)
        {
            
            case GameManager.PlayerType.Cross:
                preFabs = crossPrefab;
                break;
            case GameManager.PlayerType.Circle:
                preFabs = circlePrefab;
                break;
            default:
                Debug.LogError("Invalid player type");
                return;
        }
        Transform instantiatedPrefab = Instantiate(preFabs , GetGripPosition(x, y), Quaternion.identity);
        instantiatedPrefab.GetComponent<NetworkObject>().Spawn(true);
        
    }
    private Vector2 GetGripPosition(int x, int y) // lấy vị trí grip dựa trên x, y và các thông số của board để tính toán vị trí chính xác
    {
         float worldHeight = cameraSize * 2f;
        float topSpace = worldHeight * topUiRatio;

        float boardWidth = width * cellSize;
        float boardHeight = height * cellSize;

        float startX = -boardWidth / 2f;

        float boardAreaCenterY = -topSpace / 2f;
        float startY = boardAreaCenterY - boardHeight / 2f;

        return new Vector2(
            startX + x * cellSize,
            startY + y * cellSize
        );
    
    }
}
