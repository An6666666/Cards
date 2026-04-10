using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class EnemySpawnConfig
{
    // 這一組設定要生成的敵人 Prefab。
    public Enemy enemyPrefab;
    // 生成數量。
    public int count = 1;
}

// 戰鬥主控制器：負責回合、選牌、結算與 UI 協調。
public class BattleManager : MonoBehaviour
{
    private enum BattlePhaseHintType
    {
        Unknown,
        PlayerTurn,
        EnemyTurn,
        SelectStartTile
    }

    [Header("Core References")]
    public Player player;
    [NonSerialized] public List<Enemy> enemies = new List<Enemy>();


    public Board board;

    [Header("Cards & UI")]
    public GameObject cardPrefab;
    public Transform handPanel;
    public Transform deckPile;
    public Transform discardPile;
    [Header("Relics UI")]
    [SerializeField] private GameObject relicUIPrefab;
    [SerializeField] private Transform relicUIParent;
    public Text energyText;
    [SerializeField] private Button endTurnButton;
    [Header("Battle Phase Hint")]
    [SerializeField] private Text phaseHintText;
    [SerializeField] private TMP_Text phaseHintTmpText;
    [SerializeField] private Image phaseHintImage;
    [SerializeField] private Image phaseTopIndicatorImage;
    [SerializeField] private Sprite playerTurnCenterSprite;
    [SerializeField] private Sprite enemyTurnCenterSprite;
    [SerializeField] private Sprite selectStartTileCenterSprite;
    [SerializeField] private Sprite playerTurnTopSprite;
    [SerializeField] private Sprite enemyTurnTopSprite;
    [SerializeField] private Sprite selectStartTileTopSprite;
    [Min(0f)] [SerializeField] private float phaseHintDuration = 1.2f;
    [Min(0f)] [SerializeField] private float phaseHintFadeInDuration = 0.2f;
    [Min(0f)] [SerializeField] private float phaseHintFadeOutDuration = 0.25f;


    [Header("Initial Setup")]
    public List<EnemySpawnConfig> enemySpawnConfigs = new List<EnemySpawnConfig>();


    public Vector2Int playerStartPos = Vector2Int.zero;

    [Header("Tutorial")]
    [SerializeField] private TutorialBattleController tutorialController;
    private readonly BattleStateMachine stateMachine = new BattleStateMachine();


    [Header("Guaranteed Cards")]
    public Move_YiDong guaranteedMovementCard;


    private Move_YiDong guaranteedMovementCardInstance;


    [Header("Rewards")]
    public List<CardBase> allCardPool = new List<CardBase>();


    public RewardUI rewardUIPrefab;

    [Header("Relic Reward")]
    [SerializeField, Range(0f, 1f)] private float normalBattleRelicRewardChance = 0.1f;
    [SerializeField, Range(0f, 1f)] private float eliteBattleRelicRewardChance = 1f;
    [SerializeField, Min(1)] private int normalBattleRelicChoiceCount = 3;

    [Header("Timings")]
    public float cardUseDelay = 0f;

    // 戰鬥是否已開始。
    private bool battleStarted = false;

    private BattleEncounterLoader encounterLoader;
    private BattleTurnController turnController;
    private BattleHandUIController handUIController;
    private MovementSelectionController movementSelectionController;


    private AttackSelectionController attackSelectionController;


    private BattleRewardController rewardController;
    private BattleRuntimeContext runtimeContext;
    private IEnemyQueryService enemyQueryService;
    private PlayerDeckController playerDeckController;
    private readonly List<GameObject> spawnedRelicUiObjects = new List<GameObject>();
    private RunManager runManager;
    private Coroutine phaseHintCoroutine;
    private CanvasGroup phaseHintLegacyTextCanvasGroup;
    private CanvasGroup phaseHintTmpTextCanvasGroup;
    private CanvasGroup phaseHintImageCanvasGroup;


    public bool BattleStarted => battleStarted;
    public float NormalBattleRelicRewardChance => Mathf.Clamp01(normalBattleRelicRewardChance);
    public float EliteBattleRelicRewardChance => Mathf.Clamp01(eliteBattleRelicRewardChance);
    public int NormalBattleRelicChoiceCount => Mathf.Max(1, normalBattleRelicChoiceCount);
    public GameObject RelicRewardIconPrefab => relicUIPrefab;


