using System;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-90)]
public sealed class ShopTutorialController : MonoBehaviour
{
    public event Action TutorialCompleted;

    private const string OverlayRootName = "ShopTutorialOverlay";
    private const float DefaultPromptPanelWidth = 760f;
    private const float DefaultPromptPanelHeight = 140f;
    private const float DefaultHintWidth = 320f;
    private const float DefaultHintHeight = 180f;

    [Header("References")]
    [SerializeField] private ShopUIManager shopUIManager;
    [SerializeField] private ShopNpcDialogueController shopNpcController;
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private ShopTutorialDefinition tutorialDefinition;

    [Header("Overlay")]
    [SerializeField] private Color promptPanelColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color promptTextColor = Color.white;

    private RectTransform overlayRoot;
    private Image tutorialImageView;
    private RectTransform tutorialImageRect;
    private Image promptPanelImage;
    private Text promptText;
    private Button continueButton;
    private Text continueButtonLabel;
    private bool isRunning;
    private int currentStepIndex = -1;
    private bool completionRaised;

    public bool IsRunning => isRunning;

    public bool Begin(ShopTutorialDefinition definition = null)
    {
        ResolveReferences();

        ShopTutorialDefinition resolvedDefinition = definition != null ? definition : tutorialDefinition;
        if (shopUIManager == null || resolvedDefinition == null || !resolvedDefinition.HasSteps)
            return false;

        tutorialDefinition = resolvedDefinition;
        EnsureOverlay();
        if (overlayRoot == null)
            return false;

        shopUIManager.AttachTutorialController(this);

        isRunning = true;
        currentStepIndex = -1;
        completionRaised = false;

        overlayRoot.gameObject.SetActive(true);
        overlayRoot.SetAsLastSibling();
        PromoteProtectedUiAboveOverlay();

        ShowStep(0);
        return true;
    }

    public void HandleAction(ShopTutorialAction action)
    {
        if (!isRunning)
            return;

        ShopTutorialDefinition.Step currentStep = GetCurrentStep();
        if (currentStep == null || currentStep.RequiredAction != action)
            return;

        AdvanceToNextStep();
    }

    private void OnDestroy()
    {
        if (isRunning && shopUIManager != null)
            shopUIManager.SetTutorialInteractionState(false);

        if (isRunning && shopNpcController != null)
            shopNpcController.ClearPinnedLine();
    }

    private void ResolveReferences()
    {
        if (shopUIManager == null)
            shopUIManager = GetComponent<ShopUIManager>() ?? FindObjectOfType<ShopUIManager>(true);

        if (shopNpcController == null && shopUIManager != null)
            shopNpcController = shopUIManager.ShopNpcController;

        if (shopNpcController == null)
            shopNpcController = GetComponent<ShopNpcDialogueController>() ?? FindObjectOfType<ShopNpcDialogueController>(true);

        Canvas preferredShopCanvas = shopUIManager != null ? shopUIManager.GetPreferredTutorialCanvas() : null;
        if (preferredShopCanvas != null)
        {
            Canvas preferredRootCanvas = preferredShopCanvas.rootCanvas != null ? preferredShopCanvas.rootCanvas : preferredShopCanvas;
            Canvas currentRootCanvas = overlayCanvas != null
                ? (overlayCanvas.rootCanvas != null ? overlayCanvas.rootCanvas : overlayCanvas)
                : null;

            if (currentRootCanvas == null || currentRootCanvas != preferredRootCanvas)
                overlayCanvas = preferredRootCanvas;
        }

        if (overlayCanvas == null)
            overlayCanvas = FindBestSceneCanvas();
    }

