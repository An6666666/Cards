using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class TitleUI : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string nextSceneName = "ElementSelectScene";
    [SerializeField] private string newSceneName = "ElementSelectScene 1";

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button newButton;
    [SerializeField] private Button continueNewButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button illustratedBookButton;
    [SerializeField] private Button settingButton;

    [Header("Panels")]
    [SerializeField] private IllustratedBookPanelController illustratedBookPanelController;
    [SerializeField] private TitleTutorialPromptController tutorialPromptController;

    [Header("Selection Indicator")]
    [SerializeField] private RectTransform selectionArrow;
    [SerializeField] private Vector2 arrowOffset = new Vector2(-56f, 0f);

    [Header("Title Visuals")]
    [SerializeField] private Graphic backgroundGraphic;
    [SerializeField] private RawImage backgroundVideoSurface;
    [SerializeField] private VideoPlayer backgroundVideoPlayer;
    [SerializeField] private RectTransform fireEffectRoot;
    [SerializeField] private VideoClip backgroundVideoClip;

    private readonly Button[] menuButtons = new Button[6];
    private RectTransform currentTarget;
    private RenderTexture backgroundVideoTexture;
    private Coroutine initialSelectionRoutine;
    private Text startTitleText;
    private TMP_Text startTitleTmpText;
    private string defaultStartButtonTitle;

    private const string ContinueTitleSuffix = "(繼續關卡)";

    private void Awake()
    {
        RunManager.DestroyInstance();
        AudioManager.Instance?.RefreshSceneBGM();

        SetupTitleVisuals();
        ResolvePanelReferences();
        ResolveButtonReferences();
        CacheMenuButtons();
        CacheStartButtonTitleReferences();
        WireButtons();
        RegisterSelectionTargets();
        RefreshContinueState();
        SetSelectionArrowVisible(false);
    }

    private void Start()
    {
        RefreshContinueState();
        EnsureInitialSelection();
        RefreshSelectionArrow();
        QueueInitialSelectionRefresh();
    }

    private void OnEnable()
    {
        EnsureBackgroundVideoPlaying();
        RefreshContinueState();
        EnsureInitialSelection();
        RefreshSelectionArrow();
        QueueInitialSelectionRefresh();
    }

    private void OnDisable()
    {
        if (initialSelectionRoutine != null)
        {
            StopCoroutine(initialSelectionRoutine);
            initialSelectionRoutine = null;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            RefreshContinueState();
            QueueInitialSelectionRefresh();
        }
    }

    private void OnDestroy()
    {
        if (backgroundVideoPlayer != null)
        {
            backgroundVideoPlayer.Stop();
            backgroundVideoPlayer.targetTexture = null;
        }

        if (backgroundVideoTexture != null)
        {
            backgroundVideoTexture.Release();
            Destroy(backgroundVideoTexture);
        }
    }

    private void Update()
    {
        RefreshSelectionArrow();
    }

    public void NotifyButtonHighlighted(RectTransform target)
    {
        if (target == null || !IsTrackedButton(target.gameObject))
        {
            return;
        }

        currentTarget = target;
        MoveSelectionArrow(target);
    }

    private void CacheMenuButtons()
    {
        menuButtons[0] = continueNewButton;
        menuButtons[1] = startButton;
        menuButtons[2] = illustratedBookButton;
        menuButtons[3] = quitButton;
        menuButtons[4] = newButton;
        menuButtons[5] = settingButton;
    }

    private void ResolveButtonReferences()
    {
        startButton = ResolveButtonByObjectName(startButton, "StartButton");
        continueNewButton = ResolveButtonByObjectName(continueNewButton, "NewButton");
        menuButtons[0] = continueNewButton;
        menuButtons[1] = startButton;
        menuButtons[4] = newButton;
    }

    private void ResolvePanelReferences()
    {
        if (tutorialPromptController == null)
        {
            tutorialPromptController = GetComponent<TitleTutorialPromptController>();
        }

        if (tutorialPromptController == null)
        {
            tutorialPromptController = GetComponentInChildren<TitleTutorialPromptController>(true);
        }
    }

    private Button ResolveButtonByObjectName(Button current, string objectName)
    {
        if (current != null && current.gameObject != null && current.gameObject.name == objectName)
        {
            return current;
        }

        Canvas rootCanvas = ResolveRootCanvas();
        if (rootCanvas == null)
        {
            return null;
        }

        Button[] buttons = rootCanvas.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];
            if (candidate != null && candidate.gameObject.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private void CacheStartButtonTitleReferences()
    {
        if (startButton == null)
        {
            return;
        }

        Transform titleTransform = startButton.transform.Find("TitleText");
        if (titleTransform == null)
        {
            titleTransform = startButton.GetComponentInChildren<Text>(true)?.transform;
        }

        if (titleTransform == null)
        {
            titleTransform = startButton.GetComponentInChildren<TMP_Text>(true)?.transform;
        }

        if (titleTransform == null)
        {
            return;
        }

        startTitleText = titleTransform.GetComponent<Text>();
        startTitleTmpText = titleTransform.GetComponent<TMP_Text>();

        if (string.IsNullOrEmpty(defaultStartButtonTitle))
        {
            if (startTitleTmpText != null)
            {
                defaultStartButtonTitle = startTitleTmpText.text;
            }
            else if (startTitleText != null)
            {
                defaultStartButtonTitle = startTitleText.text;
            }
        }
    }

    private void RefreshContinueState()
    {
        ResolveButtonReferences();
        bool hasSave = RunProgressPersistence.HasSavedProgress();

        if (continueNewButton != null)
        {
            continueNewButton.gameObject.SetActive(hasSave);
        }

        CacheStartButtonTitleReferences();

        string baseTitle = string.IsNullOrEmpty(defaultStartButtonTitle) ? "開始遊戲" : defaultStartButtonTitle;
        SetStartButtonTitle(hasSave ? $"{baseTitle}{ContinueTitleSuffix}" : baseTitle);
    }

    private void SetStartButtonTitle(string value)
    {
        if (startTitleTmpText != null)
        {
            startTitleTmpText.text = value;
        }

        if (startTitleText != null)
        {
            startTitleText.text = value;
        }
    }

    private void WireButtons()
    {
        ResolveButtonReferences();
        BindButton(startButton, OnStartClicked);
        BindButton(continueNewButton, OnNewClicked);
        BindButton(newButton, OnNoviceClicked);
        BindButton(quitButton, OnQuitClicked);
        BindButton(illustratedBookButton, OnIllustratedBookClicked);
        BindButton(settingButton, OnSettingsClicked);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void RegisterSelectionTargets()
    {
        foreach (Button button in menuButtons)
        {
            if (button == null)
            {
                continue;
            }

            TitleMenuSelectionTarget target = button.GetComponent<TitleMenuSelectionTarget>();
            if (target == null)
            {
                target = button.gameObject.AddComponent<TitleMenuSelectionTarget>();
            }

            target.Initialize(this, button.GetComponent<RectTransform>());
        }
    }

    private void EnsureInitialSelection()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || startButton == null)
        {
            return;
        }

        eventSystem.firstSelectedGameObject = startButton.gameObject;

        if (!IsTrackedButton(eventSystem.currentSelectedGameObject))
        {
            SelectButton(startButton);
        }

        if (IsTrackedButton(eventSystem.currentSelectedGameObject))
        {
            currentTarget = eventSystem.currentSelectedGameObject.GetComponent<RectTransform>();
        }
        else
        {
            currentTarget = startButton.GetComponent<RectTransform>();
        }
    }

    private void QueueInitialSelectionRefresh()
    {
        if (!isActiveAndEnabled || startButton == null)
        {
            return;
        }

        if (initialSelectionRoutine != null)
        {
            StopCoroutine(initialSelectionRoutine);
        }

        initialSelectionRoutine = StartCoroutine(RefreshInitialSelectionNextFrame());
    }

    private IEnumerator RefreshInitialSelectionNextFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (this == null || !isActiveAndEnabled)
        {
            initialSelectionRoutine = null;
            yield break;
        }

        EnsureInitialSelection();
        if (EventSystem.current != null && !IsTrackedButton(EventSystem.current.currentSelectedGameObject))
        {
            SelectButton(startButton);
        }

        RefreshSelectionArrow();
        initialSelectionRoutine = null;
    }

    private void RefreshSelectionArrow()
    {
        if (selectionArrow == null)
        {
            return;
        }

        if ((illustratedBookPanelController != null && illustratedBookPanelController.IsOpen) ||
            (tutorialPromptController != null && tutorialPromptController.IsOpen) ||
            (GlobalSettingsUI.Instance != null && GlobalSettingsUI.Instance.IsOpen))
        {
            SetSelectionArrowVisible(false);
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            GameObject selectedObject = eventSystem.currentSelectedGameObject;
            if (selectedObject != null && IsTrackedButton(selectedObject))
            {
                currentTarget = selectedObject.GetComponent<RectTransform>();
            }
        }

        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            SetSelectionArrowVisible(false);
            return;
        }

        MoveSelectionArrow(currentTarget);
    }

    private void MoveSelectionArrow(RectTransform target)
    {
        if (selectionArrow == null || target == null)
        {
            return;
        }

        Canvas rootCanvas = selectionArrow.GetComponentInParent<Canvas>();
        RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        if (canvasRect == null)
        {
            SetSelectionArrowVisible(false);
            return;
        }

        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector3 worldPoint = target.TransformPoint(target.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, worldPoint);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, eventCamera, out Vector2 localPoint))
        {
            SetSelectionArrowVisible(false);
            return;
        }

        selectionArrow.SetParent(canvasRect, false);
        selectionArrow.SetAsLastSibling();
        selectionArrow.anchoredPosition = localPoint + arrowOffset;
        SetSelectionArrowVisible(true);
    }

    private void SetSelectionArrowVisible(bool visible)
    {
        if (selectionArrow != null && selectionArrow.gameObject.activeSelf != visible)
        {
            selectionArrow.gameObject.SetActive(visible);
        }
    }

    private void SelectButton(Button button)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable())
        {
            return;
        }

        eventSystem.SetSelectedGameObject(null);
        eventSystem.SetSelectedGameObject(button.gameObject);
        currentTarget = button.GetComponent<RectTransform>();
    }

    private bool IsTrackedButton(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        foreach (Button button in menuButtons)
        {
            if (button != null && button.gameObject == target)
            {
                return true;
            }
        }

        return false;
    }

    private void OnStartClicked()
    {
        if (RunProgressPersistence.TryPrepareResumeFromTitle(out string bootstrapSceneName))
        {
            SceneTransitionLoader.LoadScene(bootstrapSceneName);
            return;
        }

        if (!IsNewButtonOpen())
        {
            OpenNewGameTutorialOrStart();
            return;
        }

        SceneTransitionLoader.LoadScene(nextSceneName);
    }

    private void OnNewClicked()
    {
        OpenNewGameTutorialOrStart();
    }

    private bool IsNewButtonOpen()
    {
        return continueNewButton != null && continueNewButton.gameObject.activeInHierarchy;
    }

    private void OpenNewGameTutorialOrStart()
    {
        if (tutorialPromptController != null)
        {
            tutorialPromptController.Open(StartNewGame);
            SetSelectionArrowVisible(false);
            return;
        }

        StartNewGame();
    }

    private void StartNewGame()
    {
        RunProgressPersistence.ClearSavedProgress();
        StartingDeckSelection.ClearSelection();
        SceneTransitionLoader.LoadScene(nextSceneName);
    }

    private void OnNoviceClicked()
    {
        SceneTransitionLoader.LoadScene(newSceneName);
    }

    private void OnQuitClicked()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnIllustratedBookClicked()
    {
        illustratedBookPanelController?.Open();
        SetSelectionArrowVisible(false);
    }

    private void OnSettingsClicked()
    {
        GlobalSettingsUI.OpenGlobal();
        SetSelectionArrowVisible(false);
    }

    private void SetupTitleVisuals()
    {
        Canvas rootCanvas = ResolveRootCanvas();
        if (rootCanvas == null)
            return;

        Transform canvasTransform = rootCanvas.transform;
        ResolveTitleVisualReferences(canvasTransform);
        CreateOrUpdateBackgroundVideo(canvasTransform);
        EnableFireEffect(canvasTransform);
    }

    private Canvas ResolveRootCanvas()
    {
        if (startButton != null)
            return startButton.GetComponentInParent<Canvas>();

        return FindObjectOfType<Canvas>();
    }

    private void ResolveTitleVisualReferences(Transform canvasTransform)
    {
        if (canvasTransform == null)
            return;

        if (backgroundGraphic == null)
        {
            Transform background = canvasTransform.Find("Background");
            if (background != null)
                backgroundGraphic = background.GetComponent<Graphic>();
        }

        if (backgroundVideoSurface == null && backgroundGraphic != null)
        {
            Transform videoSurface = backgroundGraphic.transform.Find("BackgroundVideo");
            GameObject videoSurfaceObject;
            if (videoSurface == null)
            {
                videoSurfaceObject = new GameObject("BackgroundVideo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                RectTransform videoRect = videoSurfaceObject.GetComponent<RectTransform>();
                videoRect.SetParent(backgroundGraphic.transform, false);
                videoRect.anchorMin = Vector2.zero;
                videoRect.anchorMax = Vector2.one;
                videoRect.offsetMin = Vector2.zero;
                videoRect.offsetMax = Vector2.zero;
                videoRect.SetAsLastSibling();
            }
            else
            {
                videoSurfaceObject = videoSurface.gameObject;
            }

            backgroundVideoSurface = videoSurfaceObject.GetComponent<RawImage>();
        }

        if (backgroundVideoPlayer == null && backgroundGraphic != null)
        {
            backgroundVideoPlayer = backgroundGraphic.GetComponent<VideoPlayer>();
            if (backgroundVideoPlayer == null)
                backgroundVideoPlayer = backgroundGraphic.gameObject.AddComponent<VideoPlayer>();
        }

        if (fireEffectRoot == null)
        {
            Transform fireEffect = canvasTransform.Find("Fire_effect");
            if (fireEffect != null)
                fireEffectRoot = fireEffect as RectTransform;
        }
    }

    private void CreateOrUpdateBackgroundVideo(Transform canvasTransform)
    {
        if (canvasTransform == null || backgroundVideoClip == null)
            return;

        ResolveTitleVisualReferences(canvasTransform);

        if (backgroundGraphic == null || backgroundVideoSurface == null || backgroundVideoPlayer == null)
            return;

        backgroundGraphic.enabled = false;
        backgroundGraphic.raycastTarget = false;
        backgroundVideoSurface.gameObject.SetActive(true);
        backgroundVideoSurface.raycastTarget = false;
        backgroundVideoSurface.color = Color.white;

        EnsureBackgroundVideoTexture();
        backgroundVideoSurface.texture = backgroundVideoTexture;

        backgroundVideoPlayer.playOnAwake = true;
        backgroundVideoPlayer.isLooping = true;
        backgroundVideoPlayer.skipOnDrop = true;
        backgroundVideoPlayer.waitForFirstFrame = true;
        backgroundVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        backgroundVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        backgroundVideoPlayer.targetTexture = backgroundVideoTexture;
        backgroundVideoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        backgroundVideoPlayer.clip = backgroundVideoClip;
        backgroundVideoPlayer.Play();
    }

    private void EnsureBackgroundVideoTexture()
    {
        if (backgroundVideoTexture != null)
            return;

        backgroundVideoTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
        {
            name = "TitleBackgroundVideo"
        };
        backgroundVideoTexture.Create();
    }

    private void EnsureBackgroundVideoPlaying()
    {
        if (backgroundVideoPlayer != null && backgroundVideoClip != null && !backgroundVideoPlayer.isPlaying)
            backgroundVideoPlayer.Play();
    }

    private void EnableFireEffect(Transform canvasTransform)
    {
        if (canvasTransform == null)
            return;

        ResolveTitleVisualReferences(canvasTransform);

        if (fireEffectRoot == null)
            return;

        if (!fireEffectRoot.gameObject.activeSelf)
            fireEffectRoot.gameObject.SetActive(true);

        Animator fireAnimator = fireEffectRoot.GetComponent<Animator>();
        if (fireAnimator != null)
        {
            fireAnimator.enabled = true;
            fireAnimator.Rebind();
            fireAnimator.Update(0f);
            fireAnimator.Play(0, 0, 0f);
        }
    }
}
