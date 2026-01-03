using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleUI : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string nextSceneName = "ElementSelectScene";

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    private void OnStartClicked()
    {
        SceneManager.LoadScene(nextSceneName);
    }

    private void OnQuitClicked()
    {
        Application.Quit();

#if UNITY_EDITOR
        // 在 Editor 裡按離開可以直接停 Play
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
