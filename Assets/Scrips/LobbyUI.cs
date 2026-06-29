using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] private GameObject lobbyPanel; // Kéo Panel UI phòng chờ vào đây
    [SerializeField] private TextMeshProUGUI playerStatusText; // Text hiển thị trạng thái danh sách người chơi
    [SerializeField] private Button startGameButton; // Nút Start Game (Chỉ hiển thị cho Host)

    private void Start()
    {
        GameManager.Instance.OnLobbyPlayersChanged += GameManager_OnLobbyPlayersChanged;
        GameManager.Instance.OnGameStarted += GameManager_OnGameStarted;
        GameManager.Instance.OnGameReturnedToLobby += GameManager_OnGameReturnedToLobby;

        startGameButton.onClick.AddListener(() => {
            GameManager.Instance.StartGameFromLobby();
        });

        UpdateLobbyUI();
    }

    private void GameManager_OnLobbyPlayersChanged(object sender, System.EventArgs e)
    {
        UpdateLobbyUI();
    }

    private void GameManager_OnGameStarted(object sender, System.EventArgs e)
    {
        lobbyPanel.SetActive(false); // Ẩn giao diện Lobby đi khi Game bắt đầu công khai
    }

    private void GameManager_OnGameReturnedToLobby(object sender, System.EventArgs e)
    {
        lobbyPanel.SetActive(true);
        UpdateLobbyUI();
    }

    private void UpdateLobbyUI()
    {
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer) return;

        int connectedCount = GameManager.Instance.GetLobbyPlayerCount();
        string info = "DANH SÁCH PHÒNG CHỜ:\n";

        for (int i = 0; i < connectedCount; i++)
        {
            if (i == 0) info += $"- Người chơi 1 (X) [Host]\n";
            else if (i == 1) info += $"- Người chơi 2 (O)\n";
            else info += $"- Khán giả {i - 1} (Đang xem)\n";
        }

        playerStatusText.text = info;

        // Chỉ cho phép Máy chủ (Host) nhìn thấy và bấm được nút Start Game khi có từ 2 người trở lên
        if (GameManager.Instance.CanStartGameFromLobby())
        {
            startGameButton.gameObject.SetActive(true);
        }
        else
        {
            startGameButton.gameObject.SetActive(false);
        }
    }
}
