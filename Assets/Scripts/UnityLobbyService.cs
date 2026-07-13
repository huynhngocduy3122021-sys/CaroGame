using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class UnityLobbyService : ILobbyService
{
    public event Action<LobbyData> OnLobbyCreated;
    public event Action<LobbyData> OnLobbyJoined;
    public event Action OnLobbyLeft;
    public event Action<LobbyData> OnLobbyUpdated;
    public event Action<string> OnPlayerJoined;
    public event Action<string> OnPlayerLeft;
    public event Action OnLobbyDeleted;

    private const string DefaultClientAddress = "127.0.0.1";
    private const string HostListenAddress = "0.0.0.0";
    
    private string activeLobbyId;
    private LobbyData currentLobbyData;

    public void CreateLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        if (NetworkManager.Singleton == null)
        {
            onComplete?.Invoke(false, "NetworkManager.Singleton is null.");
            return;
        }

        var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (unityTransport == null)
        {
            onComplete?.Invoke(false, "UnityTransport not found.");
            return;
        }

        ParseConnectionCode(lobbyData.ConnectionCode, out string address, out ushort port);
        unityTransport.SetConnectionData(DefaultClientAddress, port, HostListenAddress);

        bool started = NetworkManager.Singleton.StartHost();
        if (!started)
        {
            onComplete?.Invoke(false, "Failed to start Host.");
            return;
        }

        activeLobbyId = string.IsNullOrEmpty(lobbyData.LobbyId) ? Guid.NewGuid().ToString() : lobbyData.LobbyId;
        lobbyData.LobbyId = activeLobbyId;
        lobbyData.ConnectionCode = GetLocalIPv4Address() + ":" + port;
        currentLobbyData = lobbyData;

        // Register in mock JSON database so other local processes can discover it
        var serializableLobby = new SerializableLobbyData
        {
            LobbyId = lobbyData.LobbyId,
            LobbyName = lobbyData.LobbyName,
            Type = lobbyData.Type,
            MaxPlayers = lobbyData.MaxPlayers,
            CurrentPlayers = 1,
            IsPrivate = lobbyData.IsPrivate,
            Password = lobbyData.Password,
            ConnectionCode = lobbyData.ConnectionCode
        };
        MockLobbyService.RegisterLobby(serializableLobby);

        SubscribeNetworkCallbacks();

        OnLobbyCreated?.Invoke(lobbyData);
        onComplete?.Invoke(true, "Unity host started.");
    }

    public void JoinLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        if (NetworkManager.Singleton == null)
        {
            onComplete?.Invoke(false, "NetworkManager.Singleton is null.");
            return;
        }

        var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (unityTransport == null)
        {
            onComplete?.Invoke(false, "UnityTransport not found.");
            return;
        }

        // Validate password if lobby exists in registry
        var lobbies = MockLobbyService.GetLobbies();
        var match = lobbies.Find(l => (!string.IsNullOrEmpty(lobbyData.LobbyId) && l.LobbyId == lobbyData.LobbyId) || 
                                       l.ConnectionCode == lobbyData.ConnectionCode);
        if (match == null)
        {
            // Nếu không tìm thấy trong mock registry (do khác máy tính), vẫn cho phép kết nối thẳng qua IP
            if (string.IsNullOrEmpty(lobbyData.ConnectionCode))
            {
                onComplete?.Invoke(false, "Không tìm thấy phòng.");
                return;
            }
            activeLobbyId = string.IsNullOrEmpty(lobbyData.LobbyId) ? Guid.NewGuid().ToString() : lobbyData.LobbyId;
            currentLobbyData = lobbyData;
        }
        else
        {
            if (match.Type == LobbyType.Private)
            {
                if (match.Password != lobbyData.Password)
                {
                    onComplete?.Invoke(false, "Mật khẩu phòng không đúng!");
                    return;
                }
            }

            lobbyData.ConnectionCode = match.ConnectionCode;
            activeLobbyId = match.LobbyId;
            lobbyData.LobbyId = match.LobbyId;
        }

        ParseConnectionCode(lobbyData.ConnectionCode, out string address, out ushort port);
        unityTransport.SetConnectionData(address, port);

        bool started = NetworkManager.Singleton.StartClient();
        if (!started)
        {
            onComplete?.Invoke(false, "Failed to start Client.");
            return;
        }

        activeLobbyId = lobbyData.LobbyId;
        currentLobbyData = lobbyData;

        if (!string.IsNullOrEmpty(activeLobbyId))
        {
            MockLobbyService.UpdateLobbyPlayerCount(activeLobbyId, 2);
        }

        SubscribeNetworkCallbacks();

        OnLobbyJoined?.Invoke(lobbyData);
        onComplete?.Invoke(true, "Unity client connected.");
    }

    public void JoinByCode(string code, Action<bool, string> onComplete)
    {
        LobbyData data = new LobbyData
        {
            ConnectionCode = code,
            Type = LobbyType.Private
        };
        JoinLobby(data, onComplete);
    }

    public void QuickJoin(Action<bool, string> onComplete)
    {
        var lobbies = QueryPublicLobbies();
        var openLobby = lobbies.Find(l => !l.IsPrivate && l.CurrentPlayers < l.MaxPlayers);

        if (openLobby == null)
        {
            onComplete?.Invoke(false, "Không tìm thấy phòng công khai nào còn trống.");
            return;
        }

        JoinLobby(openLobby, onComplete);
    }

    public void LeaveLobby(Action<bool, string> onComplete)
    {
        UnsubscribeNetworkCallbacks();

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsServer && !string.IsNullOrEmpty(activeLobbyId))
            {
                MockLobbyService.UnregisterLobby(activeLobbyId);
            }
            else if (NetworkManager.Singleton.IsClient && !string.IsNullOrEmpty(activeLobbyId))
            {
                MockLobbyService.UpdateLobbyPlayerCount(activeLobbyId, 1);
            }
            NetworkManager.Singleton.Shutdown();
        }

        activeLobbyId = null;
        currentLobbyData = null;

        OnLobbyLeft?.Invoke();
        onComplete?.Invoke(true, "Lobby connection terminated.");
    }

    public void UpdateLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        currentLobbyData = lobbyData;
        if (!string.IsNullOrEmpty(activeLobbyId))
        {
            var serializableLobby = new SerializableLobbyData
            {
                LobbyId = activeLobbyId,
                LobbyName = lobbyData.LobbyName,
                Type = lobbyData.Type,
                MaxPlayers = lobbyData.MaxPlayers,
                CurrentPlayers = lobbyData.CurrentPlayers,
                IsPrivate = lobbyData.IsPrivate,
                Password = lobbyData.Password,
                ConnectionCode = lobbyData.ConnectionCode
            };
            MockLobbyService.RegisterLobby(serializableLobby);
            OnLobbyUpdated?.Invoke(lobbyData);
            onComplete?.Invoke(true, "Lobby updated in registry.");
        }
        else
        {
            onComplete?.Invoke(false, "No active lobby to update.");
        }
    }

    public void DeleteLobby(Action<bool, string> onComplete)
    {
        if (!string.IsNullOrEmpty(activeLobbyId))
        {
            MockLobbyService.UnregisterLobby(activeLobbyId);
            activeLobbyId = null;
            currentLobbyData = null;
            OnLobbyDeleted?.Invoke();
            onComplete?.Invoke(true, "Lobby deleted.");
        }
        else
        {
            onComplete?.Invoke(false, "No active lobby to delete.");
        }
    }

    public List<LobbyData> QueryPublicLobbies()
    {
        var rawLobbies = MockLobbyService.GetLobbies();
        var result = new List<LobbyData>();
        foreach (var l in rawLobbies)
        {
            bool isPrivate = l.Type == LobbyType.Private;
            result.Add(new LobbyData
            {
                LobbyId = l.LobbyId,
                LobbyName = l.LobbyName,
                Type = l.Type,
                MaxPlayers = l.MaxPlayers,
                IsPrivate = l.IsPrivate,
                Password = string.Empty, // Protect password
                ConnectionCode = isPrivate ? string.Empty : l.ConnectionCode, // Hide connection code for private
                CurrentPlayers = l.CurrentPlayers
            });
        }
        return result;
    }

    private void SubscribeNetworkCallbacks()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback += Netcode_OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += Netcode_OnClientDisconnected;
    }

    private void UnsubscribeNetworkCallbacks()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= Netcode_OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= Netcode_OnClientDisconnected;
    }

    private void Netcode_OnClientConnected(ulong clientId)
    {
        OnPlayerJoined?.Invoke(clientId.ToString());
    }

    private void Netcode_OnClientDisconnected(ulong clientId)
    {
        OnPlayerLeft?.Invoke(clientId.ToString());
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
        catch (Exception)
        {
            // Ignore
        }
        return DefaultClientAddress;
    }
}
