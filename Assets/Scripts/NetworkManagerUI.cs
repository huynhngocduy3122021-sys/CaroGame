using System;
using System.Net;
using System.Net.Sockets;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using Unity.InferenceEngine;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

public class NetworkManagerUI : MonoBehaviour
{
    public static NetworkManagerUI Instance { get; private set; }
    
    [Header("Connection")]
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button startClientButton;
    [SerializeField] private TMP_InputField inviteInputField;
    [SerializeField] private TMP_InputField portInputField;
    [SerializeField] private TextMeshProUGUI connectionStatusText;
    [SerializeField] private ModelAsset aiModel;

    [Header("Lobby")]
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private TextMeshProUGUI inviteCodeText;
    [SerializeField] private TextMeshProUGUI playerStatusText;
    [SerializeField] private TextMeshProUGUI lobbyStatusText;
    [SerializeField] private Button copyInviteButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private GameObject gameplayPanel;
    [SerializeField] private Button gameplayExitButton;

    [Header("Connection Settings")]
    [SerializeField] private int defaultPort = 7777;

    private const string DefaultClientAddress = "127.0.0.1";
    private const string HostListenAddress = "0.0.0.0";

    private UnityTransport unityTransport;
    private string currentInviteAddress = DefaultClientAddress;
    private ushort currentPort = 7777;
    private bool subscribedToGameManager;

    // Dynamic UI controls built at runtime
    private Button publicTypeBtn;
    private Button privateTypeBtn;
    private Button rankingTypeBtn;
    private TMP_InputField lobbyNameInputField;
    private TMP_InputField lobbyPasswordInputField;
    private Button quickJoinButton;
    private Button refreshLobbiesButton;
    private GameObject lobbyListContainer;
    
    private LobbyType selectedLobbyType = LobbyType.Public;
    private TMP_FontAsset vietnameseFont;

    private void SetupVietnameseFont()
    {
        try
        {
            string winFontPath = @"C:\Windows\Fonts\segoeui.ttf";
            if (!System.IO.File.Exists(winFontPath))
            {
                winFontPath = @"C:\Windows\Fonts\arial.ttf";
            }
            if (!System.IO.File.Exists(winFontPath))
            {
                winFontPath = @"C:\Windows\Fonts\calibri.ttf";
            }

            if (System.IO.File.Exists(winFontPath))
            {
                Font font = new Font(winFontPath);
                vietnameseFont = TMP_FontAsset.CreateFontAsset(font);
                vietnameseFont.name = "SegoeUI SDF (Dynamic)";
                vietnameseFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;

                // Also register as fallback to default font so any existing text in the scene handles Vietnamese
                TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
                if (defaultFont != null)
                {
                    if (defaultFont.fallbackFontAssetTable == null)
                    {
                        defaultFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                    }
                    if (!defaultFont.fallbackFontAssetTable.Contains(vietnameseFont))
                    {
                        defaultFont.fallbackFontAssetTable.Add(vietnameseFont);
                        Debug.Log("Segoe UI registered as fallback font for " + defaultFont.name);
                    }
                }
                Debug.Log("Successfully created dynamic Vietnamese Font Asset at runtime from " + winFontPath);
            }
            else
            {
                Debug.LogWarning("No suitable system font found for Vietnamese rendering fallback.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error initializing Vietnamese font at runtime: " + ex.Message);
        }
    }

    private void Awake()
    {
        SetupVietnameseFont();
        Instance = this;
        currentPort = NormalizePort(defaultPort);
        unityTransport = FindUnityTransport();

        // Self-healing initialization of LobbyManager singleton if missing from the scene
        if (LobbyManager.Instance == null)
        {
            GameObject lmObj = new GameObject("LobbyManager", typeof(LobbyManager));
        }

        if (NeedsRuntimeUI())
        {
            BuildRuntimeUI();
        }

        WireUIEvents();
        SubscribeNetworkCallbacks();

        if (GameStartSettings.StartMode != GameStartSettings.Mode.Single && GameStartSettings.StartMode != GameStartSettings.Mode.Host)
        {
            ShowConnectionPanel("Tạo phòng mới hoặc nhập mã mời để tham gia.");
        }
    }

    private void Start()
    {
        TrySubscribeGameManager();

        // Wire event handlers to LobbyManager events (reactive UI updates)
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnLobbyCreated += OnLobbyCreatedCallback;
            LobbyManager.Instance.OnLobbyJoined += OnLobbyJoinedCallback;
            LobbyManager.Instance.OnLobbyLeft += OnLobbyLeftCallback;
            LobbyManager.Instance.OnPlayerJoined += OnPlayerJoinedCallback;
            LobbyManager.Instance.OnPlayerLeft += OnPlayerLeftCallback;
            LobbyManager.Instance.OnLobbyDeleted += OnLobbyDeletedCallback;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            ShowLobbyPanel("Đã kết nối lobby.");
            return;
        }

        // Auto-action based on start mode
        switch (GameStartSettings.StartMode)
        {
            case GameStartSettings.Mode.Host:
                StartHost();
                break;
            case GameStartSettings.Mode.Join:
                ShowConnectionPanel("Nhập mã mời để tham gia sảnh caro.");
                break;
            case GameStartSettings.Mode.Single:
                // Start Host first to enable Netcode server role, then start AI mode when ready
                if (CanStartNetwork())
                {
                    if (TryReadPort(out currentPort))
                    {
                        unityTransport.SetConnectionData(DefaultClientAddress, currentPort, HostListenAddress);
                        currentInviteAddress = GetLocalIPv4Address();

                        bool started = NetworkManager.Singleton.StartHost();
                        if (started)
                        {
                            ShowGameplayPanel();
                            StartCoroutine(StartAIGameWhenReady());
                        }
                        else
                        {
                            ShowConnectionPanel("Không thể khởi động chế độ chơi với AI.");
                        }
                    }
                }
                break;
            default:
                ShowConnectionPanel("Tạo phòng mới hoặc nhập mã mời để tham gia.");
                break;
        }
    }

    private void Update()
    {
        if (!subscribedToGameManager)
        {
            TrySubscribeGameManager();
        }
    }

