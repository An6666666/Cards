using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleUI : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string nextSceneName = "ElementSelectScene";
    [SerializeField] private string newSceneName = "ElementSelectScene 1";

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button newButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartClicked);
        }

        if (newButton != null)
        {
            newButton.onClick.RemoveAllListeners();
            newButton.onClick.AddListener(OnNewClicked);
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

    private void OnNewClicked()
    {
        SceneManager.LoadScene(newSceneName);
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
