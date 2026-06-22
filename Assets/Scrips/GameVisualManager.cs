using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

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
    
    private List<GameObject> visualGameObjectList = new List<GameObject>();
    private void Awake()
    {
        visualGameObjectList = new List<GameObject>();
    }

    private void Start()
    {
       GameManager.Instance.OnGripPositionClicked += GameManager_OnGripPositionClicked;
       GameManager.Instance.OnGameWin += GameManager_OnGameWin;
       GameManager.Instance.OnRematch += GameManager_OnRematch;
    }
    private void GameManager_OnRematch(object sender, System.EventArgs e)
    {
        if(!NetworkManager.Singleton.IsServer)
        {
            return;
         }
        foreach (GameObject visualGameOBJ in visualGameObjectList)
        {
                if (visualGameOBJ == null) continue;

                NetworkObject networkObject = visualGameOBJ.GetComponent<NetworkObject>();

                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn(true);
                }
                else
                {
                    Destroy(visualGameOBJ);
                }
        }
        visualGameObjectList.Clear();
    }
    private void GameManager_OnGameWin(object sender, GameManager.OnGameWinEventArgs e)
    {
        if (!IsServer || e.playerWinType == GameManager.PlayerType.None)
        {
            return;
        }

        Transform lineTransform = Instantiate(
            linCompletePrefab,
            GetGripPosition(e.centerGridposition.x, e.centerGridposition.y),
            GetWinLineRotation(e.orientation)
        );

        SpriteRenderer lineRenderer = lineTransform.GetComponent<SpriteRenderer>();
        if (lineRenderer != null)
        {
            lineRenderer.sortingOrder = 3;
        }

        NetworkObject networkObject = lineTransform.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError($"Prefab {linCompletePrefab.name} is missing NetworkObject.");
            Destroy(lineTransform.gameObject);
            return;
        }

        networkObject.Spawn(true);

        visualGameObjectList.Add(lineTransform.gameObject);
      
    }
    private Quaternion GetWinLineRotation(GameManager.Orientation orientation)
    {
        switch(orientation)
        {
            case GameManager.Orientation.Horizontal:
                return Quaternion.Euler(0, 0, 0);
            case GameManager.Orientation.Vertical:
                return Quaternion.Euler(0, 0, 90);
            case GameManager.Orientation.DiagonalA:
                return Quaternion.Euler(0, 0, 45);
            case GameManager.Orientation.DiagonalB:
                return Quaternion.Euler(0, 0, -45);
            default:
                Debug.LogError("Invalid orientation");
                return Quaternion.identity;
        }
    }
    private void GameManager_OnGripPositionClicked(object sender, GameManager.OnGripPositionClickedEventArgs e)
    {
        Debug.Log("GameManager_OnGripPositionClicked:" + e.x + ", " + e.y);
        if (!IsServer)
        {
            return;
        }

        SpawnObject(e.x, e.y, e.playerType);
    }

    private void SpawnObject(int x, int y, GameManager.PlayerType playerType)
    {
        Debug.Log("SpawnObject:" + x + ", " + y);
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
        NetworkObject networkObject = instantiatedPrefab.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError($"Prefab {preFabs.name} is missing NetworkObject.");
            Destroy(instantiatedPrefab.gameObject);
            return;
        }

        networkObject.Spawn(true);
        visualGameObjectList.Add(instantiatedPrefab.gameObject);
        
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
