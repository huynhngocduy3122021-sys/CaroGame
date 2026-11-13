using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Điều khiển camera cho bàn Caro lớn trên PC và mobile.
/// PC: kéo chuột trái để di chuyển, lăn chuột để phóng to/thu nhỏ.
/// Mobile: kéo một ngón tay để di chuyển, dùng hai ngón tay để zoom.
/// </summary>
[RequireComponent(typeof(Camera))]
public sealed class CaroCameraController : MonoBehaviour
{
    [Header("Kích thước bàn cờ")]
    [Tooltip("Số khoảng chia theo chiều ngang. Với 31 điểm tọa độ hiện tại, giá trị là 30.")]
    [SerializeField] private int boardWidth = GameManager.BoardPointsX - 1;

    [Tooltip("Số khoảng chia theo chiều dọc. Với 19 điểm tọa độ hiện tại, giá trị là 18.")]
    [SerializeField] private int boardHeight = GameManager.BoardPointsY - 1;

    [SerializeField] private float cellSize = 0.45f;

    [Tooltip("Phải khớp với cameraSize của BoardGenerator để tâm bàn được tính đúng.")]
    [SerializeField] private float boardLayoutCameraSize = 7.5f;

    [SerializeField, Range(0f, 0.5f)] private float topUiRatio = 0.1f;
    [SerializeField] private float boardPadding = 0.5f;

    [Header("Phóng to / thu nhỏ")]
    [SerializeField] private float minZoom = 3f;
    [SerializeField] private float maxZoom = 25f;
    [SerializeField] private float mouseWheelZoomSpeed = 2f;
    [SerializeField] private float pinchZoomSpeed = 2.5f;
    [SerializeField] private bool centerCameraOnStart = true;

    [Header("Di chuyển camera")]
    [SerializeField] private float mousePanMultiplier = 1f;
    [SerializeField] private float touchPanMultiplier = 1f;

