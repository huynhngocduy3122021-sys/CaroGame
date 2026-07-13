using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class SerializableLobbyData
{
    public string LobbyId;
    public string LobbyName;
    public LobbyType Type;
    public int MaxPlayers;
    public int CurrentPlayers;
    public bool IsPrivate;
    public string Password;
    public string ConnectionCode;
}

[Serializable]
public class LobbyListContainer
{
    public List<SerializableLobbyData> Lobbies = new List<SerializableLobbyData>();
}

public class MockLobbyService : ILobbyService
{
    public event Action<LobbyData> OnLobbyCreated;
    public event Action<LobbyData> OnLobbyJoined;
    public event Action OnLobbyLeft;
    public event Action<LobbyData> OnLobbyUpdated;
    public event Action<string> OnPlayerJoined;
    public event Action<string> OnPlayerLeft;
    public event Action OnLobbyDeleted;

    private static string FilePath => Path.Combine(Path.GetTempPath(), "mock_lobbies.json");
    private string activeLobbyId;

    public void CreateLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        activeLobbyId = string.IsNullOrEmpty(lobbyData.LobbyId) ? Guid.NewGuid().ToString() : lobbyData.LobbyId;
        lobbyData.LobbyId = activeLobbyId;
        
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

        RegisterLobbyStatic(serializableLobby);

        OnLobbyCreated?.Invoke(lobbyData);
        onComplete?.Invoke(true, "[Mock] Room created.");
    }

    public void JoinLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        var lobbies = GetLobbiesStatic();
        var match = lobbies.Find(l => (!string.IsNullOrEmpty(lobbyData.LobbyId) && l.LobbyId == lobbyData.LobbyId) || 
                                       l.ConnectionCode == lobbyData.ConnectionCode);
        if (match == null)
        {
            onComplete?.Invoke(false, "Không tìm thấy phòng.");
            return;
        }

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

        if (!string.IsNullOrEmpty(activeLobbyId))
        {
            UpdateLobbyPlayerCountStatic(activeLobbyId, 2);
        }

        OnLobbyJoined?.Invoke(lobbyData);
        OnPlayerJoined?.Invoke("Player2_Mock");
        onComplete?.Invoke(true, "[Mock] Room joined.");
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
            onComplete?.Invoke(false, "[Mock] No public lobbies available.");
            return;
        }

        JoinLobby(openLobby, onComplete);
    }

    public void LeaveLobby(Action<bool, string> onComplete)
    {
        if (!string.IsNullOrEmpty(activeLobbyId))
        {
            var lobbies = GetLobbiesStatic();
            var match = lobbies.Find(l => l.LobbyId == activeLobbyId);
            if (match != null)
            {
                if (match.CurrentPlayers > 1)
                {
                    UpdateLobbyPlayerCountStatic(activeLobbyId, 1);
                    OnPlayerLeft?.Invoke("Player2_Mock");
                }
                else
                {
                    UnregisterLobbyStatic(activeLobbyId);
                }
            }
        }

        activeLobbyId = null;
        OnLobbyLeft?.Invoke();
        onComplete?.Invoke(true, "[Mock] Left room.");
    }

    public void UpdateLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
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
            RegisterLobbyStatic(serializableLobby);
            OnLobbyUpdated?.Invoke(lobbyData);
            onComplete?.Invoke(true, "[Mock] Room updated.");
        }
        else
        {
            onComplete?.Invoke(false, "[Mock] No active lobby to update.");
        }
    }

    public void DeleteLobby(Action<bool, string> onComplete)
    {
        if (!string.IsNullOrEmpty(activeLobbyId))
        {
            UnregisterLobbyStatic(activeLobbyId);
            activeLobbyId = null;
            OnLobbyDeleted?.Invoke();
            onComplete?.Invoke(true, "[Mock] Room deleted.");
        }
        else
        {
            onComplete?.Invoke(false, "[Mock] No active lobby to delete.");
        }
    }

    public List<LobbyData> QueryPublicLobbies()
    {
        var rawLobbies = GetLobbiesStatic();
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

    // Static helper methods for reading/writing the common registry file
    public static void ClearAllLobbies()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Error clearing mock lobbies: " + ex.Message);
        }
    }

    public static List<SerializableLobbyData> GetLobbies()
    {
        return GetLobbiesStatic();
    }

    public static void RegisterLobby(SerializableLobbyData lobby)
    {
        RegisterLobbyStatic(lobby);
    }

    public static void UnregisterLobby(string lobbyId)
    {
        UnregisterLobbyStatic(lobbyId);
    }

    public static void UpdateLobbyPlayerCount(string lobbyId, int count)
    {
        UpdateLobbyPlayerCountStatic(lobbyId, count);
    }

    private static List<SerializableLobbyData> GetLobbiesStatic()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new List<SerializableLobbyData>();
            }
            string json = File.ReadAllText(FilePath);
            var container = JsonUtility.FromJson<LobbyListContainer>(json);
            return container?.Lobbies ?? new List<SerializableLobbyData>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Error reading mock lobbies: " + ex.Message);
            return new List<SerializableLobbyData>();
        }
    }

    private static void RegisterLobbyStatic(SerializableLobbyData lobby)
    {
        try
        {
            var lobbies = GetLobbiesStatic();
            lobbies.RemoveAll(l => l.LobbyId == lobby.LobbyId);
            lobbies.Add(lobby);
            SaveLobbiesStatic(lobbies);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error registering mock lobby: " + ex.Message);
        }
    }

    private static void UnregisterLobbyStatic(string lobbyId)
    {
        try
        {
            var lobbies = GetLobbiesStatic();
            lobbies.RemoveAll(l => l.LobbyId == lobbyId);
            SaveLobbiesStatic(lobbies);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error unregistering mock lobby: " + ex.Message);
        }
    }

    private static void UpdateLobbyPlayerCountStatic(string lobbyId, int count)
    {
        try
        {
            var lobbies = GetLobbiesStatic();
            var lobby = lobbies.Find(l => l.LobbyId == lobbyId);
            if (lobby != null)
            {
                lobby.CurrentPlayers = count;
                SaveLobbiesStatic(lobbies);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error updating lobby player count: " + ex.Message);
        }
    }

    private static void SaveLobbiesStatic(List<SerializableLobbyData> lobbies)
    {
        try
        {
            string dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var container = new LobbyListContainer { Lobbies = lobbies };
            string json = JsonUtility.ToJson(container, true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error saving mock lobbies: " + ex.Message);
        }
    }
}
