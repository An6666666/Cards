using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class EnemySpawnConfig
{
    public Enemy enemyPrefab;
    public int count = 1;
}

public partial class BattleManager : MonoBehaviour
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

    private bool battleStarted;
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

    private void Awake()
    {
        runManager = RunManager.Instance;
        ResolveTutorialController();

        EnsurePhaseHintText();
        HideCentralPhaseHintVisuals();
        HideTopPhaseIndicator();

        InitializeControllers();
        handUIController.SetEndTurnButtonInteractable(false);

        encounterLoader.LoadEncounterFromRunManager();
        ConfigureTutorialForActiveEncounter();
    }

    private void Start()
    {
        SubscribeRunSnapshot();
        RefreshRelicUI();
        StartCoroutine(encounterLoader.GameStartRoutine());
    }

    private void Update()
    {
        stateMachine.Update();

        if (!battleStarted)
        {
            return;
        }

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

    public bool OnEnemyClicked(Enemy enemy)
    {
        if (movementSelectionController != null && movementSelectionController.OnEnemyClicked(enemy))
        {
            return true;
        }

        return attackSelectionController.OnEnemyClicked(enemy);
    }

    public void UpdateAttackHover(Vector2 worldPosition)
    {
        attackSelectionController.UpdateAttackHover(worldPosition);
    }

    public void EndAttackSelect()
    {
        attackSelectionController.EndAttackSelect();
    }

    public void OnEnemyDefeated(Enemy enemy)
    {
        rewardController.OnEnemyDefeated(enemy);

        if (player != null && enemy != null && stateMachine.Current is PlayerTurnState)
        {
            player.NotifyEnemyDefeated(enemy);
        }
    }

    public void ShowVictoryRewards()
    {
        rewardController.ShowVictoryRewards();
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
}