    public BattleStateMachine StateMachine => stateMachine;

    public TutorialBattleController TutorialController => tutorialController;
    public bool IsProcessingEnemyTurnStart => turnController != null && turnController.IsProcessingEnemyTurnStart;


    public bool IsCardInteractionLocked => handUIController != null && handUIController.IsCardInteractionLocked;
    public BattleRuntimeContext RuntimeContext => runtimeContext;
    public IEnemyQueryService EnemyQueryService => enemyQueryService;


    void Awake()
    {
        runManager = RunManager.Instance;
        ResolveTutorialController();

        EnsurePhaseHintText();
        HideCentralPhaseHintVisuals();
        HideTopPhaseIndicator();

        // 建立所有子控制器，並先關閉「結束回合」按鈕。
        InitializeControllers();
        handUIController.SetEndTurnButtonInteractable(false);

        // 由 RunManager 載入本場戰鬥與教學設定。
        encounterLoader.LoadEncounterFromRunManager();
        ConfigureTutorialForActiveEncounter();
    }
    private void ResolveTutorialController()
    {
        if (tutorialController != null)
        {
            return;
        }

        tutorialController = GetComponentInChildren<TutorialBattleController>(true);
        if (tutorialController == null)
        {
            tutorialController = FindObjectOfType<TutorialBattleController>();
        }
    }
    private void ConfigureTutorialForActiveEncounter()
    {
        ResolveTutorialController();

        if (tutorialController == null)
        {
            return;
        }


        RunEncounterDefinition encounter = RunManager.Instance?.ActiveNode?.Encounter;
        bool enableTutorial = encounter != null && encounter.UseTutorialBattle;
        TutorialBattleDefinition definition = enableTutorial ? encounter.TutorialBattleDefinition : null;

        tutorialController.ConfigureForBattle(enableTutorial, definition);

    }
    void Start()
    {
        SubscribeRunSnapshot();
        RefreshRelicUI();
        StartCoroutine(encounterLoader.GameStartRoutine());

    }

    void Update()
    {
        // 每幀更新狀態機。
        stateMachine.Update();

        if (!battleStarted) return;

        // 空白鍵快捷結束回合（僅在按鈕可互動時生效）。
        if (Input.GetKeyDown(KeyCode.Space) && endTurnButton != null && endTurnButton.interactable)
        {
            EndPlayerTurn();
            return;
        }

        enemies.RemoveAll(e => e == null);


        bool allDead = enemies.Count == 0 || enemies.TrueForAll(e => e.currentHP <= 0);


        if (allDead && !(stateMachine.Current is VictoryState))
        {
            stateMachine.ChangeState(new VictoryState(this));

        }

        if (player.currentHP <= 0 && !(stateMachine.Current is DefeatState))
        {
            stateMachine.ChangeState(new DefeatState(this));

        }
    }

    public void StartPlayerTurn()
    {
        turnController.StartPlayerTurn();
    }

    public void EndPlayerTurn()
    {
        turnController.EndPlayerTurn();
    }

    public IEnumerator EnemyTurnCoroutine()
    {
        return turnController.EnemyTurnCoroutine();
    }

    public void UseMovementCard(CardBase movementCard)
    {
        movementSelectionController.UseMovementCard(movementCard);

    }

    public void CancelMovementSelection()
    {
        movementSelectionController.CancelMovementSelection();

    }
    
    public bool OnTileClicked(BoardTile tile)
    {
        return movementSelectionController.OnTileClicked(tile);

    }

    public void StartAttackSelect(CardBase attackCard)
    {
        if (!TryCreateAttackSelectionRequest(attackCard, out AttackSelectionRequest request))
        {
            return;
        }

        StartAttackSelect(request);
    }

    public void StartAttackSelect(AttackSelectionRequest request)
    {
        attackSelectionController.StartAttackSelect(request);
    }

