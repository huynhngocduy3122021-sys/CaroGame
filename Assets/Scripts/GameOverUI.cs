using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Color winColor;
    [SerializeField] private Color loseColor;
    [SerializeField] private Color drawsColor;
    [SerializeField] private  Button rematch;

    [Header("Animation Elements")]
    [SerializeField] private Image background; // Tấm nền đen để làm mờ
    [SerializeField] private RectTransform contentPanel; // Nhóm UI để làm hiệu ứng nảy
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
        if(e.playerWinType == GameManager.PlayerType.None)
        {
            text.text = "DRAWS";
            text.color = drawsColor;
        }
        else if(e.playerWinType == GameManager.Instance.GetLocalPlayerType())
        {
            text.text = "You Win!";
            text.color = winColor;
        }
        else
        {
            text.text = "You Lose!";
            text.color = loseColor;
        }
        
        show();
    }
    
    private void show()
    {
        gameObject.SetActive(true);
        // Bắt đầu chạy hiệu ứng Pop-up
        StartCoroutine(AnimatePopUp());
    }

    private void hide()
    {
        gameObject.SetActive(false);
    }

    // Hiệu ứng mờ nền và nảy UI
    private IEnumerator AnimatePopUp()
    {
        float duration = 0.5f; // Thời gian hiệu ứng (nửa giây)
        float elapsed = 0f;

        // 1. Đặt thông số ban đầu (Nền trong suốt, UI nhỏ xíu)
        if (background != null)
        {
            Color bgColor = background.color;
            bgColor.a = 0f; 
            background.color = bgColor;
        }
        
        if (contentPanel != null)
        {
            contentPanel.localScale = Vector3.zero;
        }

        // 2. Chạy hiệu ứng theo thời gian
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Làm mờ dần nền đen (từ 0 lên 0.7)
            if (background != null)
            {
                Color bgColor = background.color;
                bgColor.a = Mathf.Lerp(0f, 0.7f, t);
                background.color = bgColor;
            }

            // Hiệu ứng nảy (Ease Out Back) cho cụm UI
            if (contentPanel != null)
            {
                float scale = EaseOutBack(t);
                contentPanel.localScale = new Vector3(scale, scale, 1f);
            }

            yield return null; // Đợi đến frame tiếp theo
        }

        // 3. Chốt thông số cuối cùng để đảm bảo không bị sai lệch
        if (background != null)
        {
            Color bgColor = background.color;
            bgColor.a = 0.7f;
            background.color = bgColor;
        }
        if (contentPanel != null)
        {
            contentPanel.localScale = Vector3.one; // Kích thước gốc
        }
    }

    // Công thức toán học tạo độ nảy mượt mà
    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
    

