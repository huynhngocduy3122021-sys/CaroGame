using UnityEngine;
using System;
using TMPro;

public class PlayerUI : MonoBehaviour
{
    [SerializeField] private GameObject crossArrowGameObject;
    [SerializeField] private GameObject circleArrowGameObject;
    [SerializeField] private GameObject crossYouTextGameObject;
    [SerializeField] private GameObject circleYouTextGameObject;
    [SerializeField] private TextMeshProUGUI crossText;
    [SerializeField] private TextMeshProUGUI circleText;

    private void Awake()
    {
        crossArrowGameObject.SetActive(false);
        circleArrowGameObject.SetActive(false);
        crossYouTextGameObject.SetActive(false);
        circleYouTextGameObject.SetActive(false);
        crossText.text = "0";
        circleText.text = "0";
        
    }

    private void Start()
    {
        GameManager.Instance.OnGameStarted += GameManager_OnGameStarted;
        GameManager.Instance.OnCurrentPlayerType += GameManager_OnCurrentPlayerType;
        GameManager.Instance.OnScoreChanged += GameManager_OnScoreChanged;
       
        Invoke(nameof(updateVisual), 0.2f);
       
        
    }
    private void Update()
    {
        if (GameManager.Instance != null)
        {
            updateVisual();
        }
    }
    private void GameManager_OnScoreChanged(object sender , System.EventArgs e)
    {
        updateVisual();
    }

    private void updateVisual()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        crossText.text = "" + GameManager.Instance.getCrossScore();
        circleText.text = "" + GameManager.Instance.getCircleScore();
        
    }
    private void GameManager_OnCurrentPlayerType(object sender, EventArgs e)
    {
        UpdateCurrentArrow();
    }

    private void GameManager_OnGameStarted(object sender, EventArgs e)
    {
        if(GameManager.Instance.GetLocalPlayerType() == GameManager.PlayerType.Cross)
        {
            crossYouTextGameObject.SetActive(true);
        }
        else if(GameManager.Instance.GetLocalPlayerType() == GameManager.PlayerType.Circle)
        {
            circleYouTextGameObject.SetActive(true);
        }
        
        updateVisual();
        UpdateCurrentArrow();
    }
    private void UpdateCurrentArrow()
    {
        GameManager.PlayerType currentPlayer = GameManager.Instance.GetCurrentPlayerType();

        if(currentPlayer == GameManager.PlayerType.Cross)
        {
            crossArrowGameObject.SetActive(true);
            circleArrowGameObject.SetActive(false);
        }
        else if(GameManager.Instance.GetCurrentPlayerType() == GameManager.PlayerType.Circle)
        {
            crossArrowGameObject.SetActive(false);
            circleArrowGameObject.SetActive(true);
        }
        else
        {
            crossArrowGameObject.SetActive(false);
            circleArrowGameObject.SetActive(false);
        }
    }
}