    public bool OnEnemyClicked(Enemy e)
    {
        if (movementSelectionController != null && movementSelectionController.OnEnemyClicked(e))
        {
            return true;
        }

        return attackSelectionController.OnEnemyClicked(e);

    }

    public void UpdateAttackHover(Vector2 worldPosition)
    {
        attackSelectionController.UpdateAttackHover(worldPosition);

    }

    public void EndAttackSelect()
    {
        attackSelectionController.EndAttackSelect();

    }

    private bool TryCreateAttackSelectionRequest(CardBase attackCard, out AttackSelectionRequest request)
    {
        request = default;

        if (attackCard == null || player == null || player.Hand == null)
        {
            return false;
        }

        if (!player.Hand.Contains(attackCard))
        {
            return false;
        }

        int finalCost = CalculateCardEnergyCost(attackCard);
        if (player.energy < finalCost)
        {
            Debug.Log("Not enough energy");
            return false;
        }

        request = new AttackSelectionRequest(attackCard, finalCost, Time.unscaledTime);
        return true;
    }

    public int CalculateCardEnergyCost(CardBase cardData)
    {
        if (cardData == null || player == null)
        {
            return 0;
        }

        int finalCost = cardData.cost + player.GetCardCostModifier(cardData);

        if (cardData.cardType == CardType.Attack)
        {
            finalCost += player.buffs.nextAttackCostModify;
        }

        if (cardData.cardType == CardType.Movement)
        {
            finalCost += player.buffs.movementCostModify;
        }

        return Mathf.Max(0, finalCost);
    }

    public void OnEnemyDefeated(Enemy e)
    {
        rewardController.OnEnemyDefeated(e);

        if (player != null && e != null && stateMachine.Current is PlayerTurnState)
        {
            player.NotifyEnemyDefeated(e);
        }

    }

    public void ShowVictoryRewards()
    {
        rewardController.ShowVictoryRewards();

    }




    public bool PlayCard(CardBase cardData)
    {
        // 只允許在玩家回合打牌。
        if (!(stateMachine.Current is PlayerTurnState)) return false;


        if (cardData == null) return false;
        if (player == null || player.Hand == null) return false;


        if (!player.Hand.Contains(cardData)) return false;


        int finalCost = CalculateCardEnergyCost(cardData);

        if (player.energy < finalCost)
        {
            Debug.Log("Not enough energy");
            return false;
        }

        if (cardData is Skill_ZhiJiao && !player.HasExhaustableCardInHand(cardData))
        {
            Debug.Log("No exhaustable cards in hand for ?脩?");
            return false;
        }

        Enemy target = enemies.Find(e => e != null && e.currentHP > 0);

        if (target != null)
        {
            FaceUtils.Face(player.gameObject, target.transform);
        }
        List<ElementType> targetElementsBefore = null;
        if (target != null)
        {
            targetElementsBefore = new List<ElementType>(target.GetElementTags());
        }

        PlayNonAttackCardAnimation(cardData);
        player.NotifyCardPlayStarted(cardData);
        cardData.ExecuteEffect(player, target);

        List<ElementType> targetElementsAfter = null;
        if (target != null)
        {
            targetElementsAfter = new List<ElementType>(target.GetElementTags());
        }
        if (cardData.cardType == CardType.Attack && player.buffs.nextAttackPlus > 0)
        {
            player.buffs.nextAttackPlus = 0;
        }


        bool isGuaranteedMovement = IsGuaranteedMovementCard(cardData);


        bool removedFromHand = player.Hand.Remove(cardData);


        if (removedFromHand)
        {
            player.ClearCardCostModifier(cardData);


            if (isGuaranteedMovement)
            {
                RemoveGuaranteedMovementCardFromPiles();

            }
            else if (cardData.exhaustOnUse)
            {
                player.ExhaustCard(cardData);

            }
            else
            {
                player.discardPile.Add(cardData);

            }
        }
        else if (isGuaranteedMovement)
        {
            RemoveGuaranteedMovementCardFromPiles();

        }

        player.UseEnergy(finalCost);
        GameEvents.RaiseCardPlayed(cardData);
        GameEvents.RaiseCardPlayedWithContext(
        new CardPlayContext(cardData, target, targetElementsBefore, targetElementsAfter));
        player.NotifyCardPlayed(cardData);
        if (removedFromHand)
        handUIController.RefreshHandUI(false);
        return true;
    }

