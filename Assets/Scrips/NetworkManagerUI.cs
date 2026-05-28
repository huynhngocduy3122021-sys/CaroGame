using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button  startHostButton;
    [SerializeField] private Button startClientButton;

    private void Awake()
    {
        startHostButton.onClick.AddListener(() => {
            Debug.Log("Start Host");
            NetworkManager.Singleton.StartHost();
            Hide();
        });

        startClientButton.onClick.AddListener(() => {
            Debug.Log("Start Client");
            NetworkManager.Singleton.StartClient();
            Hide();
        });
    }

    private void Hide(){
        gameObject.SetActive(false);
    }

    
}
