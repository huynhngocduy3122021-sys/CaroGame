using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class StartGameUI : MonoBehaviour
{
    private Button hostButton;
    private Button joinButton;
    private Button singleButton;

    private void Start()
    {
        Debug.Log("StartGameUI: Khởi tạo các nút trong Menu chính...");

        var hostObj = GameObject.Find("host-game ");
        if (hostObj != null)
        {
            hostButton = hostObj.GetComponent<Button>();
            if (hostButton != null)
            {
                hostButton.onClick.AddListener(() => StartGameMode(GameStartSettings.Mode.Host));
                Debug.Log("StartGameUI: Đã gán nút Host.");
            }
        }

        var joinObj = GameObject.Find("join-room");
        if (joinObj != null)
        {
            joinButton = joinObj.GetComponent<Button>();
            if (joinButton != null)
            {
                joinButton.onClick.AddListener(() => StartGameMode(GameStartSettings.Mode.Join));
                Debug.Log("StartGameUI: Đã gán nút Join.");
            }
        }

        var singleObj = GameObject.Find("single-player");
        if (singleObj != null)
        {
            singleButton = singleObj.GetComponent<Button>();
            if (singleButton != null)
            {
                singleButton.onClick.AddListener(() => StartGameMode(GameStartSettings.Mode.Single));
                Debug.Log("StartGameUI: Đã gán nút Single.");
            }
        }
    }

    private void StartGameMode(GameStartSettings.Mode mode)
    {
        GameStartSettings.StartMode = mode;
        Debug.Log($"StartGameUI: Đang chuyển sang gamePlay với chế độ {mode}");
        SceneManager.LoadScene("gamePlay");
    }
}
