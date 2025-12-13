using UnityEngine;
using UnityEngine.UI;

public class JoinCreateLobby : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject createPanel;
    [SerializeField] private GameObject joinPanel;

    [Header("Buttons")]
    [SerializeField] private Button createButton;
    [SerializeField] private Button joinButton;

    private void Start()
    {
        createButton.onClick.AddListener(ShowCreatePanel);
        joinButton.onClick.AddListener(ShowJoinPanel);
        
        ShowJoinPanel();
    }

    public void ShowCreatePanel()
    {
        createPanel.SetActive(true);
        joinPanel.SetActive(false);

        SetButtonAlpha(createButton, 0.5f);
        SetButtonAlpha(joinButton, 0.2f);
    }

    public void ShowJoinPanel()
    {
        joinPanel.SetActive(true);
        createPanel.SetActive(false);

        SetButtonAlpha(joinButton, 0.5f);
        SetButtonAlpha(createButton, 0.2f);
    }

    private void SetButtonAlpha(Button button, float alpha)
    {
        var image = button.GetComponent<Image>();
        if (image != null)
        {
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}