    private Camera targetCamera;
    private Vector3 lastPointerWorldPosition;
    private bool isMouseDragging;
    private bool isTouchDragging;
    private float lastPinchDistance;
    private Vector2 lastTouchMidpoint;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.orthographic = true;
        targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize, minZoom, maxZoom);
    }

    private void Start()
    {
        if (centerCameraOnStart)
        {
            Vector2 boardCenter = GetBoardCenter();
            transform.position = new Vector3(boardCenter.x, boardCenter.y, transform.position.z);
        }

        ClampCameraPosition();
    }

    private void Update()
    {
        if (Input.touchSupported && Input.touchCount > 0)
        {
            HandleTouchInput();
            return;
        }

        HandleMouseInput();
    }

    private void HandleMouseInput()
    {
        if (IsPointerOverUi())
        {
            isMouseDragging = false;
        }
        else if (Input.GetMouseButtonDown(0))
        {
            isMouseDragging = true;
            lastPointerWorldPosition = ScreenToWorld(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isMouseDragging = false;
        }

        if (isMouseDragging && Input.GetMouseButton(0))
        {
            Vector3 currentPointerWorldPosition = ScreenToWorld(Input.mousePosition);
            Vector3 movement = lastPointerWorldPosition - currentPointerWorldPosition;
            transform.position += new Vector3(movement.x, movement.y, 0f) * mousePanMultiplier;
            lastPointerWorldPosition = ScreenToWorld(Input.mousePosition);
        }

        float wheelDelta = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(wheelDelta, 0f) && !IsPointerOverUi())
        {
            ZoomCamera(-wheelDelta * mouseWheelZoomSpeed);
        }

        ClampCameraPosition();
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (IsPointerOverUi(touch.fingerId))
            {
                isTouchDragging = false;
                return;
            }

            if (touch.phase == TouchPhase.Began)
            {
                isTouchDragging = true;
                lastPointerWorldPosition = ScreenToWorld(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isTouchDragging = false;
            }
            else if (isTouchDragging && touch.phase == TouchPhase.Moved)
            {
                Vector3 currentPointerWorldPosition = ScreenToWorld(touch.position);
                Vector3 movement = lastPointerWorldPosition - currentPointerWorldPosition;
                transform.position += new Vector3(movement.x, movement.y, 0f) * touchPanMultiplier;
                lastPointerWorldPosition = ScreenToWorld(touch.position);
            }

            ClampCameraPosition();
            return;
        }

        if (Input.touchCount >= 2)
        {
            Touch firstTouch = Input.GetTouch(0);
            Touch secondTouch = Input.GetTouch(1);

            if (IsPointerOverUi(firstTouch.fingerId) || IsPointerOverUi(secondTouch.fingerId))
            {
                isTouchDragging = false;
                lastPinchDistance = 0f;
                return;
            }

            float currentPinchDistance = Vector2.Distance(firstTouch.position, secondTouch.position);
            Vector2 currentTouchMidpoint = (firstTouch.position + secondTouch.position) * 0.5f;

            if (firstTouch.phase == TouchPhase.Began || secondTouch.phase == TouchPhase.Began || lastPinchDistance <= 0f)
            {
                lastPinchDistance = currentPinchDistance;
                lastTouchMidpoint = currentTouchMidpoint;
                return;
            }

            float pinchDelta = currentPinchDistance - lastPinchDistance;
            ZoomCamera(-pinchDelta / Mathf.Max(1f, Screen.height) * pinchZoomSpeed * 10f);

            Vector3 currentMidpointWorld = ScreenToWorld(currentTouchMidpoint);
            Vector3 previousMidpointWorld = ScreenToWorld(lastTouchMidpoint);
            Vector3 movement = previousMidpointWorld - currentMidpointWorld;
            transform.position += new Vector3(movement.x, movement.y, 0f) * touchPanMultiplier;

            lastPinchDistance = currentPinchDistance;
            lastTouchMidpoint = currentTouchMidpoint;
            ClampCameraPosition();
        }
    }

    private void ZoomCamera(float amount)
    {
        targetCamera.orthographicSize = Mathf.Clamp(
            targetCamera.orthographicSize + amount,
            minZoom,
            maxZoom);

        ClampCameraPosition();
    }

    private Vector3 ScreenToWorld(Vector2 screenPosition)
    {
        float cameraDistance = Mathf.Abs(transform.position.z);
        Vector3 screenPoint = new Vector3(screenPosition.x, screenPosition.y, cameraDistance);
        return targetCamera.ScreenToWorldPoint(screenPoint);
    }

    private Vector2 GetBoardCenter()
    {
        float worldHeight = boardLayoutCameraSize * 2f;
        float topSpace = worldHeight * topUiRatio;
        return new Vector2(0f, -topSpace * 0.5f);
    }

    private Bounds GetBoardBounds()
    {
        Vector2 boardCenter = GetBoardCenter();
        float boardWorldWidth = Mathf.Max(0, boardWidth) * cellSize;
        float boardWorldHeight = Mathf.Max(0, boardHeight) * cellSize;

        var bounds = new Bounds(
            new Vector3(boardCenter.x, boardCenter.y, 0f),
            new Vector3(boardWorldWidth, boardWorldHeight, 0f));

        bounds.Expand(boardPadding * 2f);
        return bounds;
    }

    private void ClampCameraPosition()
    {
        if (targetCamera == null)
        {
            return;
        }

        Bounds boardBounds = GetBoardBounds();
        float halfViewHeight = targetCamera.orthographicSize;
        float halfViewWidth = halfViewHeight * targetCamera.aspect;

        float minX = boardBounds.min.x + halfViewWidth;
        float maxX = boardBounds.max.x - halfViewWidth;
        float minY = boardBounds.min.y + halfViewHeight;
        float maxY = boardBounds.max.y - halfViewHeight;

        float clampedX = minX > maxX
            ? boardBounds.center.x
            : Mathf.Clamp(transform.position.x, minX, maxX);

        float clampedY = minY > maxY
            ? boardBounds.center.y
            : Mathf.Clamp(transform.position.y, minY, maxY);

        transform.position = new Vector3(clampedX, clampedY, transform.position.z);
    }

    private bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsPointerOverUi(int fingerId)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    private void OnValidate()
    {
        boardWidth = Mathf.Max(1, boardWidth);
        boardHeight = Mathf.Max(1, boardHeight);
        cellSize = Mathf.Max(0.01f, cellSize);
        boardLayoutCameraSize = Mathf.Max(0.01f, boardLayoutCameraSize);
        boardPadding = Mathf.Max(0f, boardPadding);
        minZoom = Mathf.Max(0.1f, minZoom);
        maxZoom = Mathf.Max(minZoom, maxZoom);
        mouseWheelZoomSpeed = Mathf.Max(0.01f, mouseWheelZoomSpeed);
        pinchZoomSpeed = Mathf.Max(0.01f, pinchZoomSpeed);
    }
}
