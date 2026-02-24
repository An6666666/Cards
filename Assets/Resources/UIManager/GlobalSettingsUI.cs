using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GlobalSettingsUI : MonoBehaviour
{
    public const string DefaultGlobalUiPrefabResourcePath = "GlobalUI/GlobalUIRoot";

    public static GlobalSettingsUI Instance { get; private set; }

    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Header("Prefab References")]
    [SerializeField] private bool autoFindReferencesInChildren = true;
    [SerializeField] private GameObject settingsOverlay;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject globalExtrasRoot;
    [SerializeField] private ScrollRect settingsScrollRect;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button quitButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        EnsureInstance();
    }

    public static GlobalSettingsUI EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GlobalSettingsUI found = Object.FindObjectOfType<GlobalSettingsUI>();
        if (found != null)
        {
            return found;
        }

        GameObject prefab = UnityEngine.Resources.Load<GameObject>(DefaultGlobalUiPrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("GlobalSettingsUI: missing prefab at Resources/" + DefaultGlobalUiPrefabResourcePath);
            return null;
        }

        GameObject instance = Object.Instantiate(prefab);
        instance.name = "GlobalUIRoot";

        GlobalSettingsUI component = instance.GetComponent<GlobalSettingsUI>();
        if (component == null)
        {
            Debug.LogError("GlobalSettingsUI: prefab is missing GlobalSettingsUI component.");
            return null;
        }

        return component;
    }

    public static void OpenGlobal()
    {
        GlobalSettingsUI ui = EnsureInstance();
        if (ui != null)
        {
            ui.Open();
        }
    }

    public static void CloseGlobal()
    {
        if (Instance != null)
        {
            Instance.Close();
        }
    }

    public static void ToggleGlobal()
    {
        GlobalSettingsUI ui = EnsureInstance();
        if (ui != null)
        {
            ui.Toggle();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CacheReferencesFromPrefabIfNeeded();
        WireCloseButton();
        WireQuitButton();
        SetVisible(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }
    }

    public void Open()
    {
        CacheReferencesFromPrefabIfNeeded();
        EnsureEventSystem();
        SetVisible(true);
    }

    public void Close()
    {
        if (settingsOverlay != null)
        {
            settingsOverlay.SetActive(false);
        }
    }

    public void Toggle()
    {
        CacheReferencesFromPrefabIfNeeded();
        EnsureEventSystem();

        bool nextVisible = true;
        if (settingsOverlay != null)
        {
            nextVisible = !settingsOverlay.activeSelf;
        }

        SetVisible(nextVisible);
    }

    private void SetVisible(bool visible)
    {
        if (settingsOverlay == null)
        {
            Debug.LogWarning("GlobalSettingsUI: settingsOverlay reference is missing.");
            return;
        }

        settingsOverlay.SetActive(visible);
    }

    private void WireCloseButton()
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.onClick.RemoveListener(Close);
        closeButton.onClick.AddListener(Close);
    }

    private void WireQuitButton()
    {
        if (quitButton == null)
        {
            return;
        }

        quitButton.onClick.RemoveListener(QuitGame);
        quitButton.onClick.AddListener(QuitGame);
    }

    private void CacheReferencesFromPrefabIfNeeded()
    {
        if (!autoFindReferencesInChildren)
        {
            return;
        }

        if (settingsOverlay == null)
        {
            Transform t = transform.Find("SettingsOverlay");
            if (t != null) settingsOverlay = t.gameObject;
        }

        if (settingsPanel == null && settingsOverlay != null)
        {
            Transform t = settingsOverlay.transform.Find("SettingsPanel");
            if (t != null) settingsPanel = t.gameObject;
        }

        if (globalExtrasRoot == null)
        {
            Transform t = transform.Find("GlobalExtras");
            if (t != null) globalExtrasRoot = t.gameObject;
        }

        if (settingsScrollRect == null && settingsPanel != null)
        {
            settingsScrollRect = settingsPanel.GetComponentInChildren<ScrollRect>(true);
        }

        if (closeButton == null && settingsPanel != null)
        {
            Transform closeTransform = settingsPanel.transform.Find("Header/CloseButton");
            if (closeTransform != null)
            {
                closeButton = closeTransform.GetComponent<Button>();
            }

            if (closeButton == null)
            {
                closeButton = settingsPanel.GetComponentInChildren<Button>(true);
            }
        }

        if (quitButton == null && settingsPanel != null)
        {
            Transform quitTransform = settingsPanel.transform.Find("Footer/QuitButton");
            if (quitTransform != null)
            {
                quitButton = quitTransform.GetComponent<Button>();
            }
        }
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("GlobalSettingsUI: QuitGame called (Editor mode, Application.Quit skipped).");
#else
        Application.Quit();
#endif
    }
}
