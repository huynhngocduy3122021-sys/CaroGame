using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Color winColor;
    [SerializeField] private Color loseColor;
    [SerializeField] private Color drawsColor;
    [SerializeField] private  Button rematch;
    private void Awake()
    {
        rematch.onClick.AddListener(() => {
            GameManager.Instance.RematchRpc();
        });
    }

     
    private void Start()
    {
        GameManager.Instance.OnGameWin += GameManager_OnGameWin;
        GameManager.Instance.OnRematch += GameManager_OnRematch;
        hide();
    }
    private void GameManager_OnRematch(object sender, System.EventArgs e)
    {
        hide();
    }
    private void GameManager_OnGameWin(object sender, GameManager.OnGameWinEventArgs e)
    {

        if(e.playerWinType == GameManager.Instance.GetLocalPlayerType())
        {
            text.text = "You Win!";
            text.color = winColor;
        }
        else
        {
            text.text = "You Lose!";
            text.color = loseColor;
        }

        if(e.playerWinType == GameManager.PlayerType.None)
        {
            text.text = "DRAWS";
            text.color = drawsColor;
        }
        show();
    }
    
    private void show()
    {
        gameObject.SetActive(true);
    }

    private void hide()
    {
        gameObject.SetActive(false);
    }
}
    