    private void OnDestroy()
    {
        if (startHostButton != null) startHostButton.onClick.RemoveListener(StartHost);
        if (startClientButton != null) startClientButton.onClick.RemoveListener(StartClient);
        if (copyInviteButton != null) copyInviteButton.onClick.RemoveListener(CopyInviteCode);
        if (startGameButton != null) startGameButton.onClick.RemoveListener(StartGameFromLobby);
        if (leaveLobbyButton != null) leaveLobbyButton.onClick.RemoveListener(LeaveLobby);
        if (gameplayExitButton != null) gameplayExitButton.onClick.RemoveListener(LeaveLobby);

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnLobbyCreated -= OnLobbyCreatedCallback;
            LobbyManager.Instance.OnLobbyJoined -= OnLobbyJoinedCallback;
            LobbyManager.Instance.OnLobbyLeft -= OnLobbyLeftCallback;
            LobbyManager.Instance.OnPlayerJoined -= OnPlayerJoinedCallback;
            LobbyManager.Instance.OnPlayerLeft -= OnPlayerLeftCallback;
            LobbyManager.Instance.OnLobbyDeleted -= OnLobbyDeletedCallback;
        }

        if (GameManager.Instance != null && subscribedToGameManager)
        {
            GameManager.Instance.OnLobbyPlayersChanged -= GameManager_OnLobbyPlayersChanged;
            GameManager.Instance.OnGameStarted -= GameManager_OnGameStarted;
            GameManager.Instance.OnGameReturnedToLobby -= GameManager_OnGameReturnedToLobby;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= NetworkManager_OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback -= NetworkManager_OnClientDisconnectCallback;
        }
    }

    private bool NeedsRuntimeUI()
    {
        return connectionPanel == null ||
            lobbyPanel == null ||
            inviteInputField == null ||
            portInputField == null ||
            inviteCodeText == null ||
            playerStatusText == null ||
            lobbyStatusText == null ||
            copyInviteButton == null ||
            startGameButton == null ||
            leaveLobbyButton == null ||
            gameplayPanel == null ||
            gameplayExitButton == null;
    }

    private void WireUIEvents()
    {
        if (startHostButton != null)
        {
            startHostButton.onClick.RemoveListener(StartHost);
            startHostButton.onClick.AddListener(StartHost);
        }

        if (startClientButton != null)
        {
            startClientButton.onClick.RemoveListener(StartClient);
            startClientButton.onClick.AddListener(StartClient);
        }

        if (copyInviteButton != null)
        {
            copyInviteButton.onClick.RemoveListener(CopyInviteCode);
            copyInviteButton.onClick.AddListener(CopyInviteCode);
        }

        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveListener(StartGameFromLobby);
            startGameButton.onClick.AddListener(StartGameFromLobby);
        }

        if (leaveLobbyButton != null)
        {
            leaveLobbyButton.onClick.RemoveListener(LeaveLobby);
            leaveLobbyButton.onClick.AddListener(LeaveLobby);
        }

        if (gameplayExitButton != null)
        {
            gameplayExitButton.onClick.RemoveListener(LeaveLobby);
            gameplayExitButton.onClick.AddListener(LeaveLobby);
        }

        // Strategy selection & browser event hooks
        if (publicTypeBtn != null)
        {
            publicTypeBtn.onClick.AddListener(() => SelectLobbyType(LobbyType.Public));
        }
        if (privateTypeBtn != null)
        {
            privateTypeBtn.onClick.AddListener(() => SelectLobbyType(LobbyType.Private));
        }
        if (rankingTypeBtn != null)
        {
            rankingTypeBtn.onClick.AddListener(() => SelectLobbyType(LobbyType.Ranking));
        }
        if (quickJoinButton != null)
        {
            quickJoinButton.onClick.AddListener(QuickJoin);
        }
        if (refreshLobbiesButton != null)
        {
            refreshLobbiesButton.onClick.AddListener(RefreshLobbyListUI);
        }

        if (portInputField != null)
        {
            portInputField.onValueChanged.RemoveAllListeners();
            portInputField.text = currentPort.ToString();
            portInputField.onValueChanged.AddListener(_ => UpdateInvitePreview());
        }

        if (inviteInputField != null && string.IsNullOrWhiteSpace(inviteInputField.text))
        {
            inviteInputField.text = $"{DefaultClientAddress}:{currentPort}";
        }

        UpdateInvitePreview();
        SelectLobbyType(LobbyType.Public);
        RefreshLobbyListUI();
    }

    private void SubscribeNetworkCallbacks()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback -= NetworkManager_OnClientConnectedCallback;
        NetworkManager.Singleton.OnClientDisconnectCallback -= NetworkManager_OnClientDisconnectCallback;
        NetworkManager.Singleton.OnClientConnectedCallback += NetworkManager_OnClientConnectedCallback;
        NetworkManager.Singleton.OnClientDisconnectCallback += NetworkManager_OnClientDisconnectCallback;
    }

    private void TrySubscribeGameManager()
    {
        if (GameManager.Instance == null || subscribedToGameManager)
        {
            return;
        }

        GameManager.Instance.OnLobbyPlayersChanged += GameManager_OnLobbyPlayersChanged;
        GameManager.Instance.OnGameStarted += GameManager_OnGameStarted;
        GameManager.Instance.OnGameReturnedToLobby += GameManager_OnGameReturnedToLobby;
        subscribedToGameManager = true;
        UpdateLobbyUI();
    }

    // Reactive callback registrations
    private void OnLobbyCreatedCallback(LobbyData lobbyData)
    {
        Debug.Log($"[DIAGNOSTIC] Lobby Created | Id: '{lobbyData.LobbyId}', Name: '{lobbyData.LobbyName}', IsPrivate: {lobbyData.IsPrivate}");
        currentInviteAddress = GetLocalIPv4Address();
        ShowLobbyPanel("Phòng đã tạo. Gửi mã mời cho người chơi khác.");
    }

    private void OnLobbyJoinedCallback(LobbyData lobbyData)
    {
        ParseConnectionCode(lobbyData.ConnectionCode, out string address, out ushort port);
        currentInviteAddress = address;
        currentPort = port;
        ShowLobbyPanel("Đã kết nối!");
    }

    private void OnLobbyLeftCallback()
    {
        ShowConnectionPanel("Đã rời sảnh.");
    }

    private void OnLobbyDeletedCallback()
    {
        ShowConnectionPanel("Sảnh đã bị xóa.");
    }

    private void OnPlayerJoinedCallback(string playerId)
    {
        UpdateLobbyUI();
    }

    private void OnPlayerLeftCallback(string playerId)
    {
        UpdateLobbyUI();
    }

    private void StartHost()
    {
        if (!CanStartNetwork())
        {
            return;
        }

        if (!TryReadPort(out currentPort))
        {
            return;
        }

        string lobbyName = lobbyNameInputField != null ? lobbyNameInputField.text : "Phòng Caro";
        string password = lobbyPasswordInputField != null ? lobbyPasswordInputField.text : string.Empty;

        LobbyData data = new LobbyData
        {
            LobbyName = string.IsNullOrWhiteSpace(lobbyName) ? "Phòng Caro" : lobbyName,
            Type = selectedLobbyType,
            Password = password,
            MaxPlayers = 2,
            ConnectionCode = "127.0.0.1:" + currentPort
        };

        SetConnectionStatus("Đang tạo phòng...");

        LobbyManager.Instance.CreateLobby(data, (success, message) =>
        {
            if (!success)
            {
                SetConnectionStatus("Lỗi tạo phòng: " + message);
            }
        });
    }

    private void StartClient()
    {
        if (!CanStartNetwork())
        {
            return;
        }

        if (!TryParseInvite(out string address, out ushort port))
        {
            return;
        }

        string password = lobbyPasswordInputField != null ? lobbyPasswordInputField.text : string.Empty;

        LobbyData data = new LobbyData
        {
            ConnectionCode = address + ":" + port,
            Password = password,
            Type = LobbyType.Private
        };

        SetConnectionStatus("Đang kết nối đến phòng...");

        LobbyManager.Instance.JoinLobby(data, (success, message) =>
        {
            if (!success)
            {
                SetConnectionStatus("Không thể tham gia: " + message);
            }
        });
    }

    private void QuickJoin()
    {
        if (!CanStartNetwork())
        {
            return;
        }

        SetConnectionStatus("Đang tìm phòng vào nhanh...");

        LobbyManager.Instance.QuickJoin(LobbyType.Public, (success, message) =>
        {
            if (!success)
            {
                SetConnectionStatus("Vào nhanh thất bại: " + message);
            }
        });
    }

    private void ShowPasswordDialog(LobbyData lobby)
    {
        GameObject dialogOverlay = CreateFullscreenPanel("PasswordDialogOverlay", new Color(0, 0, 0, 0.75f));
        dialogOverlay.transform.SetParent(transform, false);

        GameObject box = CreateUIObject("DialogBox", dialogOverlay.transform);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = Vector2.zero;
        boxRect.sizeDelta = new Vector2(360f, 220f);

        Image boxBg = box.AddComponent<Image>();
        boxBg.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        boxBg.type = Image.Type.Sliced;
        boxBg.color = new Color(0.15f, 0.13f, 0.11f, 1f);

        Color goldColor = new Color(0.9f, 0.75f, 0.15f, 1f);

        var title = CreateText("DialogTitle", box.transform, "NHẬP MẬT KHẨU PHÒNG", 16f, TextAlignmentOptions.Center, new Vector2(320f, 30f), new Vector2(0f, 70f), goldColor);
        title.fontStyle = FontStyles.Bold;

        CreateText("LobbyInfo", box.transform, $"Phòng: {lobby.LobbyName}", 13f, TextAlignmentOptions.Center, new Vector2(320f, 25f), new Vector2(0f, 40f), Color.white);

        TMP_InputField pwdInput = CreateInput(box.transform, "DialogPasswordInput", "Mật khẩu", new Vector2(280f, 40f), new Vector2(0f, -10f));
        pwdInput.contentType = TMP_InputField.ContentType.Password;

        var statusText = CreateText("DialogStatusText", box.transform, string.Empty, 12f, TextAlignmentOptions.Center, new Vector2(320f, 25f), new Vector2(0f, -50f), new Color(0.9f, 0.3f, 0.3f, 1f));

        var confirmBtn = CreateButton(box.transform, "ConfirmBtn", "Vào", new Vector2(100f, 35f), new Vector2(-60f, -90f), new Color(0.1f, 0.55f, 0.35f, 1f));
        var cancelBtn = CreateButton(box.transform, "CancelBtn", "Hủy", new Vector2(100f, 35f), new Vector2(60f, -90f), new Color(0.55f, 0.2f, 0.2f, 1f));

        cancelBtn.onClick.AddListener(() => {
            Destroy(dialogOverlay);
        });

        confirmBtn.onClick.AddListener(() => {
            string password = pwdInput.text;
            lobby.Password = password;

            statusText.text = "Đang kiểm tra mật khẩu...";
            confirmBtn.interactable = false;
            cancelBtn.interactable = false;

            LobbyManager.Instance.JoinLobby(lobby, (success, message) => {
                if (success)
                {
                    Destroy(dialogOverlay);
                }
                else
                {
                    statusText.text = message;
                    confirmBtn.interactable = true;
                    cancelBtn.interactable = true;
                }
            });
        });
    }

    private void JoinMockLobby(LobbyData lobby)
    {
        if (!CanStartNetwork())
        {
            return;
        }

        if (lobby.Type == LobbyType.Private)
        {
            ShowPasswordDialog(lobby);
        }
        else
        {
            SetConnectionStatus($"Đang kết nối đến {lobby.LobbyName}...");
            LobbyManager.Instance.JoinLobby(lobby, (success, message) =>
            {
                if (!success)
                {
                    SetConnectionStatus("Kết nối thất bại: " + message);
                }
            });
        }
    }

    private void StartGameFromLobby()
    {
        if (GameManager.Instance == null)
        {
            SetLobbyStatus("GameManager chưa sẵn sàng.");
            return;
        }

        if (!GameManager.Instance.CanStartGameFromLobby())
        {
            SetLobbyStatus("Cần ít nhất 2 người chơi để bắt đầu.");
            return;
        }

        GameManager.Instance.StartGameFromLobby();
    }

    public void LeaveLobby()
    {
        LobbyManager.Instance.LeaveLobby((success, message) =>
        {
            if (GameStartSettings.StartMode == GameStartSettings.Mode.Single)
            {
                if (NetworkManager.Singleton != null)
                {
                    Destroy(NetworkManager.Singleton.gameObject);
                }
                UnityEngine.SceneManagement.SceneManager.LoadScene("StartGame");
            }
            else
            {
                ShowConnectionPanel("Đã rời lobby.");
            }
        });
    }

    private bool CanStartNetwork()
    {
        if (NetworkManager.Singleton == null)
        {
            ShowConnectionPanel("Không tìm thấy NetworkManager trong scene.");
            return false;
        }

        unityTransport = FindUnityTransport();
        if (unityTransport == null)
        {
            ShowConnectionPanel("Không tìm thấy UnityTransport trên NetworkManager.");
            return false;
        }

        if (NetworkManager.Singleton.IsListening)
        {
            ShowLobbyPanel("Bạn đang ở trong lobby.");
            return false;
        }

        return true;
    }

    private UnityTransport FindUnityTransport()
    {
        if (NetworkManager.Singleton == null)
        {
            return null;
        }

        return NetworkManager.Singleton.GetComponent<UnityTransport>();
    }

    private bool TryReadPort(out ushort port)
    {
        string rawPort = portInputField != null ? portInputField.text : defaultPort.ToString();

        if (string.IsNullOrWhiteSpace(rawPort))
        {
            port = NormalizePort(defaultPort);
            return true;
        }

        if (ushort.TryParse(rawPort.Trim(), out port) && port > 0)
        {
            return true;
        }

        ShowConnectionPanel("Port phải nằm trong khoảng 1 - 65535.");
        return false;
    }

    private bool TryParseInvite(out string address, out ushort port)
    {
        address = DefaultClientAddress;

        if (!TryReadPort(out port))
        {
            return false;
        }

        string rawInvite = inviteInputField != null ? inviteInputField.text : string.Empty;
        rawInvite = (rawInvite ?? string.Empty).Trim();

        if (rawInvite.StartsWith("caro://", StringComparison.OrdinalIgnoreCase))
        {
            rawInvite = rawInvite.Substring("caro://".Length);
        }

        if (string.IsNullOrWhiteSpace(rawInvite))
        {
            return true;
        }

        int portSeparatorIndex = rawInvite.LastIndexOf(':');
        if (portSeparatorIndex > 0 && portSeparatorIndex < rawInvite.Length - 1)
        {
            string parsedAddress = rawInvite.Substring(0, portSeparatorIndex).Trim();
            string parsedPort = rawInvite.Substring(portSeparatorIndex + 1).Trim();

            if (!ushort.TryParse(parsedPort, out port) || port == 0)
            {
                ShowConnectionPanel("Mã mời sai định dạng. Dùng IP:PORT, ví dụ 192.168.1.8:7777.");
                return false;
            }

            address = parsedAddress;
        }
        else
        {
            address = rawInvite;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            ShowConnectionPanel("Mã mời cần có IP hoặc hostname.");
            return false;
        }

        if (portInputField != null)
        {
            portInputField.text = port.ToString();
        }

        return true;
    }

    private ushort NormalizePort(int value)
    {
        return (ushort)Mathf.Clamp(value, 1, ushort.MaxValue);
    }

    private void CopyInviteCode()
    {
        string inviteCode = GetInviteCode();
        GUIUtility.systemCopyBuffer = inviteCode;
        SetLobbyStatus("Đã sao chép mã mời: " + inviteCode);
    }

    private void NetworkManager_OnClientConnectedCallback(ulong clientId)
    {
        UpdateLobbyUI();
    }

    private void NetworkManager_OnClientDisconnectCallback(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (GameStartSettings.StartMode == GameStartSettings.Mode.Single)
            {
                return;
            }
            ShowConnectionPanel("Mất kết nối lobby.");
            return;
        }

        UpdateLobbyUI();
    }

    private void GameManager_OnLobbyPlayersChanged(object sender, EventArgs e)
    {
        UpdateLobbyUI();
    }

    private void GameManager_OnGameStarted(object sender, EventArgs e)
    {
        ShowGameplayPanel();
    }

    private void GameManager_OnGameReturnedToLobby(object sender, EventArgs e)
    {
        ShowLobbyPanel("Một người chơi đã thoát. Phòng vẫn mở.");
    }

    private void ShowConnectionPanel(string message)
    {
        gameObject.SetActive(true);
        SetPanelState(true, false, false);
        SetConnectionStatus(message);
        RefreshLobbyListUI();
    }

    private void ShowLobbyPanel(string message)
    {
        gameObject.SetActive(true);
        SetPanelState(false, true, false);
        SetLobbyStatus(message);
        UpdateLobbyUI();
    }

    private void ShowGameplayPanel()
    {
        gameObject.SetActive(true);
        SetPanelState(false, false, true);
    }

    private void SetPanelState(bool showConnection, bool showLobby, bool showGameplay)
    {
        if (connectionPanel != null)
        {
            connectionPanel.SetActive(showConnection);
        }

        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(showLobby);
        }

        if (gameplayPanel != null)
        {
            gameplayPanel.SetActive(showGameplay);
        }
    }

    private void UpdateInvitePreview()
    {
        if (TryReadPortSilently(out ushort parsedPort))
        {
            currentPort = parsedPort;
        }

        if (inviteCodeText != null)
        {
            inviteCodeText.text = "<b>Mã mời:</b> <color=#fbbf24>" + GetInviteCode() + "</color>";
        }
    }

    private bool TryReadPortSilently(out ushort port)
    {
        string rawPort = portInputField != null ? portInputField.text : defaultPort.ToString();

        if (string.IsNullOrWhiteSpace(rawPort))
        {
            port = NormalizePort(defaultPort);
            return true;
        }

        return ushort.TryParse(rawPort.Trim(), out port) && port > 0;
    }

    private string GetInviteCode()
    {
        string inviteAddress = string.IsNullOrWhiteSpace(currentInviteAddress) ? GetLocalIPv4Address() : currentInviteAddress;
        return inviteAddress + ":" + currentPort;
    }

    private void UpdateLobbyUI()
    {
        if (playerStatusText == null)
        {
            return;
        }

        int connectedCount = GetLobbyPlayerCount();
        string info = "<align=center><size=115%><color=#f59e0b><b>DANH SÁCH THÀNH VIÊN</b></color></size></align>\n\n";

        for (int i = 0; i < connectedCount; i++)
        {
            if (i == 0) info += "  <color=#f59e0b>★</color> <b>Người chơi 1 (X)</b> <color=#f59e0b>[CHỦ PHÒNG]</color>\n";
            else if (i == 1) info += "  <color=#3b82f6>●</color> <b>Người chơi 2 (O)</b> <color=#3b82f6>[ĐÃ VÀO]</color>\n";
            else info += $"  <color=#9ca3af>●</color> Khán giả {i - 1} (đang xem)\n";
        }

        if (connectedCount == 0)
        {
            info += "  <color=#ef4444>●</color> <color=#9ca3af><i>Đang chờ kết nối...</i></color>\n";
        }

        if (GameManager.Instance != null)
        {
            string roleColor = "#9ca3af";
            var role = GameManager.Instance.GetLocalPlayerType();
            if (role == GameManager.PlayerType.Cross) roleColor = "#f59e0b";
            else if (role == GameManager.PlayerType.Circle) roleColor = "#3b82f6";
            
            info += $"\n<align=center><size=95%>Vai trò của bạn: <color={roleColor}><b>{FormatRole(role)}</b></color></size></align>";
        }

        playerStatusText.text = info;

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        bool canStart = GameManager.Instance != null && GameManager.Instance.CanStartGameFromLobby();

        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isHost);
            startGameButton.interactable = canStart;
        }

        if (!isHost)
        {
            SetLobbyStatus("<align=center><color=#9ca3af>Trạng thái trận đấu:</color>\n\n<size=115%><color=#fbbf24><b>ĐANG ĐỢI CHỦ PHÒNG</b></color></size></align>");
        }
        else if (connectedCount < 2)
        {
            SetLobbyStatus("<align=center><color=#9ca3af>Trạng thái trận đấu:</color>\n\n<size=115%><color=#ef4444><b>ĐANG ĐỢI NGƯỜI CHƠI THỨ 2</b></color></size></align>");
        }
        else
        {
            SetLobbyStatus("<align=center><color=#9ca3af>Trạng thái trận đấu:</color>\n\n<size=115%><color=#10b981><b>PHÒNG ĐÃ SẴN SÀNG!</b></color></size></align>");
        }

        if (inviteCodeText != null)
        {
            inviteCodeText.text = "<b>Mã mời:</b> <color=#fbbf24>" + GetInviteCode() + "</color>";
        }
    }

    private int GetLobbyPlayerCount()
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetLobbyPlayerCount();
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            return NetworkManager.Singleton.ConnectedClientsList.Count;
        }

        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening ? 1 : 0;
    }

    private string FormatRole(GameManager.PlayerType playerType)
    {
        switch (playerType)
        {
            case GameManager.PlayerType.Cross:
                return "Người chơi 1 (X)";
            case GameManager.PlayerType.Circle:
                return "Người chơi 2 (O)";
            default:
                return "Khán giả";
        }
    }

    private void SetConnectionStatus(string message)
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = message;
        }
    }

    private void SetLobbyStatus(string message)
    {
        if (lobbyStatusText != null)
        {
            lobbyStatusText.text = message;
        }
    }

    private string GetLocalIPv4Address()
    {
        try
        {
            IPAddress[] addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
            for (int i = 0; i < addresses.Length; i++)
            {
                IPAddress address = addresses[i];
                if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                {
                    return address.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Could not read local IPv4 address: " + ex.Message);
        }

        return DefaultClientAddress;
    }

    private Sprite GetGameBackgroundSprite()
    {
        GameObject bgObj = GameObject.Find("BackGround");
        if (bgObj != null)
        {
            SpriteRenderer bgSR = bgObj.GetComponent<SpriteRenderer>();
            if (bgSR != null)
            {
                return bgSR.sprite;
            }
        }
        return null;
    }

    private void BuildRuntimeUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }

        RectTransform rootRect = GetOrAddRectTransform(gameObject);
        Stretch(rootRect);

        Color panelTint = new Color(0.09f, 0.08f, 0.07f, 0.93f);
        Color boxBg = new Color(0.16f, 0.14f, 0.12f, 0.98f);
        Color boxBorder = new Color(0.85f, 0.6f, 0.2f, 0.35f); 
        Color innerCardBg = new Color(0.1f, 0.09f, 0.08f, 0.95f);
        Color innerCardBorder = new Color(0.35f, 0.28f, 0.22f, 0.6f);
        Color goldColor = new Color(0.96f, 0.72f, 0.2f, 1f);

        // 1. Connection Panel (Login Screen)
        connectionPanel = CreateFullscreenPanel("ConnectionPanel", panelTint);
        
        GameObject connectionBox = CreateBox(
            "ConnectionBox", 
            connectionPanel.transform, 
            new Vector2(840f, 500f), 
            Vector2.zero, 
            boxBg, 
            boxBorder
        );
        var animBox = connectionBox.AddComponent<UIAnimate>();
        animBox.animType = UIAnimate.AnimationType.CardPopIn;

        // LEFT-SIDE Creation Form (Form setup centered around x = -200)
        var titleText = CreateText("Title", connectionBox.transform, "CARO ONLINE", 32f, TextAlignmentOptions.Center, new Vector2(360f, 50f), new Vector2(-200f, 200f), goldColor);
        titleText.fontStyle = FontStyles.Bold;
        var animTitle = titleText.gameObject.AddComponent<UIAnimate>();
        animTitle.animType = UIAnimate.AnimationType.TextGlowPulse;
        
        CreateText("Subtitle", connectionBox.transform, "HỆ THỐNG ĐẤU TRÍ THỜI GIAN THỰC", 11f, TextAlignmentOptions.Center, new Vector2(360f, 30f), new Vector2(-200f, 160f), new Color(0.7f, 0.64f, 0.58f, 1f));

        CreateText("LobbyTypeLabel", connectionBox.transform, "CHẾ ĐỘ PHÒNG", 11f, TextAlignmentOptions.MidlineLeft, new Vector2(320f, 20f), new Vector2(-200f, 135f), goldColor);

        // Lobby Mode Selection Buttons
        publicTypeBtn = CreateButton(connectionBox.transform, "PublicTypeBtn", "Công khai", new Vector2(100f, 30f), new Vector2(-305f, 105f), boxBg);
        privateTypeBtn = CreateButton(connectionBox.transform, "PrivateTypeBtn", "Riêng tư", new Vector2(100f, 30f), new Vector2(-200f, 105f), boxBg);
        rankingTypeBtn = CreateButton(connectionBox.transform, "RankingTypeBtn", "Xếp hạng", new Vector2(100f, 30f), new Vector2(-95f, 105f), boxBg);

        CreateText("LobbyNameLabel", connectionBox.transform, "TÊN PHÒNG", 11f, TextAlignmentOptions.MidlineLeft, new Vector2(320f, 20f), new Vector2(-200f, 75f), goldColor);
        lobbyNameInputField = CreateInput(connectionBox.transform, "LobbyNameInput", "Tên phòng caro", new Vector2(320f, 40f), new Vector2(-200f, 45f));
        lobbyNameInputField.text = "Phòng Caro Vui Vẻ";

        CreateText("PasswordLabel", connectionBox.transform, "MẬT KHẨU PHÒNG", 11f, TextAlignmentOptions.MidlineLeft, new Vector2(320f, 20f), new Vector2(-200f, 10f), goldColor);
        lobbyPasswordInputField = CreateInput(connectionBox.transform, "LobbyPasswordInput", "Không cần mật khẩu", new Vector2(320f, 40f), new Vector2(-200f, -20f));

        CreateText("PortLabel", connectionBox.transform, "CỔNG KẾT NỐI", 11f, TextAlignmentOptions.MidlineLeft, new Vector2(150f, 20f), new Vector2(-285f, -50f), goldColor);
        portInputField = CreateInput(connectionBox.transform, "PortInput", "Cổng", new Vector2(150f, 40f), new Vector2(-285f, -80f));
        portInputField.text = "7777";

        startHostButton = CreateButton(connectionBox.transform, "CreateRoomBtn", "Tạo phòng mới", new Vector2(155f, 40f), new Vector2(-118f, -80f), new Color(0.85f, 0.55f, 0.15f, 1f));
        var animHost = startHostButton.gameObject.AddComponent<UIAnimate>();
        animHost.animType = UIAnimate.AnimationType.ButtonInteractive;

        quickJoinButton = CreateButton(connectionBox.transform, "QuickJoinBtn", "VÀO CHƠI NHANH CÔNG KHAI", new Vector2(320f, 40f), new Vector2(-200f, -135f), new Color(0.1f, 0.55f, 0.35f, 1f));
        var animQuick = quickJoinButton.gameObject.AddComponent<UIAnimate>();
        animQuick.animType = UIAnimate.AnimationType.ButtonInteractive;

        connectionStatusText = CreateText("ConnectionStatus", connectionBox.transform, string.Empty, 14f, TextAlignmentOptions.Center, new Vector2(320f, 40f), new Vector2(-200f, -190f), new Color(0.85f, 0.8f, 0.75f, 1f));

        // RIGHT-SIDE Lobby Browser (Form setup centered around x = 200)
        GameObject lobbyBrowserCard = CreateBox(
            "LobbyBrowserCard", 
            connectionBox.transform, 
            new Vector2(380f, 320f), 
            new Vector2(200f, -15f), 
            innerCardBg, 
            innerCardBorder
        );

        var browserTitle = CreateText("BrowserTitle", connectionBox.transform, "SẢNH ĐẤU CÔNG KHAI", 18f, TextAlignmentOptions.MidlineLeft, new Vector2(220f, 30f), new Vector2(120f, 175f), goldColor);
        browserTitle.fontStyle = FontStyles.Bold;

        refreshLobbiesButton = CreateButton(connectionBox.transform, "RefreshLobbiesBtn", "Làm mới", new Vector2(100f, 30f), new Vector2(330f, 175f), new Color(0.45f, 0.36f, 0.28f, 1f));
        var animRefresh = refreshLobbiesButton.gameObject.AddComponent<UIAnimate>();
        animRefresh.animType = UIAnimate.AnimationType.ButtonInteractive;

        lobbyListContainer = CreateUIObject("LobbyListContainer", lobbyBrowserCard.transform);
        Stretch(lobbyListContainer.GetComponent<RectTransform>(), 10f, 10f, 10f, 10f);

        // Invite code joining section at the bottom right
        CreateText("InviteLabel", connectionBox.transform, "HOẶC KẾT NỐI BẰNG MÃ MỜI:", 11f, TextAlignmentOptions.MidlineLeft, new Vector2(380f, 20f), new Vector2(200f, -200f), goldColor);
        
        inviteInputField = CreateInput(connectionBox.transform, "InviteInput", "IP:PORT, ví dụ 192.168.1.8:7777", new Vector2(240f, 40f), new Vector2(130f, -230f));
        
        startClientButton = CreateButton(connectionBox.transform, "JoinByCodeBtn", "Kết nối", new Vector2(120f, 40f), new Vector2(320f, -230f), new Color(0.1f, 0.45f, 0.6f, 1f));
        var animStartClient = startClientButton.gameObject.AddComponent<UIAnimate>();
        animStartClient.animType = UIAnimate.AnimationType.ButtonInteractive;

        // 2. Lobby Panel (Waiting Room)
        lobbyPanel = CreateFullscreenPanel("LobbyPanel", panelTint);
        
        GameObject lobbyBox = CreateBox(
            "LobbyBox", 
            lobbyPanel.transform, 
            new Vector2(820f, 520f), 
            Vector2.zero, 
            boxBg, 
            boxBorder
        );
        var animLobbyBox = lobbyBox.AddComponent<UIAnimate>();
        animLobbyBox.animType = UIAnimate.AnimationType.CardPopIn;

        var lobbyTitleText = CreateText("LobbyTitle", lobbyBox.transform, "PHÒNG CHỜ CARO", 36f, TextAlignmentOptions.Center, new Vector2(460f, 58f), new Vector2(0f, 215f), new Color(0.95f, 0.92f, 0.88f, 1f));
        lobbyTitleText.fontStyle = FontStyles.Bold;

        inviteCodeText = CreateText("InviteCode", lobbyBox.transform, string.Empty, 20f, TextAlignmentOptions.MidlineLeft, new Vector2(360f, 42f), new Vector2(-110f, 165f), new Color(0.9f, 0.85f, 0.8f, 1f));
        
        copyInviteButton = CreateButton(lobbyBox.transform, "CopyInviteButton", "Sao chép mã", new Vector2(140f, 38f), new Vector2(230f, 165f), new Color(0.45f, 0.36f, 0.28f, 1f));
        var animCopy = copyInviteButton.gameObject.AddComponent<UIAnimate>();
        animCopy.animType = UIAnimate.AnimationType.ButtonInteractive;
        
        GameObject playerListCard = CreateBox(
            "PlayerListCard", 
            lobbyBox.transform, 
            new Vector2(370f, 230f), 
            new Vector2(-190f, -20f), 
            innerCardBg, 
            innerCardBorder
        );
        playerStatusText = CreateText("PlayerStatus", playerListCard.transform, string.Empty, 16f, TextAlignmentOptions.TopLeft, new Vector2(340f, 200f), Vector2.zero, Color.white);
        
        GameObject matchStatusCard = CreateBox(
            "MatchStatusCard", 
            lobbyBox.transform, 
            new Vector2(370f, 230f), 
            new Vector2(190f, -20f), 
            innerCardBg, 
            innerCardBorder
        );
        lobbyStatusText = CreateText("LobbyStatus", matchStatusCard.transform, string.Empty, 18f, TextAlignmentOptions.Center, new Vector2(340f, 200f), Vector2.zero, Color.white);

        startGameButton = CreateButton(lobbyBox.transform, "StartGameButton", "Bắt đầu đấu", new Vector2(200f, 48f), new Vector2(-110f, -210f), new Color(0.1f, 0.55f, 0.35f, 1f));
        var animStart = startGameButton.gameObject.AddComponent<UIAnimate>();
        animStart.animType = UIAnimate.AnimationType.ButtonInteractive;

        leaveLobbyButton = CreateButton(lobbyBox.transform, "LeaveLobbyButton", "Thoát phòng", new Vector2(200f, 48f), new Vector2(110f, -210f), new Color(0.65f, 0.18f, 0.18f, 1f));
        var animLeave = leaveLobbyButton.gameObject.AddComponent<UIAnimate>();
        animLeave.animType = UIAnimate.AnimationType.ButtonInteractive;
        
        lobbyPanel.SetActive(false);

        // 3. Gameplay UI Overlay
        gameplayPanel = CreateUIObject("GameplayPanel", transform);
        Stretch(gameplayPanel.GetComponent<RectTransform>());
        gameplayExitButton = CreateButton(gameplayPanel.transform, "GameplayExitButton", "Thoát Game", new Vector2(140f, 42f), new Vector2(-80f, -34f), new Color(0.15f, 0.15f, 0.15f, 1f));
        var exitText = gameplayExitButton.GetComponentInChildren<TextMeshProUGUI>();
        if (exitText != null)
        {
            exitText.color = new Color(0.96f, 0.72f, 0.2f, 1f);
        }
        var animExit = gameplayExitButton.gameObject.AddComponent<UIAnimate>();
        animExit.animType = UIAnimate.AnimationType.ButtonInteractive;

        RectTransform exitRect = gameplayExitButton.GetComponent<RectTransform>();
        exitRect.anchorMin = new Vector2(1f, 1f);
        exitRect.anchorMax = new Vector2(1f, 1f);
        exitRect.pivot = new Vector2(1f, 1f);
        gameplayPanel.SetActive(false);
    }

    private void SelectLobbyType(LobbyType lobbyType)
    {
        selectedLobbyType = lobbyType;

        Color selectedColor = new Color(0.96f, 0.72f, 0.2f, 1f); 
        Color defaultColor = new Color(0.16f, 0.14f, 0.12f, 0.98f); 

        if (publicTypeBtn != null) publicTypeBtn.image.color = (lobbyType == LobbyType.Public) ? selectedColor : defaultColor;
        if (privateTypeBtn != null) privateTypeBtn.image.color = (lobbyType == LobbyType.Private) ? selectedColor : defaultColor;
        if (rankingTypeBtn != null) rankingTypeBtn.image.color = (lobbyType == LobbyType.Ranking) ? selectedColor : defaultColor;

        if (lobbyPasswordInputField != null)
        {
            if (lobbyType == LobbyType.Private)
            {
                lobbyPasswordInputField.interactable = true;
                var placeholder = lobbyPasswordInputField.placeholder as TextMeshProUGUI;
                if (placeholder != null) placeholder.text = "Nhập mật khẩu (tùy chọn)";
            }
            else
            {
                lobbyPasswordInputField.text = string.Empty;
                lobbyPasswordInputField.interactable = false;
                var placeholder = lobbyPasswordInputField.placeholder as TextMeshProUGUI;
                if (placeholder != null) placeholder.text = "Không cần mật khẩu";
            }
        }

        if (lobbyNameInputField != null)
        {
            if (lobbyType == LobbyType.Ranking)
            {
                lobbyNameInputField.text = string.Empty;
                lobbyNameInputField.interactable = false;
                var placeholder = lobbyNameInputField.placeholder as TextMeshProUGUI;
                if (placeholder != null) placeholder.text = "Tên phòng tự động";
            }
            else
            {
                lobbyNameInputField.interactable = true;
                var placeholder = lobbyNameInputField.placeholder as TextMeshProUGUI;
                if (placeholder != null) placeholder.text = "Tên phòng caro";
            }
        }
    }

    private void RefreshLobbyListUI()
    {
        if (lobbyListContainer == null) return;

        for (int i = lobbyListContainer.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(lobbyListContainer.transform.GetChild(i).gameObject);
        }

        var service = LobbyManager.Instance != null ? LobbyManager.Instance.GetLobbyService() : null;
        if (service == null) return;

        var lobbies = service.QueryPublicLobbies();
        Debug.Log($"[DIAGNOSTIC] Number of queried lobbies: {lobbies.Count}");
        foreach (var l in lobbies)
        {
            Debug.Log($"[DIAGNOSTIC] Queried lobby - Name: '{l.LobbyName}', Id: '{l.LobbyId}', IsPrivate: {l.IsPrivate}");
        }

        int index = 0;
        
        foreach (var lobby in lobbies)
        {
            if ((lobby.Type != LobbyType.Public && lobby.Type != LobbyType.Private) || lobby.CurrentPlayers >= lobby.MaxPlayers)
            {
                continue;
            }

            float yPos = 110f - index * 48f;
            if (yPos < -130f) break; 

            GameObject row = CreateUIObject("LobbyRow_" + index, lobbyListContainer.transform);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = new Vector2(0f, yPos);
            rowRect.sizeDelta = new Vector2(340f, 40f);

            Image rowBg = row.AddComponent<Image>();
            rowBg.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
            rowBg.type = Image.Type.Sliced;
            rowBg.color = new Color(0.12f, 0.1f, 0.08f, 0.6f);

            string typeLabel;
            if (lobby.Type == LobbyType.Public)
            {
                typeLabel = "<color=#10b981>🟢 Public</color>";
            }
            else if (lobby.Type == LobbyType.Private)
            {
                typeLabel = "<color=#ef4444>🔒 Private</color>";
            }
            else
            {
                typeLabel = "<color=#fbbf24>⭐ Ranking</color>";
            }
            string textContent = $"{lobby.LobbyName} ({lobby.CurrentPlayers}/{lobby.MaxPlayers}) - {typeLabel}";
            var text = CreateText("Text", row.transform, textContent, 14f, TextAlignmentOptions.MidlineLeft, new Vector2(260f, 35f), new Vector2(-20f, 0f), Color.white);
            text.fontStyle = FontStyles.Bold;

            var joinBtn = CreateButton(row.transform, "JoinButton", "Vào", new Vector2(60f, 30f), new Vector2(125f, 0f), new Color(0.1f, 0.55f, 0.35f, 1f));
            var capturedLobby = lobby;
            joinBtn.onClick.AddListener(() => JoinMockLobby(capturedLobby));

            index++;
        }

        if (index == 0)
        {
            CreateText("NoLobbiesText", lobbyListContainer.transform, "Chưa có phòng công khai nào", 14f, TextAlignmentOptions.Center, new Vector2(300f, 30f), Vector2.zero, new Color(0.6f, 0.55f, 0.5f, 0.8f));
        }
    }

    private GameObject CreateFullscreenPanel(string objectName, Color color)
    {
        GameObject panel = CreateUIObject(objectName, transform);
        Stretch(panel.GetComponent<RectTransform>());

        Image image = panel.AddComponent<Image>();
        Sprite bgSprite = GetGameBackgroundSprite();
        if (bgSprite != null)
        {
            image.sprite = bgSprite;
            image.type = Image.Type.Simple;
        }
        image.color = color;

        return panel;
    }

    private GameObject CreateBox(string objectName, Transform parent, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        return CreateBox(objectName, parent, size, anchoredPosition, color, new Color(1f, 1f, 1f, 0.15f));
    }

    private GameObject CreateBox(string objectName, Transform parent, Vector2 size, Vector2 anchoredPosition, Color bgColor, Color borderColor)
    {
        GameObject borderBox = CreateUIObject(objectName + "_Border", parent);
        RectTransform borderRect = borderBox.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0.5f, 0.5f);
        borderRect.anchorMax = new Vector2(0.5f, 0.5f);
        borderRect.pivot = new Vector2(0.5f, 0.5f);
        borderRect.anchoredPosition = anchoredPosition;
        borderRect.sizeDelta = size;

        Image borderImage = borderBox.AddComponent<Image>();
        borderImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        borderImage.type = Image.Type.Sliced;
        borderImage.color = borderColor;

        GameObject bgBox = CreateUIObject(objectName, borderBox.transform);
        RectTransform bgRect = bgBox.GetComponent<RectTransform>();
        Stretch(bgRect, 2f, 2f, 2f, 2f); 

        Image bgImage = bgBox.AddComponent<Image>();
        bgImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        bgImage.type = Image.Type.Sliced;
        bgImage.color = bgColor;

        return bgBox;
    }

    private TMP_InputField CreateInput(Transform parent, string objectName, string placeholder, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject inputBorder = CreateUIObject(objectName + "_Border", parent);
        RectTransform borderRect = inputBorder.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0.5f, 0.5f);
        borderRect.anchorMax = new Vector2(0.5f, 0.5f);
        borderRect.pivot = new Vector2(0.5f, 0.5f);
        borderRect.anchoredPosition = anchoredPosition;
        borderRect.sizeDelta = size;

        Image borderImage = inputBorder.AddComponent<Image>();
        borderImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        borderImage.type = Image.Type.Sliced;
        borderImage.color = new Color(0.4f, 0.33f, 0.25f, 0.8f); 

        GameObject inputObject = CreateUIObject(objectName, inputBorder.transform);
        RectTransform rectTransform = inputObject.GetComponent<RectTransform>();
        Stretch(rectTransform, 1.5f, 1.5f, 1.5f, 1.5f);

        Image image = inputObject.AddComponent<Image>();
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.09f, 0.08f, 0.07f, 0.95f); 

        TMP_InputField inputField = inputObject.AddComponent<TMP_InputField>();
        inputField.targetGraphic = image;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = 64;
        inputField.textViewport = rectTransform;

        TextMeshProUGUI text = CreateText("Text", inputObject.transform, string.Empty, 18f, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero, Color.white);
        Stretch(text.rectTransform, 14f, 4f, 14f, 4f);
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        TextMeshProUGUI placeholderText = CreateText("Placeholder", inputObject.transform, placeholder, 16f, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero, new Color(0.5f, 0.46f, 0.4f, 0.8f));
        Stretch(placeholderText.rectTransform, 14f, 4f, 14f, 4f);
        placeholderText.raycastTarget = false;
        placeholderText.textWrappingMode = TextWrappingModes.NoWrap;

        inputField.textComponent = text;
        inputField.placeholder = placeholderText;

        return inputField;
    }

    private Button CreateButton(Transform parent, string objectName, string label, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = color;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.disabledColor = new Color(0.25f, 0.28f, 0.32f, 0.75f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TextMeshProUGUI buttonText = CreateText("Label", buttonObject.transform, label, 18f, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero, Color.white);
        Stretch(buttonText.rectTransform);
        buttonText.raycastTarget = false;
        buttonText.fontStyle = FontStyles.Bold;

        return button;
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, string text, float fontSize, TextAlignmentOptions alignment, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        GameObject textObject = CreateUIObject(objectName, parent);
        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
        if (vietnameseFont != null)
        {
            textComponent.font = vietnameseFont;
        }
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = color;
        textComponent.alignment = alignment;
        textComponent.textWrappingMode = TextWrappingModes.Normal;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;

        return textComponent;
    }

    private GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.layer = gameObject.layer;
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private RectTransform GetOrAddRectTransform(GameObject target)
    {
        RectTransform rectTransform = target.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = target.AddComponent<RectTransform>();
        }

        return rectTransform;
    }

    private void Stretch(RectTransform rectTransform)
    {
        Stretch(rectTransform, 0f, 0f, 0f, 0f);
    }

    private void Stretch(RectTransform rectTransform, float left, float top, float right, float bottom)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
    }

    private System.Collections.IEnumerator StartAIGameWhenReady()
    {
        yield return new WaitUntil(() => GameManager.Instance != null && GameManager.Instance.IsSpawned);

        if (aiModel == null)
        {
            Debug.LogError("AI model is not assigned in NetworkManagerUI.");
            NetworkManager.Singleton.Shutdown();
            yield break;
        }

        GameManager.Instance.StartAIGame();
        CreateGameplayAgent();
    }

    private void CreateGameplayAgent()
    {
        GameObject agentObject = new GameObject("CaroGameplayAgent");
        agentObject.SetActive(false);

        CaroGameplayAgent agent = agentObject.AddComponent<CaroGameplayAgent>();
        BehaviorParameters behavior = agentObject.GetComponent<BehaviorParameters>();
        behavior.BehaviorName = "CaroAgent";
        behavior.BrainParameters.VectorObservationSize = GameManager.BoardPointsX * GameManager.BoardPointsY;
        behavior.BrainParameters.NumStackedVectorObservations = 1;
        behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(GameManager.BoardPointsX * GameManager.BoardPointsY);
        behavior.BehaviorType = BehaviorType.InferenceOnly;
        behavior.Model = aiModel;
        behavior.DeterministicInference = true;

        agent.Configure(GameManager.Instance);
        agentObject.SetActive(true);
        Debug.Log("StartGameManager: Đã khởi tạo AI CaroGameplayAgent thành công với mô hình ONNX.");
    }

    private void ParseConnectionCode(string code, out string address, out ushort port)
    {
        address = DefaultClientAddress;
        port = 7777;

        if (string.IsNullOrWhiteSpace(code)) return;

        int idx = code.LastIndexOf(':');
        if (idx > 0 && idx < code.Length - 1)
        {
            address = code.Substring(0, idx).Trim();
            string pStr = code.Substring(idx + 1).Trim();
            ushort.TryParse(pStr, out port);
        }
        else
        {
            address = code.Trim();
        }
    }
}