    private void EnsureOverlay()
    {
        ResolveReferences();
        if (overlayCanvas == null)
            return;

        Canvas rootCanvas = overlayCanvas.rootCanvas != null ? overlayCanvas.rootCanvas : overlayCanvas;
        Transform existingRoot = rootCanvas.transform.Find(OverlayRootName);
        if (existingRoot != null)
        {
            overlayRoot = existingRoot as RectTransform;
            CacheOverlayReferences();
            return;
        }

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject rootObject = new GameObject(OverlayRootName, typeof(RectTransform));
        overlayRoot = rootObject.GetComponent<RectTransform>();
        overlayRoot.SetParent(rootCanvas.transform, false);
        overlayRoot.anchorMin = Vector2.zero;
        overlayRoot.anchorMax = Vector2.one;
        overlayRoot.offsetMin = Vector2.zero;
        overlayRoot.offsetMax = Vector2.zero;
        overlayRoot.SetAsLastSibling();

        tutorialImageView = CreateImage(
            "HintImage",
            overlayRoot,
            Color.white,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(DefaultHintWidth, DefaultHintHeight));
        tutorialImageView.raycastTarget = false;
        tutorialImageView.preserveAspect = true;
        tutorialImageRect = tutorialImageView.rectTransform;

        promptPanelImage = CreateImage(
            "PromptPanel",
            overlayRoot,
            promptPanelColor,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 28f),
            new Vector2(DefaultPromptPanelWidth, DefaultPromptPanelHeight));
        promptPanelImage.raycastTarget = false;

        RectTransform promptPanelRect = promptPanelImage.rectTransform;

        GameObject promptTextObject = new GameObject("PromptText", typeof(RectTransform), typeof(Text));
        RectTransform promptTextRect = promptTextObject.GetComponent<RectTransform>();
        promptTextRect.SetParent(promptPanelRect, false);
        promptTextRect.anchorMin = new Vector2(0f, 0f);
        promptTextRect.anchorMax = new Vector2(1f, 1f);
        promptTextRect.offsetMin = new Vector2(24f, 18f);
        promptTextRect.offsetMax = new Vector2(-168f, -18f);

        promptText = promptTextObject.GetComponent<Text>();
        promptText.font = defaultFont;
        promptText.fontSize = 28;
        promptText.color = promptTextColor;
        promptText.alignment = TextAnchor.MiddleLeft;
        promptText.horizontalOverflow = HorizontalWrapMode.Wrap;
        promptText.verticalOverflow = VerticalWrapMode.Overflow;
        promptText.raycastTarget = false;

        GameObject continueButtonObject = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform continueButtonRect = continueButtonObject.GetComponent<RectTransform>();
        continueButtonRect.SetParent(promptPanelRect, false);
        continueButtonRect.anchorMin = new Vector2(1f, 0.5f);
        continueButtonRect.anchorMax = new Vector2(1f, 0.5f);
        continueButtonRect.anchoredPosition = new Vector2(-76f, 0f);
        continueButtonRect.sizeDelta = new Vector2(132f, 56f);

        Image continueButtonImage = continueButtonObject.GetComponent<Image>();
        continueButtonImage.color = new Color(1f, 1f, 1f, 0.92f);
        continueButton = continueButtonObject.GetComponent<Button>();
        continueButton.targetGraphic = continueButtonImage;
        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(HandleContinuePressed);

        GameObject continueLabelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        RectTransform continueLabelRect = continueLabelObject.GetComponent<RectTransform>();
        continueLabelRect.SetParent(continueButtonRect, false);
        continueLabelRect.anchorMin = Vector2.zero;
        continueLabelRect.anchorMax = Vector2.one;
        continueLabelRect.offsetMin = Vector2.zero;
        continueLabelRect.offsetMax = Vector2.zero;

        continueButtonLabel = continueLabelObject.GetComponent<Text>();
        continueButtonLabel.font = defaultFont;
        continueButtonLabel.fontSize = 24;
        continueButtonLabel.color = Color.black;
        continueButtonLabel.alignment = TextAnchor.MiddleCenter;
        continueButtonLabel.text = "\u7e7c\u7e8c";
        continueButtonLabel.raycastTarget = false;

