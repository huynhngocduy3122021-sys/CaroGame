using System;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public LobbyData ActiveLobby { get; private set; }
    
    // Event declarations
    public event Action<LobbyData> OnLobbyCreated;
    public event Action<LobbyData> OnLobbyJoined;
    public event Action OnLobbyLeft;
    public event Action<LobbyData> OnLobbyUpdated;
    public event Action<string> OnPlayerJoined;
    public event Action<string> OnPlayerLeft;
    public event Action OnLobbyDeleted;

    private ILobbyService lobbyService;
    private LobbyStrategyFactory strategyFactory;
    private ILobbyStrategy activeStrategy;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Bootstrap();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Bootstrap()
    {
        // Self-healing bootstrap: if no service registered in ServiceLocator, register the default UnityLobbyService
        try
        {
            lobbyService = ServiceLocator.Resolve<ILobbyService>();
        }
        catch
        {
            lobbyService = new UnityLobbyService();
            ServiceLocator.Register<ILobbyService>(lobbyService);
        }

        strategyFactory = new LobbyStrategyFactory(lobbyService);
    }

    private void Start()
    {
        // Wire backend service events to domain manager events
        lobbyService.OnLobbyCreated += (data) => {
            ActiveLobby = data;
            OnLobbyCreated?.Invoke(data);
        };
        lobbyService.OnLobbyJoined += (data) => {
            ActiveLobby = data;
            OnLobbyJoined?.Invoke(data);
        };
        lobbyService.OnLobbyLeft += () => {
            ActiveLobby = null;
            OnLobbyLeft?.Invoke();
        };
        lobbyService.OnLobbyUpdated += (data) => {
            ActiveLobby = data;
            OnLobbyUpdated?.Invoke(data);
        };
        lobbyService.OnPlayerJoined += (playerId) => OnPlayerJoined?.Invoke(playerId);
        lobbyService.OnPlayerLeft += (playerId) => OnPlayerLeft?.Invoke(playerId);
        lobbyService.OnLobbyDeleted += () => {
            ActiveLobby = null;
            OnLobbyDeleted?.Invoke();
        };
    }

    public ILobbyService GetLobbyService()
    {
        return lobbyService;
    }

    public void CreateLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        activeStrategy = strategyFactory.GetStrategy(lobbyData.Type);
        activeStrategy.CreateLobby(lobbyData, onComplete);
    }

    public void JoinLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        activeStrategy = strategyFactory.GetStrategy(lobbyData.Type);
        activeStrategy.JoinLobby(lobbyData, onComplete);
    }

    public void JoinByCode(string inviteCode, LobbyType lobbyType, Action<bool, string> onComplete)
    {
        activeStrategy = strategyFactory.GetStrategy(lobbyType);
        activeStrategy.JoinByCode(inviteCode, onComplete);
    }

    public void QuickJoin(LobbyType lobbyType, Action<bool, string> onComplete)
    {
        activeStrategy = strategyFactory.GetStrategy(lobbyType);
        activeStrategy.QuickJoin(onComplete);
    }

    public void LeaveLobby(Action<bool, string> onComplete)
    {
        if (activeStrategy == null)
        {
            // Fallback shutdown directly
            lobbyService.LeaveLobby(onComplete);
            return;
        }
        activeStrategy.LeaveLobby(onComplete);
    }

    public void UpdateLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        if (activeStrategy == null)
        {
            onComplete?.Invoke(false, "No active strategy to update lobby.");
            return;
        }
        activeStrategy.UpdateLobby(lobbyData, onComplete);
    }

    public void DeleteLobby(Action<bool, string> onComplete)
    {
        if (activeStrategy == null)
        {
            onComplete?.Invoke(false, "No active strategy to delete lobby.");
            return;
        }
        activeStrategy.DeleteLobby(onComplete);
    }

    // Ranking Queue Facades
    public void QueuePlayer()
    {
        var ranking = strategyFactory.GetStrategy(LobbyType.Ranking) as RankingLobbyStrategy;
        ranking?.QueuePlayer();
    }

    public void LeaveQueue()
    {
        var ranking = strategyFactory.GetStrategy(LobbyType.Ranking) as RankingLobbyStrategy;
        ranking?.LeaveQueue();
    }
}
