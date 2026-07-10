using UnityEngine;

public class BoardGenerator : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private int width = 30;
    [SerializeField] private int height = 18;
    [SerializeField] private float cellSize = 0.45f ;

    [Header("Line")]
    [SerializeField] private float lineWidth = 0.015f;
    [SerializeField] private Color lineColor = Color.black;

    [Header("Camera")]
    [SerializeField] private float cameraSize = 7.5f;
    [SerializeField] private float topUiRatio = 0.1f;

    [Header("Click Point")]
    [SerializeField] private float clickBoxSize = 0.3f;


    void Start()
    {
        lineColor = Color.black;
        
        Camera[] cameras = FindObjectsOfType<Camera>();
        foreach (Camera cam in cameras)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;
        }

        // Disable the background sprite renderer so the pure white camera background shows through
       

        transform.localScale = Vector3.one;
        GenerateBoard();
    }

 

    void GenerateBoard() // tạo các đường kẻ và điểm click dựa trên các thông số đã thiết lập để tạo ra một bàn cờ hoàn chỉnh
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        float worldHeight = cameraSize * 2f;
        float topSpace = worldHeight * topUiRatio;

        float boardWidth = width * cellSize;
        float boardHeight = height * cellSize;

        float startX = -boardWidth / 2f;

        float boardAreaCenterY = -topSpace / 2f;
        float startY = boardAreaCenterY - boardHeight / 2f;

        for (int x = 0; x <= width; x++)
        {
            float xPos = startX + x * cellSize;

            CreateLine(
                new Vector3(xPos, startY, 0),
                new Vector3(xPos, startY + boardHeight, 0),
                "Vertical Line " + x
            );
        }

        for (int y = 0; y <= height; y++)
        {
            float yPos = startY + y * cellSize;

            CreateLine(
                new Vector3(startX, yPos, 0),
                new Vector3(startX + boardWidth, yPos, 0),
                "Horizontal Line " + y
            );
        }

        CreateClickPoints(startX, startY);
    }

    void CreateClickPoints(float startX, float startY) // tạo điểm click ở mỗi giao điểm của các đường kẻ để người chơi có thể tương tác và đặt quân cờ vào đúng vị trí trên bàn cờ
    {
        for (int x = 0; x <= width; x++)
        {
            for (int y = 0; y <= height; y++)
            {
                GameObject point = new GameObject($"Point_{x}_{y}");
                point.transform.SetParent(transform, false);

                point.transform.localPosition = new Vector3(
                    startX + x * cellSize,
                    startY + y * cellSize,
                    0
                );

                BoxCollider2D col = point.AddComponent<BoxCollider2D>();
                col.size = new Vector2(clickBoxSize, clickBoxSize);
                col.isTrigger = false;

                BoardPoint boardPoint = point.AddComponent<BoardPoint>();
                boardPoint.x = x;
                boardPoint.y = y;
               
            }
        }
    }

    void CreateLine(Vector3 start, Vector3 end, string lineName)
    {
        GameObject lineObj = new GameObject(lineName);
        lineObj.transform.SetParent(transform, false);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();

        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;

        lr.useWorldSpace = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));

        lr.startColor = lineColor;
        lr.endColor = lineColor;

        lr.sortingOrder = 1;
    }
}