    private void PlayNonAttackCardAnimation(CardBase cardData)
    {
        if (player == null || cardData == null || cardData.cardType == CardType.Attack)
        {
            return;
        }

        if (IsDefendCard(cardData))
        {
            player.PlayDefendAnim();
            return;
        }

        if (IsUtilityCard(cardData))
        {
            player.PlayUtilityAnim();
        }
    }

    private static bool IsDefendCard(CardBase cardData)
    {
        return cardData is Skill_HuWoZhenShen
            || cardData is Skill_ShenLingBiYou
            || cardData is Skill_QimenDunjia
            || cardData is Skill_JinShen
            || cardData is Skill_BuMieYiZhi
            || cardData is Skill_WeiLingZhou;
    }

    private static bool IsUtilityCard(CardBase cardData)
    {
        return cardData is Skill_ZhiJiao
            || cardData is Skill_ZhaoHun
            || cardData is Skill_ShenLin
            || cardData is Skill_LingHunZhenDang;
    }
    public void HandleCardUsedUI(CardUI usedUI)
    {
        handUIController.HandleCardUsedUI(usedUI);
    }
    public void RefreshHandUI(bool playDrawAnimation = false)
    {
        handUIController.RefreshHandUI(playDrawAnimation);

    }

    public void RefreshHandMetaUI()
    {
        handUIController?.UpdateHandMetaUI();
    }

    public void ShowBattlePhaseHint(string message, float duration = -1f, bool showCentralHint = true)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        BattlePhaseHintType hintType = ResolveBattlePhaseHintType(message);
        UpdateTopPhaseIndicator(hintType);

        if (phaseHintCoroutine != null)
        {
            StopCoroutine(phaseHintCoroutine);
            phaseHintCoroutine = null;
        }

        if (!showCentralHint)
        {
            HideCentralPhaseHintVisuals();
            return;
        }

        EnsurePhaseHintText();
        if (phaseHintText == null && phaseHintTmpText == null && phaseHintImage == null)
        {
            return;
        }

