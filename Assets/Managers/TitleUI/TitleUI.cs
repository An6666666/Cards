using UnityEngine;
using UnityEngine.EventSystems;
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
    [SerializeField] private Button illustratedBookButton;
    [SerializeField] private Button settingButton;

    [Header("Panels")]
    [SerializeField] private IllustratedBookPanelController illustratedBookPanelController;

    [Header("Selection Indicator")]
    [SerializeField] private RectTransform selectionArrow;
    [SerializeField] private Vector2 arrowOffset = new Vector2(-56f, 0f);

    private readonly Button[] menuButtons = new Button[5];
    private RectTransform currentTarget;

    private void Awake()
    {
        RunManager.DestroyInstance();

        CacheMenuButtons();
        WireButtons();
        RegisterSelectionTargets();
        SetSelectionArrowVisible(false);
    }

    private void Start()
    {
        EnsureInitialSelection();
        RefreshSelectionArrow();
    }

    private void OnEnable()
    {
        EnsureInitialSelection();
        RefreshSelectionArrow();
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
        menuButtons[0] = startButton;
        menuButtons[1] = illustratedBookButton;
        menuButtons[2] = quitButton;
        menuButtons[3] = newButton;
        menuButtons[4] = settingButton;
    }

    private void WireButtons()
    {
        BindButton(startButton, OnStartClicked);
        BindButton(newButton, OnNewClicked);
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

        if (eventSystem.currentSelectedGameObject == null)
        {
            eventSystem.SetSelectedGameObject(startButton.gameObject);
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

    private void RefreshSelectionArrow()
    {
        if (selectionArrow == null)
        {
            return;
        }

        if ((illustratedBookPanelController != null && illustratedBookPanelController.IsOpen) ||
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
}
