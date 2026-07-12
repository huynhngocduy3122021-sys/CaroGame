using System;
using UnityEngine;

public class RankingLobbyStrategy : ILobbyStrategy
{
    public event Action OnQueueJoined;
    public event Action OnQueueLeft;
    public event Action OnMatchFound;

    private readonly ILobbyService lobbyService;
    private bool isInQueue = false;

    public RankingLobbyStrategy(ILobbyService lobbyService)
    {
        this.lobbyService = lobbyService;
    }

    public void QueuePlayer()
    {
        if (isInQueue) return;
        isInQueue = true;
        OnQueueJoined?.Invoke();
        Debug.Log("[RankingStrategy] Player added to queue.");
        
        FindOpponent();
    }

    public void LeaveQueue()
    {
        if (!isInQueue) return;
        isInQueue = false;
        OnQueueLeft?.Invoke();
        Debug.Log("[RankingStrategy] Player left queue.");
    }

    public void FindOpponent()
    {
        if (!isInQueue) return;
        Debug.Log("[RankingStrategy] Finding opponent...");
        
        // Simulating matching with another player after 3 seconds
        LobbyManager.Instance.StartCoroutine(SimulateMatchmaking());
    }

    private System.Collections.IEnumerator SimulateMatchmaking()
    {
        yield return new WaitForSeconds(3f);
        if (isInQueue)
        {
            Debug.Log("[RankingStrategy] Opponent found!");
            OnMatchFound?.Invoke();
            CreateRankedLobby();
        }
    }

    public void CreateRankedLobby()
    {
        isInQueue = false;

        // Auto matchmaking room parameters: hidden ranking lobby on local port 7777
        var lobbyData = new LobbyData
        {
            LobbyId = Guid.NewGuid().ToString(),
            LobbyName = "Ranking Match #" + UnityEngine.Random.Range(100, 999),
            Type = LobbyType.Ranking,
            MaxPlayers = 2,
            IsPrivate = true,
            ConnectionCode = "127.0.0.1:7777" 
        };

        CreateLobby(lobbyData, (success, message) =>
        {
            if (success)
            {
                Debug.Log("[RankingStrategy] Automated ranking lobby host started.");
            }
            else
            {
                Debug.LogError("[RankingStrategy] Automated lobby creation failed: " + message);
            }
        });
    }

    public void CreateLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        lobbyData.IsPrivate = true;
        lobbyService.CreateLobby(lobbyData, onComplete);
    }

    public void JoinLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        lobbyService.JoinLobby(lobbyData, onComplete);
    }

    public void JoinByCode(string code, Action<bool, string> onComplete)
    {
        lobbyService.JoinByCode(code, onComplete);
    }

    public void QuickJoin(Action<bool, string> onComplete)
    {
        // Automatically check if ranking lobbies are active and join one, or create one
        var lobbies = lobbyService.QueryPublicLobbies();
        var rankingLobby = lobbies.Find(l => l.Type == LobbyType.Ranking && l.CurrentPlayers < l.MaxPlayers);

        if (rankingLobby != null)
        {
            JoinLobby(rankingLobby, onComplete);
        }
        else
        {
            CreateRankedLobby();
            onComplete?.Invoke(true, "Creating ranked room...");
        }
    }

    public void LeaveLobby(Action<bool, string> onComplete)
    {
        LeaveQueue();
        lobbyService.LeaveLobby(onComplete);
    }

    public void UpdateLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        lobbyService.UpdateLobby(lobbyData, onComplete);
    }

    public void DeleteLobby(Action<bool, string> onComplete)
    {
        lobbyService.DeleteLobby(onComplete);
    }
}
