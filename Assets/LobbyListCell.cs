using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Lobbies.Models;

public class LobbyListCell : MonoBehaviour
{
    [SerializeField] private Text lobbyNameText;
    [SerializeField] private Text playerCountText;
    [SerializeField] private Button joinButton;

    private Lobby _lobbyInfo;

    public void SetLobbyInfo(Lobby lobby, System.Action<Lobby> onJoinClick)
    {
        _lobbyInfo = lobby;
        lobbyNameText.text = lobby.Name;
        playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";

        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(() => onJoinClick?.Invoke(lobby));

        // 방이 꽉 찼으면 Join 버튼 비활성화
        joinButton.interactable = lobby.Players.Count < lobby.MaxPlayers;
    }
}