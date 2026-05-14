using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public partial class RewardUI : MonoBehaviour
{
    private enum PackOpenState
    {
        Idle,
        Opening,
        Choosing
    }

    private enum RewardStage
    {
        None,
        CardReward,
        RelicReward
    }

    private sealed class RelicChoiceView
    {
        public RelicBase Relic;
        public RectTransform ContainerRect;
        public Image BackgroundImage;
        public Button Button;
        public BattleRelicUIItem ItemView;
    }

    private BattleManager manager;
    private GameObject skipHoverIndicator;
    private Image skipHoverIndicatorImage;
    private bool skipHoverConfigured;
    private bool hasCachedSkipHoverIndicatorVisuals;
    private bool packHoverConfigured;
    private Coroutine openPackCoroutine;
    private Tween bagIdleTween;
    private Tween lightIdleFadeTween;
    private Tween bagHoverTween;
    private PackOpenState packOpenState = PackOpenState.Idle;
    private RewardStage rewardStage = RewardStage.None;
    private readonly List<Button> rewardButtons = new List<Button>();
    private readonly List<CardUI> rewardCardUIs = new List<CardUI>();
    private readonly List<RelicChoiceView> relicChoiceViews = new List<RelicChoiceView>();
    private List<CardBase> pendingCardChoices;
    private List<RelicBase> pendingRelicChoices;
    private RelicChoiceView selectedRelicChoiceView;
    private Image skipButtonImage;
    private TextAnchor defaultGoldTextAlignment = TextAnchor.UpperLeft;
    private bool hasCachedGoldTextAlignment;
    private Sprite defaultSkipButtonSprite;
    private bool hasCachedSkipButtonSprite;
    private Sprite defaultSkipHoverIndicatorSprite;
    private Vector2 defaultSkipHoverIndicatorSize;
    private Vector2 defaultSkipHoverIndicatorOffset;
    private bool defaultSkipHoverIndicatorPreserveAspect;
    private bool useConfirmSkipButtonVisuals;
    private int currentGoldReward;
    private bool stageSelectionCommitted;
    private bool closeRequested;

    [SerializeField] private Text goldText;
    [SerializeField] private Button packButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private Button allDeckCounterButton;
    [SerializeField] private Transform cardParent;

    [Header("Reward Card Display")]
    [SerializeField] private bool useRewardCardScale = true;
    [SerializeField] private Vector3 rewardCardScale = new Vector3(0.8f, 0.8f, 1f);
    [SerializeField] private bool useRewardCardLayoutSize = false;
    [SerializeField] private Vector2 rewardCardPreferredSize = new Vector2(130f, 270f);

    [Header("Relic Reward Display")]
    [SerializeField] private string relicRewardTitle = "\u7372\u5F97\u6CD5\u5668";
    [SerializeField] private Sprite relicConfirmButtonSprite;
    [SerializeField] private Sprite relicConfirmButtonHoverSprite;
    [SerializeField] private bool relicConfirmButtonHoverPreserveAspect = false;
    [SerializeField] private Vector2 relicConfirmButtonHoverIndicatorSize = new Vector2(420f, 150f);
    [SerializeField] private Vector2 relicConfirmButtonHoverIndicatorOffset = new Vector2(0f, -1f);
    [SerializeField] private Vector2 relicChoicePreferredSize = new Vector2(215f, 215f);
    [SerializeField] private Vector2 relicIconSize = new Vector2(155f, 155f);
    [SerializeField] private Vector2 relicTooltipPositionOffset = new Vector2(0f, 120f);
    [SerializeField] private float relicTooltipScaleMultiplier = 1.7f;
    [SerializeField] private Color relicChoiceNormalColor = new Color(1f, 0.95f, 0.82f, 0.08f);
    [SerializeField] private Color relicChoiceSelectedColor = new Color(1f, 0.95f, 0.72f, 0.24f);
    [SerializeField] private float relicChoiceSelectedScale = 1.04f;
    [SerializeField] private float relicChoiceTweenDuration = 0.12f;

    [Header("Panel Timing")]
    [SerializeField] private float skipButtonRevealDelay = 0.5f;

    [Header("Pack Timing")]
    [SerializeField] private float idleBagScale = 1.01f;
    [SerializeField] private float idleBagDuration = 0.8f;
    [SerializeField] private float idleLightFadeDuration = 0.9f;
    [SerializeField] private float idleLightMinAlpha = 0.45f;
    [SerializeField] private float openBurstDuration = 0.5f;
    [SerializeField] private float openToRevealDelay = 0.25f;
    [SerializeField] private string packIdleStateName = "cardbag";
    [SerializeField] private string packOpenTriggerName = "cardbag_open";
    [SerializeField, Min(0f)] private float packOpenAnimationWaitDuration = 1.1f;
    [SerializeField, Min(0.01f)] private float effectAnimatorSpeed = 1f;
    [SerializeField] private bool waitForEffectBeforeBottom = true;
    [SerializeField] private float effectToBottomTimeOffset = -0.15f;
    [SerializeField, Min(0f)] private float effectBottomOverlapDuration = 0.3f;
    [SerializeField] private float bottomRevealDuration = 0.45f;
    [SerializeField] private float cardRevealDuration = 0.3f;
    [SerializeField] private float cardRevealInterval = 0.14f;
    [SerializeField] private float cardStartYOffset = -24f;
    [SerializeField] private float cardStartScale = 0.75f;
    [SerializeField] private float packHoverScale = 1.04f;
    [SerializeField] private float packHoverDuration = 0.12f;
    [SerializeField] private float packHoverReturnDuration = 0.16f;

    [Header("Card Glow Timing")]
    [SerializeField, Min(0f)] private float cardGlowRevealDelayAfterBottom = 0f;
    [SerializeField, Min(0f)] private float cardGlowSpreadDuration = 0.35f;
    [SerializeField, Min(0f)] private float cardGlowChildStagger = 0.04f;
    [SerializeField, Min(0.01f)] private float cardGlowStartScale = 0.85f;
    [SerializeField] private Ease cardGlowSpreadEase = Ease.OutBack;
    [SerializeField] private bool restartCardGlowChildAnimators = true;
    [SerializeField] private bool replayCardGlowOnEachCardReveal = false;

    private RectTransform cardbagBag;
    private RectTransform cardbagLight;
    private RectTransform effectRoot;
    private RectTransform bottomRoot;
    private RectTransform cardGlowRoot;

    private Vector3 bagDefaultScale = Vector3.one;
    private Vector3 lightDefaultScale = Vector3.one;
    private Vector3 effectDefaultScale = Vector3.one;
    private Vector3 bottomDefaultScale = Vector3.one;
    private Vector3 glowDefaultScale = Vector3.one;
    private Coroutine skipButtonRevealCoroutine;
    private Coroutine effectHideCoroutine;

    private void Awake()
    {
        ResolveAllDeckCounterButton();
        WireAllDeckCounterButton();
        ResolvePackVisualReferences();
        CacheDefaultVisualState();
        CacheGoldTextStyle();
        ConfigureSkipButtonHover();
        ConfigurePackButtonHover();
        CacheSkipButtonVisuals();
        SetSkipHoverVisible(false);
        SetSkipButtonUseConfirmSprite(false);
    }

    private void OnDisable()
    {
        StopSkipButtonReveal();
        StopOpenPackFlow();
        HideSkipButton();
        SetSkipHoverVisible(false);
        SetSkipButtonUseConfirmSprite(false);
    }

    private void OnDestroy()
    {
        if (allDeckCounterButton != null)
        {
            allDeckCounterButton.onClick.RemoveListener(OnRewardAllDeckCounterClicked);
        }
    }

    public void Show(BattleManager bm, int goldReward, List<CardBase> cardChoices)
    {
        Show(bm, goldReward, cardChoices, null);
    }

    public void Show(BattleManager bm, int goldReward, List<CardBase> cardChoices, List<RelicBase> relicChoices)
    {
        manager = bm;
        rewardStage = RewardStage.None;
        currentGoldReward = goldReward;
        pendingCardChoices = cardChoices != null && cardChoices.Count > 0
            ? new List<CardBase>(cardChoices)
            : null;
        pendingRelicChoices = relicChoices != null && relicChoices.Count > 0
            ? new List<RelicBase>(relicChoices)
            : null;
        selectedRelicChoiceView = null;
        stageSelectionCommitted = false;
        closeRequested = false;

        ResolveAllDeckCounterButton();
        WireAllDeckCounterButton();
        ResolvePackVisualReferences();
        CacheDefaultVisualState();
        RestoreGoldTextStyle();
        CacheSkipButtonVisuals();
        StopOpenPackFlow();
        ClearRewardEntries();

        gameObject.SetActive(true);
        StopSkipButtonReveal();
        HideSkipButton();
        SetSkipHoverVisible(false);
        SetSkipButtonUseConfirmSprite(false);
        SetGoldText($"\u7372\u5F97 {goldReward} \u91D1\u5E63");

        bool hasCardChoices = pendingCardChoices != null && pendingCardChoices.Count > 0;
        bool hasRelicChoices = pendingRelicChoices != null && pendingRelicChoices.Count > 0;

        if (hasRelicChoices)
        {
            ShowRelicRewardStage();
            return;
        }

        if (hasCardChoices)
        {
            ShowCardRewardStage(pendingCardChoices);
            return;
        }

        Close();
    }

    private void ShowCardRewardStage(List<CardBase> cardChoices)
    {
        rewardStage = RewardStage.CardReward;
        stageSelectionCommitted = false;
        StopOpenPackFlow();
        ClearRewardEntries();
        RestoreGoldTextStyle();
        SetGoldText($"\u7372\u5F97 {currentGoldReward} \u91D1\u5E63");
        PrepareIdleStage();
        ConfigureSkipButtonForCardStage();

        if (packButton != null)
        {
            packButton.onClick.RemoveAllListeners();
            packButton.onClick.AddListener(() => OnPackButtonClicked(cardChoices));
        }

        StartIdleVisual();
    }

    private void ShowRelicRewardStage()
    {
        rewardStage = RewardStage.RelicReward;
        stageSelectionCommitted = false;
        SetSkipHoverVisible(false);
        StopOpenPackFlow();
        ClearRewardEntries();
        PrepareRelicStage();
        ConfigureSkipButtonForRelicStage();
        DisplayRelicChoices(pendingRelicChoices);
    }

    private void ConfigureSkipButtonForCardStage()
    {
        if (skipButton == null)
        {
            return;
        }

        skipButton.onClick.RemoveAllListeners();
        skipButton.onClick.AddListener(AdvanceAfterCardStage);
        SetSkipButtonUseConfirmSprite(false);
    }

    private void ConfigureSkipButtonForRelicStage()
    {
        if (skipButton == null)
        {
            return;
        }

        skipButton.onClick.RemoveAllListeners();
        skipButton.onClick.AddListener(ConfirmSelectedRelic);
        SetSkipButtonUseConfirmSprite(true);
        ScheduleSkipButtonReveal(false);
    }

    private void AdvanceAfterCardStage()
    {
        if (rewardStage != RewardStage.CardReward)
        {
            return;
        }

        stageSelectionCommitted = true;
        pendingCardChoices = null;
        Close();
    }

    public void Close()
    {
        if (closeRequested)
        {
            return;
        }

        closeRequested = true;
        rewardStage = RewardStage.None;
        StopSkipButtonReveal();
        StopOpenPackFlow();

        if (skipButton != null)
        {
            skipButton.interactable = false;
        }

        if (packButton != null)
        {
            packButton.interactable = false;
        }

        SetRewardCardsInteractable(false);

        RunManager runManager = RunManager.Instance;
        if (runManager != null)
        {
            bool shouldShowRunEndSummary = runManager.RunCompleted;
            if (shouldShowRunEndSummary && manager != null)
            {
                manager.CaptureBattleEndSummary(true);
            }

            runManager.ReturnToRunSceneFromBattle(suppressTitleSummary: !shouldShowRunEndSummary);
            return;
        }

        ClearRewardEntries();
        gameObject.SetActive(false);
    }

    private void ResolveAllDeckCounterButton()
    {
        if (allDeckCounterButton != null)
        {
            return;
        }

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button != null && button.name == "AllDeck CounterButton")
            {
                allDeckCounterButton = button;
                return;
            }
        }
    }

    private void WireAllDeckCounterButton()
    {
        if (allDeckCounterButton == null)
        {
            return;
        }

        allDeckCounterButton.onClick.RemoveListener(OnRewardAllDeckCounterClicked);
        allDeckCounterButton.onClick.AddListener(OnRewardAllDeckCounterClicked);
    }

    private void OnRewardAllDeckCounterClicked()
    {
        UIManager uiManager = FindObjectOfType<UIManager>(true);
        if (uiManager != null)
        {
            uiManager.OnAllDeckCounterClicked();
            return;
        }

        DeckPanelController deckPanelController = FindObjectOfType<DeckPanelController>(true);
        deckPanelController?.OpenAllDeckPanel();
    }

}