        float showDuration = duration > 0f ? duration : phaseHintDuration;
        phaseHintCoroutine = StartCoroutine(ShowBattlePhaseHintRoutine(message, showDuration, hintType));
    }

    public IEnumerator ShowBattlePhaseHintAndWait(string message, float duration = -1f, bool showCentralHint = true)
    {
        ShowBattlePhaseHint(message, duration, showCentralHint);

        if (!showCentralHint)
        {
            yield break;
        }

        float holdDuration = duration > 0f ? duration : phaseHintDuration;
        float waitDuration = Mathf.Max(0f, holdDuration) + Mathf.Max(0f, phaseHintFadeInDuration) + Mathf.Max(0f, phaseHintFadeOutDuration);
        waitDuration = Mathf.Max(0f, waitDuration);
        if (waitDuration > 0f)
        {
            yield return new WaitForSeconds(waitDuration);
        }
    }

    private IEnumerator ShowBattlePhaseHintRoutine(string message, float duration, BattlePhaseHintType hintType)
    {
        if (!TryPrepareCentralPhaseHintVisual(hintType, message, out CanvasGroup activeCanvasGroup, out GameObject activeObject))
        {
            yield break;
        }

        float fadeIn = Mathf.Max(0f, phaseHintFadeInDuration);
        float fadeOut = Mathf.Max(0f, phaseHintFadeOutDuration);
        float hold = Mathf.Max(0f, duration);

        HideCentralPhaseHintVisuals();
        activeObject.SetActive(true);
        SetCentralPhaseHintAlpha(activeCanvasGroup, 0f);

        if (fadeIn > 0f)
        {
            yield return FadePhaseHintAlpha(activeCanvasGroup, 0f, 1f, fadeIn);
        }
        else
        {
            SetCentralPhaseHintAlpha(activeCanvasGroup, 1f);
        }

        if (hold > 0f)
        {
            yield return new WaitForSeconds(hold);
        }

        if (fadeOut > 0f)
        {
            yield return FadePhaseHintAlpha(activeCanvasGroup, 1f, 0f, fadeOut);
        }
        else
        {
            SetCentralPhaseHintAlpha(activeCanvasGroup, 0f);
        }

        HideCentralPhaseHintVisuals();

        phaseHintCoroutine = null;
    }

    internal void EnsureMovementCardInHand()
    {
        if (player == null) return;

        Move_YiDong movementCard = GetGuaranteedMovementCardInstance();


        if (movementCard == null) return;

        RemoveGuaranteedMovementCardFromPiles();


        int removedDuplicateCount = 0;
        for (int i = player.Hand.Count - 1; i >= 0; i--)
        {
            CardBase card = player.Hand[i];
            if (card is Move_YiDong && !ReferenceEquals(card, movementCard))
            {
                player.Hand.RemoveAt(i);
                removedDuplicateCount++;
            }
        }

        if (!player.Hand.Contains(movementCard))
        {
            player.Hand.Add(movementCard);
        }

        if (removedDuplicateCount > 0)
        {
            player.DrawCards(removedDuplicateCount);

        }
    }

    internal void DiscardAllHand()
    {
        Move_YiDong movementCard = guaranteedMovementCardInstance;


        if (movementCard != null)
        {
            player.Hand.Remove(movementCard);


            player.discardPile.Remove(movementCard);

        }

        player.discardPile.AddRange(player.Hand);


        player.Hand.Clear();


        RemoveGuaranteedMovementCardFromPiles();


        handUIController.RefreshHandUI();

    }

    internal void SetBattleStarted(bool value)
    {
        battleStarted = value;
    }

    internal void SetEndTurnButtonInteractable(bool value)
    {
        handUIController.SetEndTurnButtonInteractable(value);

    }
    public void RefreshEnemiesFromScene()
    {
        Enemy.FillActiveEnemies(enemies);
    }
    internal bool IsGuaranteedMovementCard(CardBase card)
    {
        if (card == null)
            return false;

        Move_YiDong instance = GetGuaranteedMovementCardInstance();
        if (instance == null)
            return false;

        if (ReferenceEquals(card, instance))
            return true;

        if (guaranteedMovementCard != null && ReferenceEquals(card, guaranteedMovementCard))
            return true;

        return false;
    }

    internal void RemoveGuaranteedMovementCardFromPiles()
    {
        if (player == null) return;

        player.deck.RemoveAll(card => card is Move_YiDong);


        player.discardPile.RemoveAll(card => card is Move_YiDong);

    }

    private Move_YiDong GetGuaranteedMovementCardInstance()
    {
        if (guaranteedMovementCardInstance == null)
        {
            if (guaranteedMovementCard == null)
            {
                Debug.LogWarning("Guaranteed movement card template is not assigned.");

                return null;
            }

            guaranteedMovementCardInstance = Instantiate(guaranteedMovementCard);

        }

        return guaranteedMovementCardInstance;
    }

    private void InitializeControllers()
    {
        // 建立執行期上下文與查詢服務。
        runtimeContext = new BattleRuntimeContext(this, player, board, enemies);
        runtimeContext.Activate();
        enemyQueryService = new BattleEnemyQueryService(runtimeContext);

        handUIController = new BattleHandUIController(
            this,
            player,
            cardPrefab,
            handPanel,
            deckPile,
            discardPile,
            energyText,
            endTurnButton,
            cardUseDelay);

        movementSelectionController = new MovementSelectionController(
            this,
            player,
            board,
            handUIController);

        attackSelectionController = new AttackSelectionController(
            player,
            board,
            handUIController,
            this,
            enemyQueryService
            );

        rewardController = new BattleRewardController(
            this,
            player,
            allCardPool,
            rewardUIPrefab,
            handPanel,
            normalBattleRelicRewardChance,
            eliteBattleRelicRewardChance,
            normalBattleRelicChoiceCount);

        encounterLoader = new BattleEncounterLoader(
            this,
            board,
            player,
            enemies,
            enemySpawnConfigs,
            stateMachine,
            tutorialController);

        movementSelectionController.SetEncounterLoader(encounterLoader);


        turnController = new BattleTurnController(
            this,
            player,
            enemies,
            stateMachine,
            handUIController,
            tutorialController);

        PlayerDeckController deckController = player != null ? player.GetComponent<PlayerDeckController>() : null;
        playerDeckController = deckController;
        if (playerDeckController != null)
        {
            playerDeckController.ConfigureBattleRuntime(this, runtimeContext);
            playerDeckController.HandChanged -= HandlePlayerHandChanged;
            playerDeckController.HandChanged += HandlePlayerHandChanged;
            playerDeckController.DeckChanged -= HandlePlayerDeckChanged;
            playerDeckController.DeckChanged += HandlePlayerDeckChanged;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeRunSnapshot();

        // 解除事件綁定與 runtime context，避免場景切換後殘留參考。
        runtimeContext?.DeactivateIfOwner(this);

        if (player != null)
        {
            if (playerDeckController == null)
            {
                playerDeckController = player.GetComponent<PlayerDeckController>();
            }

            if (playerDeckController != null)
            {
                playerDeckController.HandChanged -= HandlePlayerHandChanged;
                playerDeckController.DeckChanged -= HandlePlayerDeckChanged;
                playerDeckController.ClearBattleRuntime(this);
            }
        }
    }

    public void RefreshRelicUI()
    {
        ClearSpawnedRelicUI();

        if (relicUIPrefab == null || relicUIParent == null)
            return;

        IReadOnlyList<RelicBase> relics = ResolveBattleRelics();
        if (relics == null)
            return;

        for (int i = 0; i < relics.Count; i++)
        {
            RelicBase relic = relics[i];
            if (relic == null)
                continue;

            GameObject relicUiObject = Instantiate(relicUIPrefab, relicUIParent, false);
            ApplyRelicUIData(relicUiObject, relic);
            spawnedRelicUiObjects.Add(relicUiObject);
        }
    }

    private void ApplyRelicUIData(GameObject relicUiObject, RelicBase relic)
    {
        if (relicUiObject == null)
            return;

        BattleRelicUIItem itemView = relicUiObject.GetComponent<BattleRelicUIItem>() ??
                                     relicUiObject.GetComponentInChildren<BattleRelicUIItem>(true);
        if (itemView != null)
        {
            itemView.Bind(relic);
            return;
        }

        itemView = relicUiObject.AddComponent<BattleRelicUIItem>();
        itemView.Bind(relic);
    }

    private IReadOnlyList<RelicBase> ResolveBattleRelics()
    {
        if (player != null && player.relics != null)
            return player.relics;

        return runManager?.CurrentRunSnapshot?.relics;
    }

    private void ClearSpawnedRelicUI()
    {
        for (int i = 0; i < spawnedRelicUiObjects.Count; i++)
        {
            GameObject relicUiObject = spawnedRelicUiObjects[i];
            if (relicUiObject != null)
                Destroy(relicUiObject);
        }

        spawnedRelicUiObjects.Clear();
    }

    private void SubscribeRunSnapshot()
    {
        if (runManager == null)
            runManager = RunManager.Instance;

        if (runManager == null)
            return;

        runManager.RunSnapshotChanged -= HandleRunSnapshotChanged;
        runManager.RunSnapshotChanged += HandleRunSnapshotChanged;
    }

    private void UnsubscribeRunSnapshot()
    {
        if (runManager == null)
            return;

        runManager.RunSnapshotChanged -= HandleRunSnapshotChanged;
    }

    private void HandleRunSnapshotChanged(PlayerRunSnapshot snapshot)
    {
        RefreshRelicUI();
    }

    private void EnsurePhaseHintText()
    {
        if (phaseHintText != null || phaseHintTmpText != null)
        {
            EnsurePhaseHintCanvasGroups();
            return;
        }

        Text[] allTexts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < allTexts.Length; i++)
        {
            Text candidate = allTexts[i];
            if (candidate == null)
            {
                continue;
            }

            string n = candidate.gameObject.name;
            if (n.Contains("BattlePhaseHint") || n.Contains("PhaseHint") || n.Contains("TurnHint"))
            {
                phaseHintText = candidate;
                EnsurePhaseHintCanvasGroups();
                return;
            }
        }

        TMP_Text[] allTmpTexts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < allTmpTexts.Length; i++)
        {
            TMP_Text candidate = allTmpTexts[i];
            if (candidate == null)
            {
                continue;
            }

            string n = candidate.gameObject.name;
            if (n.Contains("BattlePhaseHint") || n.Contains("PhaseHint") || n.Contains("TurnHint"))
            {
                phaseHintTmpText = candidate;
                EnsurePhaseHintCanvasGroups();
                return;
            }
        }

        Canvas targetCanvas = GetComponentInChildren<Canvas>(true);
        if (targetCanvas == null)
        {
            targetCanvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        }
        if (targetCanvas == null)
        {
            return;
        }

        GameObject hintObj = new GameObject("BattlePhaseHintText", typeof(RectTransform), typeof(Text));
        RectTransform rect = hintObj.GetComponent<RectTransform>();
        rect.SetParent(targetCanvas.transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.88f);
        rect.anchorMax = new Vector2(0.5f, 0.88f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(800f, 90f);
        rect.anchoredPosition = Vector2.zero;

        Text text = hintObj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 42;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 1f, 1f, 1f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = string.Empty;

        phaseHintText = text;
        EnsurePhaseHintCanvasGroups();
    }

    private void EnsurePhaseHintCanvasGroups()
    {
        if (phaseHintText != null && phaseHintLegacyTextCanvasGroup == null)
        {
            phaseHintLegacyTextCanvasGroup = phaseHintText.GetComponent<CanvasGroup>();
            if (phaseHintLegacyTextCanvasGroup == null)
            {
                phaseHintLegacyTextCanvasGroup = phaseHintText.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (phaseHintTmpText != null && phaseHintTmpTextCanvasGroup == null)
        {
            phaseHintTmpTextCanvasGroup = phaseHintTmpText.GetComponent<CanvasGroup>();
            if (phaseHintTmpTextCanvasGroup == null)
            {
                phaseHintTmpTextCanvasGroup = phaseHintTmpText.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (phaseHintImage != null && phaseHintImageCanvasGroup == null)
        {
            phaseHintImageCanvasGroup = phaseHintImage.GetComponent<CanvasGroup>();
            if (phaseHintImageCanvasGroup == null)
            {
                phaseHintImageCanvasGroup = phaseHintImage.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (phaseHintLegacyTextCanvasGroup != null)
        {
            phaseHintLegacyTextCanvasGroup.interactable = false;
            phaseHintLegacyTextCanvasGroup.blocksRaycasts = false;
        }

        if (phaseHintTmpTextCanvasGroup != null)
        {
            phaseHintTmpTextCanvasGroup.interactable = false;
            phaseHintTmpTextCanvasGroup.blocksRaycasts = false;
        }

        if (phaseHintImageCanvasGroup != null)
        {
            phaseHintImageCanvasGroup.interactable = false;
            phaseHintImageCanvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator FadePhaseHintAlpha(CanvasGroup canvasGroup, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetCentralPhaseHintAlpha(canvasGroup, to);
            yield break;
        }

        float elapsed = 0f;
        SetCentralPhaseHintAlpha(canvasGroup, from);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetCentralPhaseHintAlpha(canvasGroup, Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetCentralPhaseHintAlpha(canvasGroup, to);
    }

    private void SetCentralPhaseHintAlpha(CanvasGroup canvasGroup, float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
            return;
        }

        if (phaseHintTmpText != null)
        {
            Color tmpColor = phaseHintTmpText.color;
            tmpColor.a = Mathf.Clamp01(alpha);
            phaseHintTmpText.color = tmpColor;
            return;
        }

        if (phaseHintText == null)
        {
            return;
        }

        Color c = phaseHintText.color;
        c.a = Mathf.Clamp01(alpha);
        phaseHintText.color = c;
    }

    private bool TryPrepareCentralPhaseHintVisual(BattlePhaseHintType hintType, string message, out CanvasGroup activeCanvasGroup, out GameObject activeObject)
    {
        EnsurePhaseHintText();
        EnsurePhaseHintCanvasGroups();

        if (TryGetCenterHintSprite(hintType, out Sprite sprite) && phaseHintImage != null)
        {
            phaseHintImage.sprite = sprite;
            activeCanvasGroup = phaseHintImageCanvasGroup;
            activeObject = phaseHintImage.gameObject;
            return true;
        }

        if (phaseHintTmpText != null)
        {
            phaseHintTmpText.text = message;
            activeCanvasGroup = phaseHintTmpTextCanvasGroup;
            activeObject = phaseHintTmpText.gameObject;
            return true;
        }

        if (phaseHintText != null)
        {
            phaseHintText.text = message;
            activeCanvasGroup = phaseHintLegacyTextCanvasGroup;
            activeObject = phaseHintText.gameObject;
            return true;
        }

        activeCanvasGroup = null;
        activeObject = null;
        return false;
    }

    private void HideCentralPhaseHintVisuals()
    {
        if (phaseHintText != null)
        {
            phaseHintText.gameObject.SetActive(false);
        }

        if (phaseHintTmpText != null)
        {
            phaseHintTmpText.gameObject.SetActive(false);
        }

        if (phaseHintImage != null)
        {
            phaseHintImage.gameObject.SetActive(false);
        }
    }

    private BattlePhaseHintType ResolveBattlePhaseHintType(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return BattlePhaseHintType.Unknown;
        }

        if (message.Contains("玩家回合"))
        {
            return BattlePhaseHintType.PlayerTurn;
        }

        if (message.Contains("妖怪回合") || message.Contains("敵人回合"))
        {
            return BattlePhaseHintType.EnemyTurn;
        }

        if (message.Contains("選擇落點回合"))
        {
            return BattlePhaseHintType.SelectStartTile;
        }

        return BattlePhaseHintType.Unknown;
    }

    private bool TryGetCenterHintSprite(BattlePhaseHintType hintType, out Sprite sprite)
    {
        sprite = null;

        switch (hintType)
        {
            case BattlePhaseHintType.PlayerTurn:
                sprite = playerTurnCenterSprite;
                break;
            case BattlePhaseHintType.EnemyTurn:
                sprite = enemyTurnCenterSprite;
                break;
            case BattlePhaseHintType.SelectStartTile:
                sprite = selectStartTileCenterSprite;
                break;
        }

        return sprite != null;
    }

    private void UpdateTopPhaseIndicator(BattlePhaseHintType hintType)
    {
        if (phaseTopIndicatorImage == null)
        {
            return;
        }

        switch (hintType)
        {
            case BattlePhaseHintType.PlayerTurn:
                if (playerTurnTopSprite != null)
                {
                    phaseTopIndicatorImage.sprite = playerTurnTopSprite;
                    phaseTopIndicatorImage.gameObject.SetActive(true);
                }
                else
                {
                    phaseTopIndicatorImage.gameObject.SetActive(false);
                }
                break;
            case BattlePhaseHintType.EnemyTurn:
                if (enemyTurnTopSprite != null)
                {
                    phaseTopIndicatorImage.sprite = enemyTurnTopSprite;
                    phaseTopIndicatorImage.gameObject.SetActive(true);
                }
                else
                {
                    phaseTopIndicatorImage.gameObject.SetActive(false);
                }
                break;
            case BattlePhaseHintType.SelectStartTile:
                if (selectStartTileTopSprite != null)
                {
                    phaseTopIndicatorImage.sprite = selectStartTileTopSprite;
                    phaseTopIndicatorImage.gameObject.SetActive(true);
                }
                else
                {
                    phaseTopIndicatorImage.gameObject.SetActive(false);
                }
                break;
        }
    }

    private void HideTopPhaseIndicator()
    {
        if (phaseTopIndicatorImage != null)
        {
            phaseTopIndicatorImage.gameObject.SetActive(false);
        }
    }

    private void HandlePlayerHandChanged()
    {
        handUIController?.RefreshHandUI();
    }

    private void HandlePlayerDeckChanged()
    {
        handUIController?.UpdateDeckDiscardUI();
    }
}


