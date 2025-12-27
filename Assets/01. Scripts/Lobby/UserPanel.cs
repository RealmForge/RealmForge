using UnityEngine;
using UnityEngine.UI;

public class UserPanel : MonoBehaviour
{
    [SerializeField] private Text hostText;
    [SerializeField] private Text nameText;
    [SerializeField] private Text statusText;

    public void SetPlayerInfo(string playerName, bool isHost, bool isReady)
    {
        // 닉네임 표시
        if (nameText != null)
            nameText.text = playerName;

        // Host 표시 (Host인 경우만)
        if (hostText != null)
            hostText.text = isHost ? "Host" : "";

        // 상태 표시
        if (statusText != null)
        {
            statusText.text = (isReady ? "Ready" : "Not Ready");
            statusText.color = (isReady ? Color.green : Color.red);
        }
    }
}
