using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneTransitionLoader : MonoBehaviour
{
    private const string LoadingOverlayResourcePath = "LoadingOverlay";
    private const float DefaultMinimumDisplayDuration = 0.75f;

    private static SceneTransitionLoader instance;

    private GameObject overlayInstance;
    private Coroutine activeTransitionCoroutine;

    public static bool IsLoading => instance != null && instance.activeTransitionCoroutine != null;

    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("SceneTransitionLoader: sceneName is empty.");
            return;
        }

        EnsureInstance().BeginTransition(sceneName);
    }

    private static SceneTransitionLoader EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject host = new GameObject(nameof(SceneTransitionLoader));
        DontDestroyOnLoad(host);
        instance = host.AddComponent<SceneTransitionLoader>();
        instance.EnsureOverlayInstance();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureOverlayInstance();
    }

    private void BeginTransition(string sceneName)
    {
        if (activeTransitionCoroutine != null)
        {
            return;
        }

        activeTransitionCoroutine = StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        SetOverlayVisible(true);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            SetOverlayVisible(false);
            activeTransitionCoroutine = null;
            yield break;
        }

        operation.allowSceneActivation = false;

        float elapsed = 0f;
        while (!operation.isDone)
        {
            elapsed += Time.unscaledDeltaTime;
            bool readyToActivate = operation.progress >= 0.9f && elapsed >= DefaultMinimumDisplayDuration;
            if (readyToActivate)
            {
                operation.allowSceneActivation = true;
            }

            yield return null;
        }

        yield return null;
        SetOverlayVisible(false);
        activeTransitionCoroutine = null;
    }

    private void EnsureOverlayInstance()
    {
        if (overlayInstance != null)
        {
            return;
        }

        GameObject overlayPrefab = Resources.Load<GameObject>(LoadingOverlayResourcePath);
        if (overlayPrefab == null)
        {
            Debug.LogWarning(
                $"SceneTransitionLoader: could not load Resources/{LoadingOverlayResourcePath}.prefab. " +
                "Create a prefab at Assets/Resources/LoadingOverlay.prefab.");
            return;
        }

        overlayInstance = Instantiate(overlayPrefab, transform);
        overlayInstance.name = overlayPrefab.name;
        DontDestroyOnLoad(overlayInstance);
        SetOverlayVisible(false);
    }

    private void SetOverlayVisible(bool visible)
    {
        EnsureOverlayInstance();

        if (overlayInstance == null)
        {
            return;
        }

        if (overlayInstance.activeSelf != visible)
        {
            overlayInstance.SetActive(visible);
        }
    }
}