        overlayRoot.gameObject.SetActive(false);
    }

    private Canvas FindBestSceneCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        Canvas fallback = null;

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas candidate = canvases[i];
            if (candidate == null)
                continue;

            Canvas rootCandidate = candidate.rootCanvas != null ? candidate.rootCanvas : candidate;
            if (fallback == null)
                fallback = rootCandidate;

            if (!rootCandidate.isRootCanvas)
                continue;

            if (shopUIManager != null && rootCandidate.gameObject.scene != shopUIManager.gameObject.scene)
                continue;

            return rootCandidate;
        }

        return fallback;
    }

    private void CacheOverlayReferences()
    {
        if (overlayRoot == null)
            return;

        if (tutorialImageView == null)
        {
            Transform child = overlayRoot.Find("HintImage");
            if (child != null)
                tutorialImageView = child.GetComponent<Image>();
        }

        tutorialImageRect = tutorialImageView != null ? tutorialImageView.rectTransform : null;

        if (promptPanelImage == null)
        {
            Transform child = overlayRoot.Find("PromptPanel");
            if (child != null)
                promptPanelImage = child.GetComponent<Image>();
        }

        if (promptPanelImage != null && promptText == null)
        {
            Transform child = promptPanelImage.transform.Find("PromptText");
            if (child != null)
                promptText = child.GetComponent<Text>();
        }

        if (promptPanelImage != null && continueButton == null)
        {
            Transform child = promptPanelImage.transform.Find("ContinueButton");
            if (child != null)
                continueButton = child.GetComponent<Button>();
        }

        if (continueButton != null && continueButtonLabel == null)
        {
            Transform child = continueButton.transform.Find("Label");
            if (child != null)
                continueButtonLabel = child.GetComponent<Text>();
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(HandleContinuePressed);
        }
    }

    private Image CreateImage(
        string objectName,
        RectTransform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private void HandleContinuePressed()
    {
        if (!isRunning)
            return;

        ShopTutorialDefinition.Step currentStep = GetCurrentStep();
        if (currentStep == null || currentStep.RequiredAction != ShopTutorialAction.None)
            return;

        AdvanceToNextStep();
    }

    private ShopTutorialDefinition.Step GetCurrentStep()
    {
        if (!isRunning || tutorialDefinition == null || tutorialDefinition.Steps == null)
            return null;

        if (currentStepIndex < 0 || currentStepIndex >= tutorialDefinition.Steps.Count)
            return null;

        return tutorialDefinition.Steps[currentStepIndex];
    }

    private void ShowStep(int stepIndex)
    {
        if (tutorialDefinition == null || tutorialDefinition.Steps == null || stepIndex < 0)
        {
            CompleteTutorial();
            return;
        }

        if (stepIndex >= tutorialDefinition.Steps.Count)
        {
            CompleteTutorial();
            return;
        }

        currentStepIndex = stepIndex;
        ShopTutorialDefinition.Step step = tutorialDefinition.Steps[stepIndex];

        if (step.SelectTabBeforeStep && shopUIManager != null)
            shopUIManager.ForceSetTabForTutorial(step.TabToSelect);

        if (shopUIManager != null)
            shopUIManager.SetTutorialInteractionState(true, step.RequiredAction);

        UpdatePrompt(step);
        UpdateDialogue(step);
        UpdateHint(step);
        PromoteProtectedUiAboveOverlay();
    }

    private void UpdatePrompt(ShopTutorialDefinition.Step step)
    {
        if (promptText == null)
            return;

        string prompt = string.IsNullOrWhiteSpace(step.Prompt)
            ? GetDefaultPrompt(step.RequiredAction)
            : step.Prompt.Trim();

        promptText.text = prompt;

        bool usesContinueButton = step.RequiredAction == ShopTutorialAction.None;
        if (continueButton != null)
            continueButton.gameObject.SetActive(usesContinueButton);

        if (continueButtonLabel != null)
            continueButtonLabel.text = "\u7e7c\u7e8c";
    }

    private void UpdateDialogue(ShopTutorialDefinition.Step step)
    {
        string dialogue = string.IsNullOrWhiteSpace(step.Dialogue) ? string.Empty : step.Dialogue.Trim();
        if (shopNpcController != null)
        {
            if (string.IsNullOrEmpty(dialogue))
                shopNpcController.ClearPinnedLine();
            else
                shopNpcController.ShowPinnedLine(dialogue);

            return;
        }

        if (promptText == null || string.IsNullOrEmpty(dialogue))
            return;

        if (string.IsNullOrEmpty(promptText.text))
        {
            promptText.text = dialogue;
            return;
        }

        promptText.text = dialogue + "\n" + promptText.text;
    }

    private void UpdateHint(ShopTutorialDefinition.Step step)
    {
        if (tutorialImageView == null || tutorialImageRect == null)
            return;

        Sprite hintSprite = step.TutorialImage;
        if (hintSprite == null)
        {
            tutorialImageView.enabled = false;
            tutorialImageView.sprite = null;
            return;
        }

        tutorialImageView.enabled = true;
        tutorialImageView.sprite = hintSprite;
        tutorialImageView.SetNativeSize();
        tutorialImageView.color = new Color(1f, 1f, 1f, Mathf.Clamp01(step.TutorialImageAlpha));

        RectTransform focusRect = shopUIManager != null ? shopUIManager.GetTutorialAnchor(step.FocusAnchor) : null;
        tutorialImageRect.anchoredPosition = step.ImageOffset;
        tutorialImageRect.sizeDelta = ResolveHintSize(step, focusRect, hintSprite);
    }

    private Vector2 ResolveHintSize(ShopTutorialDefinition.Step step, RectTransform focusRect, Sprite hintSprite)
    {
        Vector2 explicitSize = step.ImageSize;
        if (step.MatchTargetRect && focusRect != null)
        {
            Vector2 targetSize = focusRect.rect.size;
            if (targetSize.x > 0f && targetSize.y > 0f)
                return targetSize;
        }

        if (explicitSize.x > 0f && explicitSize.y > 0f)
            return explicitSize;

        if (hintSprite != null)
        {
            Rect rect = hintSprite.rect;
            if (rect.width > 0f && rect.height > 0f)
                return new Vector2(rect.width, rect.height);
        }

        return new Vector2(DefaultHintWidth, DefaultHintHeight);
    }

    private void AdvanceToNextStep()
    {
        ShowStep(currentStepIndex + 1);
    }

    private void PromoteProtectedUiAboveOverlay()
    {
        if (overlayRoot == null || shopNpcController == null)
            return;

        RectTransform npcRect = shopNpcController.ShopNpcRectTransform;
        if (npcRect != null && npcRect.parent == overlayRoot.parent)
            npcRect.SetAsLastSibling();

        RectTransform messageRootRect = shopNpcController.MessageRootRectTransform;
        if (messageRootRect != null && messageRootRect.parent == overlayRoot.parent)
            messageRootRect.SetAsLastSibling();
    }

    private void CompleteTutorial()
    {
        isRunning = false;
        currentStepIndex = -1;

        if (shopUIManager != null)
            shopUIManager.SetTutorialInteractionState(false);

        if (shopNpcController != null)
            shopNpcController.ClearPinnedLine();

        if (overlayRoot != null)
            overlayRoot.gameObject.SetActive(false);

        if (completionRaised)
            return;

        completionRaised = true;
        TutorialCompleted?.Invoke();
    }

    private static string GetDefaultPrompt(ShopTutorialAction action)
    {
        switch (action)
        {
            case ShopTutorialAction.OpenCardsTab:
                return "\u8acb\u9ede\u64ca\u5361\u724c\u6309\u9215\u3002";
            case ShopTutorialAction.OpenRelicsTab:
                return "\u8acb\u9ede\u64ca\u6cd5\u5668\u6309\u9215\u3002";
            case ShopTutorialAction.OpenRemovalTab:
                return "\u8acb\u9ede\u64ca\u6e05\u9664\u6309\u9215\u3002";
            case ShopTutorialAction.PreviousPage:
                return "\u8acb\u9ede\u64ca\u4e0a\u4e00\u9801\u6309\u9215\u3002";
            case ShopTutorialAction.NextPage:
                return "\u8acb\u9ede\u64ca\u4e0b\u4e00\u9801\u6309\u9215\u3002";
            case ShopTutorialAction.RefreshRemoval:
                return "\u8acb\u9ede\u64ca\u91cd\u65b0\u6574\u7406\u6309\u9215\u3002";
            case ShopTutorialAction.ReturnToRunMap:
                return "\u8acb\u9ede\u64ca\u8fd4\u56de\u6309\u9215\u3002";
            case ShopTutorialAction.None:
            default:
                return "\u95b1\u8b80\u5b8c\u5f8c\u6309\u4e0b\u7e7c\u7e8c\u3002";
        }
    }
}
