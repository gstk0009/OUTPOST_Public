using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenuController : MonoBehaviour
{
    public Button startButton;
    public string gameSceneName = "GameScene";

    void Awake()
    {
        if (startButton != null)
            startButton.onClick.AddListener(StartGame);
    }

    void StartGame()
    {
        if (startButton != null) startButton.interactable = false;
        GameSessionState.BeginRun();
        LoadGameAsync().Forget();
    }

    async UniTaskVoid LoadGameAsync()
    {
        await SceneManager.LoadSceneAsync(gameSceneName).ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
    }

    void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(StartGame);
    }
}
