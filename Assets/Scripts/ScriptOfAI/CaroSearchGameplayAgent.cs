using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Lớp trung gian kết nối AI Caro Alpha-Beta không dùng ML với Unity.
/// Logic tìm kiếm vẫn độc lập với GameManager và chi tiết networking.
/// </summary>
public sealed class CaroSearchGameplayAgent : MonoBehaviour
{
    [Header("Độ khó AI")]
    [SerializeField] private CaroDifficulty difficulty = CaroDifficulty.Hard;

    // Các thành phần điều khiển ván chơi, trạng thái bàn và bộ tìm kiếm.
    private GameManager gameManager;
    private CaroAiBoard board;
    private CaroDifficultySettings settings;
    private int boardRevision;
    private bool initialized;
    private bool waitingForDecision;

    /// <summary>Nhận GameManager và độ khó trước khi bật Agent.</summary>
    public void Configure(GameManager manager, CaroDifficulty selectedDifficulty)
    {
        gameManager = manager;
        difficulty = selectedDifficulty;
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
        settings = CaroDifficultySettings.For(difficulty);
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
        boardRevision++;

        if (eventArgs.playerType == GameManager.PlayerType.Cross &&
            !waitingForDecision &&
            gameManager.IsAIGame())
        {
            waitingForDecision = true;
            StartCoroutine(RequestSearchMoveAfterGameManagerSettles());
        }
    }

    /// <summary>Đợi lượt ổn định, tính trên bản sao bàn cờ ở luồng nền rồi đặt nước hợp lệ.</summary>
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

        // Tạo bản sao trên main thread; worker chỉ đụng dữ liệu C# thuần, không gọi Unity API.
        int revision = boardRevision;
        var snapshot = new CaroAiBoard(board.Width, board.Height);
        for (int index = 0; index < board.CellCount; index++)
        {
            if (board.Cells[index] != 0) snapshot.ApplyIndex(index, board.Cells[index]);
        }
        CaroDifficultySettings searchSettings = settings;
        bool isHard = difficulty == CaroDifficulty.Hard;
        Task<int> task = Task.Run(() =>
        {
            var search = new CaroAlphaBetaSearch(
                CaroAiRules.DefaultWinLength,
                searchSettings.CandidateRadius,
                searchSettings.MaxCandidates);
            return search.FindBestMove(snapshot, CaroAiRules.AiPlayer,
                searchSettings.TimeLimitMs, searchSettings.Depth, isHard);
        });
        while (!task.IsCompleted) yield return null;

        waitingForDecision = false;
        if (task.IsFaulted)
        {
            Debug.LogError($"AI tìm kiếm gặp lỗi: {task.Exception}");
            yield break;
        }
        if (revision != boardRevision || gameManager == null ||
            !gameManager.IsAIGame() ||
            gameManager.GetCurrentPlayerType() != GameManager.PlayerType.Circle)
        {
            yield break;
        }
        int move = task.Result;

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
        boardRevision++;
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
