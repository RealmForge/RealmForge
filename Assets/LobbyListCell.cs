using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Lobbies.Models;

public class LobbyListCell : MonoBehaviour
{
    [SerializeField] private Text lobbyNameText;
    [SerializeField] private Text playerCountText;
    [SerializeField] private Button joinButton;
    [SerializeField] private Text joinText;

    private Lobby _lobbyInfo;

    public void SetLobbyInfo(Lobby lobby, System.Action<Lobby> onJoinClick)
    {
        _lobbyInfo = lobby;
        lobbyNameText.text = lobby.Name;
        playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";

        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(() => onJoinClick?.Invoke(lobby));

        // 비밀번호가 걸려있는 방은 Join 버튼 비활성화 및 텍스트 변경
        if (lobby.HasPassword)
        {
            joinButton.interactable = false;
            joinText.text = "Lock";
        }
        // 방이 꽉 찼으면 Join 버튼 비활성화
        else if (lobby.Players.Count >= lobby.MaxPlayers)
        {
            joinButton.interactable = false;
            joinText.text = "Full";
        }
        else
        {
            joinButton.interactable = true;
            joinText.text = "Join";
        }
    }
}