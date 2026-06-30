using System;
using System.Net;
using System.Net.Sockets;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManagerUI : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button startClientButton;
    [SerializeField] private TMP_InputField inviteInputField;
    [SerializeField] private TMP_InputField portInputField;
    [SerializeField] private TextMeshProUGUI connectionStatusText;

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

    private void Awake()
    {
        currentPort = NormalizePort(defaultPort);
        unityTransport = FindUnityTransport();

        if (NeedsRuntimeUI())
        {
            BuildRuntimeUI();
        }

        WireUIEvents();
        SubscribeNetworkCallbacks();
        ShowConnectionPanel("Tạo phòng mới hoặc nhập mã mời để tham gia.");
    }

    private void Start()
    {
        TrySubscribeGameManager();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            ShowLobbyPanel("Đã kết nối lobby.");
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

        unityTransport.SetConnectionData(DefaultClientAddress, currentPort, HostListenAddress);
        currentInviteAddress = GetLocalIPv4Address();

        bool started = NetworkManager.Singleton.StartHost();
        if (!started)
        {
            ShowConnectionPanel("Không thể tạo phòng. Kiểm tra NetworkManager/Transport.");
            return;
        }

        Debug.Log("Started Host on " + GetInviteCode());
        ShowLobbyPanel("Phòng đã tạo. Gửi mã mời cho người chơi khác.");
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

        currentInviteAddress = address;
        currentPort = port;
        unityTransport.SetConnectionData(address, port);

        bool started = NetworkManager.Singleton.StartClient();
        if (!started)
        {
            ShowConnectionPanel("Không thể tham gia lobby. Kiểm tra mã mời/IP.");
            return;
        }

        Debug.Log("Started Client to " + address + ":" + port);
        ShowLobbyPanel("Đang kết nối đến lobby...");
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

    private void LeaveLobby()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        ShowConnectionPanel("Đã rời lobby.");
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
            inviteCodeText.text = "Mã mời: " + GetInviteCode();
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
        string info = "DANH SÁCH PHÒNG CHỜ\n";

        for (int i = 0; i < connectedCount; i++)
        {
            if (i == 0) info += "- Người chơi 1 (X) [Host]\n";
            else if (i == 1) info += "- Người chơi 2 (O)\n";
            else info += "- Khán giả " + (i - 1) + " (đang xem)\n";
        }

        if (connectedCount == 0)
        {
            info += "- Đang đợi kết nối...\n";
        }

        if (GameManager.Instance != null)
        {
            info += "\nVai trò của bạn: " + FormatRole(GameManager.Instance.GetLocalPlayerType());
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
            SetLobbyStatus("Đang đợi host bắt đầu game.");
        }
        else if (connectedCount < 2)
        {
            SetLobbyStatus("Gửi mã mời và đợi thêm 1 người chơi.");
        }
        else
        {
            SetLobbyStatus("Đủ người. Host có thể bắt đầu game.");
        }

        if (inviteCodeText != null)
        {
            inviteCodeText.text = "Mã mời: " + GetInviteCode();
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

    private void BuildRuntimeUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }

        RectTransform rootRect = GetOrAddRectTransform(gameObject);
        Stretch(rootRect);

        connectionPanel = CreateFullscreenPanel("ConnectionPanel", new Color(0.04f, 0.06f, 0.08f, 0.94f));
        GameObject connectionBox = CreateBox("ConnectionBox", connectionPanel.transform, new Vector2(560f, 430f), Vector2.zero, new Color(0.12f, 0.15f, 0.18f, 0.96f));

        CreateText("Title", connectionBox.transform, "SẢNH CARO", 38f, TextAlignmentOptions.Center, new Vector2(460f, 60f), new Vector2(0f, 150f), Color.white);
        inviteInputField = CreateInput(connectionBox.transform, "InviteInput", "IP:PORT, ví dụ 192.168.1.8:7777", new Vector2(410f, 48f), new Vector2(0f, 70f));
        inviteInputField.text = DefaultClientAddress + ":" + currentPort;
        portInputField = CreateInput(connectionBox.transform, "PortInput", "Port", new Vector2(180f, 44f), new Vector2(-115f, 8f));
        startHostButton = CreateButton(connectionBox.transform, "HostButton", "Tạo phòng", new Vector2(190f, 50f), new Vector2(-105f, -65f), new Color(0.1f, 0.55f, 0.95f, 1f));
        startClientButton = CreateButton(connectionBox.transform, "JoinButton", "Tham gia", new Vector2(190f, 50f), new Vector2(105f, -65f), new Color(0.15f, 0.7f, 0.45f, 1f));
        connectionStatusText = CreateText("ConnectionStatus", connectionBox.transform, string.Empty, 20f, TextAlignmentOptions.Center, new Vector2(470f, 70f), new Vector2(0f, -155f), new Color(0.86f, 0.9f, 0.95f, 1f));

        lobbyPanel = CreateFullscreenPanel("LobbyPanel", new Color(0.04f, 0.06f, 0.08f, 0.94f));
        GameObject lobbyBox = CreateBox("LobbyBox", lobbyPanel.transform, new Vector2(590f, 500f), Vector2.zero, new Color(0.12f, 0.15f, 0.18f, 0.96f));

        CreateText("LobbyTitle", lobbyBox.transform, "PHÒNG CHỜ", 36f, TextAlignmentOptions.Center, new Vector2(460f, 58f), new Vector2(0f, 190f), Color.white);
        inviteCodeText = CreateText("InviteCode", lobbyBox.transform, string.Empty, 22f, TextAlignmentOptions.Center, new Vector2(500f, 42f), new Vector2(0f, 132f), new Color(0.9f, 0.94f, 1f, 1f));
        copyInviteButton = CreateButton(lobbyBox.transform, "CopyInviteButton", "Sao chép", new Vector2(180f, 44f), new Vector2(0f, 82f), new Color(0.1f, 0.55f, 0.95f, 1f));
        playerStatusText = CreateText("PlayerStatus", lobbyBox.transform, string.Empty, 22f, TextAlignmentOptions.Left, new Vector2(480f, 165f), new Vector2(0f, -25f), new Color(0.92f, 0.96f, 1f, 1f));
        lobbyStatusText = CreateText("LobbyStatus", lobbyBox.transform, string.Empty, 20f, TextAlignmentOptions.Center, new Vector2(480f, 54f), new Vector2(0f, -145f), new Color(0.86f, 0.9f, 0.95f, 1f));
        startGameButton = CreateButton(lobbyBox.transform, "StartGameButton", "Bắt đầu", new Vector2(180f, 50f), new Vector2(-105f, -205f), new Color(0.15f, 0.7f, 0.45f, 1f));
        leaveLobbyButton = CreateButton(lobbyBox.transform, "LeaveLobbyButton", "Rời phòng", new Vector2(180f, 50f), new Vector2(105f, -205f), new Color(0.55f, 0.22f, 0.22f, 1f));
        lobbyPanel.SetActive(false);

        gameplayPanel = CreateUIObject("GameplayPanel", transform);
        Stretch(gameplayPanel.GetComponent<RectTransform>());
        gameplayExitButton = CreateButton(gameplayPanel.transform, "GameplayExitButton", "Thoát", new Vector2(120f, 42f), new Vector2(-80f, -34f), new Color(0.55f, 0.22f, 0.22f, 1f));
        RectTransform exitRect = gameplayExitButton.GetComponent<RectTransform>();
        exitRect.anchorMin = new Vector2(1f, 1f);
        exitRect.anchorMax = new Vector2(1f, 1f);
        exitRect.pivot = new Vector2(1f, 1f);
        gameplayPanel.SetActive(false);
    }

    private GameObject CreateFullscreenPanel(string objectName, Color color)
    {
        GameObject panel = CreateUIObject(objectName, transform);
        Stretch(panel.GetComponent<RectTransform>());

        Image image = panel.AddComponent<Image>();
        image.color = color;

        return panel;
    }

    private GameObject CreateBox(string objectName, Transform parent, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        GameObject box = CreateUIObject(objectName, parent);
        RectTransform rectTransform = box.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Image image = box.AddComponent<Image>();
        image.color = color;

        return box;
    }

    private TMP_InputField CreateInput(Transform parent, string objectName, string placeholder, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject inputObject = CreateUIObject(objectName, parent);
        RectTransform rectTransform = inputObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Image image = inputObject.AddComponent<Image>();
        image.color = new Color(0.96f, 0.98f, 1f, 1f);

        TMP_InputField inputField = inputObject.AddComponent<TMP_InputField>();
        inputField.targetGraphic = image;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = 64;
        inputField.textViewport = rectTransform;

        TextMeshProUGUI text = CreateText("Text", inputObject.transform, string.Empty, 20f, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero, new Color(0.07f, 0.09f, 0.12f, 1f));
        Stretch(text.rectTransform, 14f, 8f, 14f, 8f);
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        TextMeshProUGUI placeholderText = CreateText("Placeholder", inputObject.transform, placeholder, 18f, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero, new Color(0.42f, 0.47f, 0.52f, 1f));
        Stretch(placeholderText.rectTransform, 14f, 8f, 14f, 8f);
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
        image.color = color;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        colors.disabledColor = new Color(0.25f, 0.28f, 0.32f, 0.75f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TextMeshProUGUI buttonText = CreateText("Label", buttonObject.transform, label, 20f, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero, Color.white);
        Stretch(buttonText.rectTransform);
        buttonText.raycastTarget = false;

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
}
