using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] private GameObject lobbyPanel; 
    [SerializeField] private TextMeshProUGUI playerStatusText; 
    [SerializeField] private Button startGameButton; 

    private void Start()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnLobbyJoined += OnLobbyJoinedCallback;
            LobbyManager.Instance.OnLobbyLeft += OnLobbyLeftCallback;
            LobbyManager.Instance.OnPlayerJoined += OnPlayerJoinedCallback;
            LobbyManager.Instance.OnPlayerLeft += OnPlayerLeftCallback;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLobbyPlayersChanged += GameManager_OnLobbyPlayersChanged;
            GameManager.Instance.OnGameStarted += GameManager_OnGameStarted;
            GameManager.Instance.OnGameReturnedToLobby += GameManager_OnGameReturnedToLobby;
        }

        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveAllListeners();
            startGameButton.onClick.AddListener(() => {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.StartGameFromLobby();
                }
            });
        }

        UpdateLobbyUI();
    }

    private void OnDestroy()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnLobbyJoined -= OnLobbyJoinedCallback;
            LobbyManager.Instance.OnLobbyLeft -= OnLobbyLeftCallback;
            LobbyManager.Instance.OnPlayerJoined -= OnPlayerJoinedCallback;
            LobbyManager.Instance.OnPlayerLeft -= OnPlayerLeftCallback;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLobbyPlayersChanged -= GameManager_OnLobbyPlayersChanged;
            GameManager.Instance.OnGameStarted -= GameManager_OnGameStarted;
            GameManager.Instance.OnGameReturnedToLobby -= GameManager_OnGameReturnedToLobby;
        }
    }

    private void OnLobbyJoinedCallback(LobbyData data)
    {
        UpdateLobbyUI();
    }

    private void OnLobbyLeftCallback()
    {
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(false);
        }
    }

    private void OnPlayerJoinedCallback(string playerId)
    {
        UpdateLobbyUI();
    }

    private void OnPlayerLeftCallback(string playerId)
    {
        UpdateLobbyUI();
    }

    private void GameManager_OnLobbyPlayersChanged(object sender, System.EventArgs e)
    {
        UpdateLobbyUI();
    }

    private void GameManager_OnGameStarted(object sender, System.EventArgs e)
    {
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(false); 
        }
    }

    private void GameManager_OnGameReturnedToLobby(object sender, System.EventArgs e)
    {
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(true);
        }
        UpdateLobbyUI();
    }

    private void UpdateLobbyUI()
    {
        if (NetworkManager.Singleton == null || (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)) 
        {
            return;
        }

        int connectedCount = GameManager.Instance != null ? GameManager.Instance.GetLobbyPlayerCount() : 0;
        string info = "DANH SÁCH PHÒNG CHỜ:\n";

        for (int i = 0; i < connectedCount; i++)
        {
            if (i == 0) info += $"- Người chơi 1 (X) [Host]\n";
            else if (i == 1) info += $"- Người chơi 2 (O)\n";
            else info += $"- Khán giả {i - 1} (Đang xem)\n";
        }

        if (playerStatusText != null)
        {
            playerStatusText.text = info;
        }

        if (startGameButton != null)
        {
            bool canStart = GameManager.Instance != null && GameManager.Instance.CanStartGameFromLobby();
            startGameButton.gameObject.SetActive(canStart);
        }
    }
}
