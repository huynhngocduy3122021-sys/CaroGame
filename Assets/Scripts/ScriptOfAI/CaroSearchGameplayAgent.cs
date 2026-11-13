using System.Collections;
using UnityEngine;

/// <summary>
/// Lớp trung gian kết nối AI Caro Alpha-Beta không dùng ML với Unity.
/// Logic tìm kiếm vẫn độc lập với GameManager và chi tiết networking.
/// </summary>
public sealed class CaroSearchGameplayAgent : MonoBehaviour
{
    [Header("Cấu hình tìm kiếm")]
    [SerializeField] private int searchDepth = 3;
    [SerializeField] private int timeLimitMilliseconds = 150;
    [SerializeField] private int candidateRadius = 2;
    [SerializeField] private int maxCandidates = 32;

    // Các thành phần điều khiển ván chơi, trạng thái bàn và bộ tìm kiếm.
    private GameManager gameManager;
    private CaroAiBoard board;
    private CaroAlphaBetaSearch search;
    private bool initialized;
    private bool waitingForDecision;

    /// <summary>Nhận GameManager hiện tại để AI gửi nước đi vào game.</summary>
    public void Configure(GameManager manager)
    {
        gameManager = manager;
    }

    /// <summary>Khởi tạo bàn tìm kiếm và đăng ký các sự kiện của GameManager.</summary>
    private void OnEnable()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (gameManager == null)
        {
            Debug.LogError("CaroSearchGameplayAgent yêu cầu có GameManager.");
            enabled = false;
            return;
        }

        board = new CaroAiBoard(GameManager.BoardPointsX, GameManager.BoardPointsY);
        search = new CaroAlphaBetaSearch(
            CaroAiRules.DefaultWinLength,
            candidateRadius,
            maxCandidates);

        gameManager.OnGripPositionClicked += OnMovePlaced;
        gameManager.OnRematch += OnRematch;
        initialized = true;
        Debug.Log("CaroSearchGameplayAgent đã kết nối với GameManager.");
    }

    /// <summary>
    /// Đồng bộ từng nước đi thật vào bàn cờ nội bộ của AI.
    /// Khi người chơi là Cross vừa đánh xong, bắt đầu tính nước Circle.
    /// </summary>
    private void OnMovePlaced(
        object sender,
        GameManager.OnGripPositionClickedEventArgs eventArgs)
    {
        if (!board.TryApply(
                eventArgs.x,
                eventArgs.y,
                eventArgs.playerType == GameManager.PlayerType.Circle
                    ? CaroAiRules.AiPlayer
                    : CaroAiRules.OpponentPlayer))
        {
            Debug.LogWarning($"AI tìm kiếm bỏ qua cập nhật bàn cờ không hợp lệ ({eventArgs.x}, {eventArgs.y}).");
            return;
        }

        if (eventArgs.playerType == GameManager.PlayerType.Cross &&
            !waitingForDecision &&
            gameManager.IsAIGame())
        {
            waitingForDecision = true;
            StartCoroutine(RequestSearchMoveAfterGameManagerSettles());
        }
    }

    /// <summary>Đợi GameManager cập nhật xong lượt rồi mới tìm và gửi nước AI.</summary>
    private IEnumerator RequestSearchMoveAfterGameManagerSettles()
    {
        yield return null;

        if (gameManager == null ||
            !gameManager.IsAIGame() ||
            gameManager.GetCurrentPlayerType() != GameManager.PlayerType.Circle)
        {
            waitingForDecision = false;
            yield break;
        }

        int move = search.FindBestMove(
            board,
            CaroAiRules.AiPlayer,
            timeLimitMilliseconds,
            searchDepth);

        waitingForDecision = false;

        if (move < 0)
        {
            Debug.LogWarning("AI tìm kiếm không tìm được nước đi hợp lệ.");
            yield break;
        }

        int x = move % board.Width;
        int y = move / board.Width;

        if (!gameManager.TryPlaceAIMove(x, y))
        {
            Debug.LogWarning($"Nước đi AI ({x}, {y}) bị GameManager từ chối.");
        }
    }

    /// <summary>Xóa trạng thái bàn cờ nội bộ khi người chơi bắt đầu ván mới.</summary>
    private void OnRematch(object sender, System.EventArgs eventArgs)
    {
        board?.Clear();
        waitingForDecision = false;
    }

    /// <summary>Hủy đăng ký sự kiện để tránh callback vào object đã bị hủy.</summary>
    private void OnDestroy()
    {
        if (!initialized || gameManager == null)
        {
            return;
        }

        gameManager.OnGripPositionClicked -= OnMovePlaced;
        gameManager.OnRematch -= OnRematch;
    }
}
