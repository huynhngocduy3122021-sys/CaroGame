[System.Serializable]
public class LobbyData
{
    public string LobbyId;
    public string LobbyName;
    public LobbyType Type;
    public int MaxPlayers;
    public bool IsPrivate;
    public string Password;
    public string ConnectionCode; // Address:Port or Invite Code
    public int CurrentPlayers;
}
