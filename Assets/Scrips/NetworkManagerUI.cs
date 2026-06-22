using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.InferenceEngine;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button  startHostButton;
    [SerializeField] private Button startClientButton;
    [SerializeField] private Button aiButton;
    [SerializeField] private ModelAsset aiModel;

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

        aiButton.onClick.AddListener(() => {
            Debug.Log("Start AI Game");
            aiButton.interactable = false;

            if (!NetworkManager.Singleton.StartHost())
            {
                aiButton.interactable = true;
                Debug.LogError("Could not start local host for AI game.");
                return;
            }

            StartCoroutine(StartAIGameWhenReady());
        });
    }

    private System.Collections.IEnumerator StartAIGameWhenReady()
    {
        yield return new WaitUntil(() =>
            GameManager.Instance != null &&
            GameManager.Instance.IsSpawned);

        if (aiModel == null)
        {
            Debug.LogError("AI model is not assigned.");
            NetworkManager.Singleton.Shutdown();
            aiButton.interactable = true;
            yield break;
        }

        GameManager.Instance.StartAIGame();
        CreateGameplayAgent();
        Hide();
    }

    private void CreateGameplayAgent()
    {
        GameObject agentObject = new GameObject("CaroGameplayAgent");
        agentObject.SetActive(false);

        CaroGameplayAgent agent = agentObject.AddComponent<CaroGameplayAgent>();
        BehaviorParameters behavior = agentObject.GetComponent<BehaviorParameters>();
        behavior.BehaviorName = "CaroAgent";
        behavior.BrainParameters.VectorObservationSize = CaroGameplayAgent.TotalPoints;
        behavior.BrainParameters.NumStackedVectorObservations = 1;
        behavior.BrainParameters.ActionSpec =
            ActionSpec.MakeDiscrete(CaroGameplayAgent.TotalPoints);
        behavior.BehaviorType = BehaviorType.InferenceOnly;
        behavior.Model = aiModel;
        behavior.DeterministicInference = true;

        agent.Configure(GameManager.Instance);
        agentObject.SetActive(true);
    }

    private void Hide(){
        gameObject.SetActive(false);
    }

    
}
