using UnityEngine;
using Unity.Netcode;
using System.Collections;
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

    // Tách riêng danh sách lưu đường kẻ (khi thắng) và Dictionary lưu quân cờ
    private List<GameObject> winLinesList = new List<GameObject>();
    private Dictionary<Vector2Int, GameObject> pieceDictionary = new Dictionary<Vector2Int, GameObject>();

    private GameObject lastMoveHighlight;
    private Coroutine highlightCoroutine;

    private void Start()
    {
        GameManager.Instance.OnGripPositionClicked += GameManager_OnGripPositionClicked;
        GameManager.Instance.OnGameWin += GameManager_OnGameWin;
        GameManager.Instance.OnRematch += GameManager_OnRematch;
        GameManager.Instance.OnGameReturnedToLobby += GameManager_OnGameReturnedToLobby;

        if (GameManager.Instance.lastMovePosition != null)
        {
            GameManager.Instance.lastMovePosition.OnValueChanged += LastMovePosition_OnValueChanged;
            if (GameManager.Instance.lastMovePosition.Value.x != -1)
            {
                LastMovePosition_OnValueChanged(Vector2Int.zero, GameManager.Instance.lastMovePosition.Value);
            }
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGripPositionClicked -= GameManager_OnGripPositionClicked;
            GameManager.Instance.OnGameWin -= GameManager_OnGameWin;
            GameManager.Instance.OnRematch -= GameManager_OnRematch;
            GameManager.Instance.OnGameReturnedToLobby -= GameManager_OnGameReturnedToLobby;

            if (GameManager.Instance.lastMovePosition != null)
            {
                GameManager.Instance.lastMovePosition.OnValueChanged -= LastMovePosition_OnValueChanged;
            }
        }
    }

    // TỐI ƯU HÓA: Tìm quân cờ tốc độ cao
    private GameObject FindPieceAtPosition(Vector2Int gridPos, Vector3 worldPos)
    {
        // 1. Nếu là Server, lấy ngay lập tức từ Dictionary (Độ phức tạp O(1))
        if (pieceDictionary.TryGetValue(gridPos, out GameObject piece))
        {
            return piece;
        }

        // 2. Nếu là Client (chưa có trong Dictionary do Server spawn)
        // Dùng danh sách object đã spawn của Netcode thay vì FindObjectsOfType toàn bộ scene
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            foreach (var kvp in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
            {
                NetworkObject netObj = kvp.Value;
                // Chỉ kiểm tra vị trí, bỏ hoàn toàn so sánh chuỗi (string matching)
                if (Vector3.Distance(netObj.transform.position, worldPos) < 0.01f)
                {
                    // Lưu lại vào Dictionary để Client không cần quét lại lần sau
                    pieceDictionary[gridPos] = netObj.gameObject;
                    return netObj.gameObject;
                }
            }
        }

        return null;
    }

    private IEnumerator AddPieceVisualWithRetry(Vector2Int gridPos, Vector3 worldPos)
    {
        float timeout = 1.0f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            // Gọi hàm tìm kiếm mới được tối ưu
            GameObject piece = FindPieceAtPosition(gridPos, worldPos);

            if (piece != null)
            {
                if (piece.GetComponent<PieceVisual>() == null)
                {
                    piece.AddComponent<PieceVisual>();
                }
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void LastMovePosition_OnValueChanged(Vector2Int oldVal, Vector2Int newVal)
    {
        if (newVal.x == -1 && newVal.y == -1)
        {
            if (lastMoveHighlight != null)
            {
                lastMoveHighlight.SetActive(false);
            }
            return;
        }

        Vector2 worldPos = GetGripPosition(newVal.x, newVal.y);

        // Truyền thêm toạ độ lưới (newVal) để hàm tìm kiếm hoạt động với Dictionary
        StartCoroutine(AddPieceVisualWithRetry(newVal, worldPos));

        GameManager.PlayerType lastPlayerType = GameManager.Instance.GetPlayerTypeAtPosition(newVal.x, newVal.y);

        if (lastMoveHighlight == null)
        {
            lastMoveHighlight = new GameObject("LastMoveHighlight");
            lastMoveHighlight.AddComponent<SpriteRenderer>();
        }

        SpriteRenderer sr = lastMoveHighlight.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Sprite spriteToUse = null;
            Transform prefabToUse = (lastPlayerType == GameManager.PlayerType.Cross) ? crossPrefab : circlePrefab;

            if (prefabToUse != null)
            {
                Transform spriteChild = prefabToUse.Find("Sprite");
                if (spriteChild != null)
                {
                    SpriteRenderer parentSR = spriteChild.GetComponent<SpriteRenderer>();
                    if (parentSR != null)
                    {
                        spriteToUse = parentSR.sprite;
                    }
                }
            }
            sr.sprite = spriteToUse;
            sr.color = new Color(1f, 0.65f, 0f, 0.7f);
            sr.sortingOrder = 3;
        }

        lastMoveHighlight.SetActive(true);
        lastMoveHighlight.transform.position = worldPos;

        if (highlightCoroutine != null)
        {
            StopCoroutine(highlightCoroutine);
        }
        highlightCoroutine = StartCoroutine(AnimateHighlightBounceAndPulse());
    }

    private IEnumerator AnimateHighlightBounceAndPulse()
    {
        if (lastMoveHighlight == null) yield break;

        float bounceDuration = 0.45f;
        float elapsed = 0f;
        Vector3 startScale = new Vector3(0.18f, 0.18f, 1f);
        Vector3 endScale = new Vector3(0.11f, 0.11f, 1f);

        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bounceDuration;
            float ease = 1f - Mathf.Pow(1f - t, 4f);

            if (lastMoveHighlight != null)
            {
                lastMoveHighlight.transform.localScale = Vector3.Lerp(startScale, endScale, ease);
            }
            yield return null;
        }

        if (lastMoveHighlight != null)
        {
            lastMoveHighlight.transform.localScale = endScale;
        }

        float pulseDuration = 1.5f;
        float pulseElapsed = 0f;

        while (lastMoveHighlight != null && lastMoveHighlight.activeSelf)
        {
            pulseElapsed += Time.deltaTime;
            float t = (pulseElapsed % pulseDuration) / pulseDuration;
            float pulseValue = Mathf.Sin(t * 2f * Mathf.PI);

            lastMoveHighlight.transform.localScale = endScale * (1.0f + pulseValue * 0.15f);

            SpriteRenderer sr = lastMoveHighlight.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(0.4f, 0.85f, (pulseValue + 1f) / 2f);
                sr.color = c;
            }
            yield return null;
        }
    }

    private void GameManager_OnRematch(object sender, System.EventArgs e)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        // Dọn dẹp đường thẳng hiển thị khi thắng
        foreach (GameObject lineObj in winLinesList)
        {
            if (lineObj != null)
            {
                NetworkObject networkObject = lineObj.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn(true);
                }
                else
                {
                    Destroy(lineObj);
                }
            }
        }
        winLinesList.Clear();

        // Dọn dẹp toàn bộ quân cờ
        foreach (var kvp in pieceDictionary)
        {
            GameObject pieceObj = kvp.Value;
            if (pieceObj != null)
            {
                NetworkObject networkObject = pieceObj.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn(true);
                }
                else
                {
                    Destroy(pieceObj);
                }
            }
        }
        pieceDictionary.Clear();
    }

    private void GameManager_OnGameReturnedToLobby(object sender, System.EventArgs e)
    {
        // Khi quay lại sảnh (ví dụ có người chơi thoát), dọn dẹp bàn cờ giống như khi Rematch
        GameManager_OnRematch(sender, e);
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
        winLinesList.Add(lineTransform.gameObject);
    }

    private Quaternion GetWinLineRotation(GameManager.Orientation orientation)
    {
        switch (orientation)
        {
            case GameManager.Orientation.Horizontal: return Quaternion.Euler(0, 0, 0);
            case GameManager.Orientation.Vertical: return Quaternion.Euler(0, 0, 90);
            case GameManager.Orientation.DiagonalA: return Quaternion.Euler(0, 0, 45);
            case GameManager.Orientation.DiagonalB: return Quaternion.Euler(0, 0, -45);
            default: return Quaternion.identity;
        }
    }

    private void GameManager_OnGripPositionClicked(object sender, GameManager.OnGripPositionClickedEventArgs e)
    {
        if (!IsServer)
        {
            return;
        }
        SpawnObject(e.x, e.y, e.playerType);
    }

    private void SpawnObject(int x, int y, GameManager.PlayerType playerType)
    {
        Transform preFabs;
        switch (playerType)
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

        Transform instantiatedPrefab = Instantiate(preFabs, GetGripPosition(x, y), Quaternion.identity);
        NetworkObject networkObject = instantiatedPrefab.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError($"Prefab {preFabs.name} is missing NetworkObject.");
            Destroy(instantiatedPrefab.gameObject);
            return;
        }

        networkObject.Spawn(true);
        // Lưu quân cờ vào Dictionary ngay khi vừa tạo ra
        pieceDictionary[new Vector2Int(x, y)] = instantiatedPrefab.gameObject;
    }

    private Vector2 GetGripPosition(int x, int y)
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