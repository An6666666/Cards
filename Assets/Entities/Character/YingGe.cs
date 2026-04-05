using System.Collections.Generic;
using UnityEngine;

public class YingGe : Enemy, IEnemyCooldownProvider
{
    private static readonly Vector2Int OffBoardSentinel = new Vector2Int(int.MinValue / 2, int.MinValue / 2);
    private static readonly Vector2Int[] PhaseTwoMeleeAttackOffsets =
    {
        new Vector2Int(-2, 0),
        new Vector2Int(2, 0),
        new Vector2Int(-1, -2),
        new Vector2Int(1, -2),
        new Vector2Int(-1, 2),
        new Vector2Int(1, 2)
    };

    public override bool ShouldResetBlockEachTurn => false;

    [Header("Ying Ge Abilities")]
    [SerializeField] private int armorPerTurn = 4;
    [SerializeField] private int miasmaDamage = 5;
    [SerializeField] private int miasmaCenters = 2;
    [SerializeField] private int stoneFeatherDamage = 15;
    [SerializeField] private int stoneFeatherCooldown = 4;

    [Header("Resurrection Stone Settings")]
    [SerializeField] private YingGeStone stonePrefab;
    [SerializeField] private int stoneHealth = 100;
    [SerializeField] private int stoneRespawnWaitTurns = 2;

    [Header("Phase Two")]
    [SerializeField] private int phaseTwoHealth = 60;
    [SerializeField] private int phaseTwoBaseAttackDamage = 10;
    [SerializeField] private int phaseTwoArmorPerTurn = 2;
    [SerializeField] private bool allowStoneFeatherInPhaseTwo = false;
    [TextArea(2, 10)]
    [SerializeField] private string phaseTwoSkillDescription;
    [SerializeField] private List<EnemySpawnConfig> phaseTwoSummons = new List<EnemySpawnConfig>();

    private readonly HashSet<Vector2Int> miasmaTiles = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> stoneFeatherTargets = new HashSet<Vector2Int>();
    private readonly List<BoardTile> stoneFeatherHighlightedTiles = new List<BoardTile>();
    private readonly List<Enemy> phaseTwoSummonedEnemies = new List<Enemy>();

    private YingGeStone activeStone;
    private BattleManager battleManager;
    private SpriteRenderer[] cachedRenderers;
    private EnemyElementStatusDisplay elementStatusDisplay;

    private bool resurrectionTriggered;
    private bool awaitingRespawn;
    private bool hasRespawned;
    private bool finalDeathHandled;
    private bool stoneRespawnCompleted;
    private bool stoneFeatherPending;
    private bool waitingForStoneFeatherTakeOff;
    private bool inPhaseTwo;
    private bool phaseTwoSummonPending;

    private int stoneFeatherCooldownTimer;
    private int currentArmorPerTurn;
    private Vector2Int storedGridBeforeHide;

    public bool IsAwaitingRespawn => awaitingRespawn;

    public void PlaySkillStart() => Visual?.PlaySkillStart();

    public void PlaySkillEnd() => Visual?.PlaySkillEnd();

    public override bool SupportsSharedSquadTactics => false;

    protected override void Awake()
    {
        enemyName = "鸚哥";
        isBoss = true;
        battleManager = FindObjectOfType<BattleManager>();
        currentArmorPerTurn = armorPerTurn;
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        ApplyInitialMiasma();
    }

    public override void ProcessTurnStart()
    {
        base.ProcessTurnStart();
    }

    public override void ProcessEnemyTurnEnd()
    {
        base.ProcessEnemyTurnEnd();
        AdvanceStoneFeatherCooldown();
    }

    public override void EnemyAction(Player player)
    {
        if (awaitingRespawn)
        {
            return;
        }

        if (HandleCrowdControl())
        {
            GainEndOfTurnArmor();
            return;
        }

        if (waitingForStoneFeatherTakeOff)
        {
            GainEndOfTurnArmor();
            return;
        }

        if (stoneFeatherPending)
        {
            ResolveStoneFeather(player);
            GainEndOfTurnArmor();
            return;
        }

        if (phaseTwoSummonPending)
        {
            SummonPhaseTwoEnemies();
            phaseTwoSummonPending = false;
            GainEndOfTurnArmor();
            return;
        }

        if (CanUseStoneFeatherInCurrentPhase()
            && player != null
            && stoneFeatherCooldownTimer >= stoneFeatherCooldown
            && TryActivateStoneFeather(player))
        {
            GainEndOfTurnArmor();
            return;
        }

        base.EnemyAction(player);
        GainEndOfTurnArmor();
    }

    public override void DecideNextIntent(Player player)
    {
        if (awaitingRespawn)
        {
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        if (player == null || frozenTurns > 0)
        {
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        if (stoneFeatherPending || waitingForStoneFeatherTakeOff)
        {
            nextIntent.type = EnemyIntentType.Skill;
            nextIntent.value = stoneFeatherDamage;
            UpdateIntentIcon();
            return;
        }

        if (phaseTwoSummonPending)
        {
            nextIntent.type = EnemyIntentType.Skill;
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        if (CanUseStoneFeatherInCurrentPhase()
            && stoneFeatherCooldownTimer >= stoneFeatherCooldown
            && CanPreviewStoneFeather(player))
        {
            nextIntent.type = EnemyIntentType.Skill;
            nextIntent.value = stoneFeatherDamage;
            UpdateIntentIcon();
            return;
        }

        if (IsPlayerInRange(player))
        {
            nextIntent.type = EnemyIntentType.Attack;
            nextIntent.value = CalculateAttackDamage();
        }
        else if (CanMoveThisTurn())
        {
            nextIntent.type = EnemyIntentType.Move;
            nextIntent.value = 0;
        }
        else
        {
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
        }

        UpdateIntentIcon();
    }

    protected internal override void Die()
    {
        if (!resurrectionTriggered && !hasRespawned && TryHandleFirstDeath())
        {
            return;
        }

        FinalizeDeath();
    }

    private bool HandleCrowdControl()
    {
        if (frozenTurns <= 0)
        {
            return false;
        }

        SetFrozenTurns(Mathf.Max(0, frozenTurns - 1));
        return true;
    }

    private void GainEndOfTurnArmor()
    {
        if (currentArmorPerTurn <= 0)
        {
            return;
        }

        block = Mathf.Max(0, block + currentArmorPerTurn);
        RaiseStatusChanged();
    }

    public int CooldownSlotCount => CanUseStoneFeatherInCurrentPhase() ? 1 : 0;

    public int GetCooldownTurnsRemaining(int slotIndex)
    {
        if (!CanUseStoneFeatherInCurrentPhase() || slotIndex != 0)
        {
            return 0;
        }

        if (stoneFeatherPending || waitingForStoneFeatherTakeOff)
        {
            return 0;
        }

        int remaining = stoneFeatherCooldown - stoneFeatherCooldownTimer;
        return Mathf.Max(0, remaining);
    }

    private bool CanUseStoneFeatherInCurrentPhase()
    {
        return !inPhaseTwo || allowStoneFeatherInPhaseTwo;
    }

    private bool CanPreviewStoneFeather(Player player)
    {
        if (player == null)
        {
            return false;
        }

        Board board = FindObjectOfType<Board>();
        if (board == null)
        {
            return false;
        }

        Vector2Int playerPos = player.position;
        int[] rowCandidates = { playerPos.y, playerPos.y + 2, playerPos.y - 2 };
        List<Vector2Int> allPositions = board.GetAllPositions();

        foreach (Vector2Int pos in allPositions)
        {
            for (int i = 0; i < rowCandidates.Length; i++)
            {
                if (pos.y == rowCandidates[i])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void AdvanceStoneFeatherCooldown()
    {
        if (!CanUseStoneFeatherInCurrentPhase())
        {
            return;
        }

        if (stoneFeatherPending || waitingForStoneFeatherTakeOff)
        {
            return;
        }

        if (stoneFeatherCooldownTimer < stoneFeatherCooldown)
        {
            stoneFeatherCooldownTimer++;
        }
    }

    private bool TryActivateStoneFeather(Player player)
    {
        if (stoneFeatherPending || waitingForStoneFeatherTakeOff)
        {
            return false;
        }

        Board board = FindObjectOfType<Board>();
        if (board == null)
        {
            return false;
        }

        stoneFeatherTargets.Clear();
        ClearStoneFeatherIndicators();

        Vector2Int playerPos = player.position;
        int[] rowCandidates = { playerPos.y, playerPos.y + 2, playerPos.y - 2 };

        foreach (int row in rowCandidates)
        {
            HighlightRow(board, row);
        }

        if (stoneFeatherTargets.Count == 0)
        {
            return false;
        }

        waitingForStoneFeatherTakeOff = true;
        PlaySkillStart();
        return true;
    }

    public void OnStoneFeatherTakeOffEvent()
    {
        if (!waitingForStoneFeatherTakeOff)
        {
            return;
        }

        waitingForStoneFeatherTakeOff = false;
        BeginStoneFeatherTakeOff();
    }

    private void BeginStoneFeatherTakeOff()
    {
        if (stoneFeatherPending || waitingForStoneFeatherTakeOff)
        {
            return;
        }

        stoneFeatherPending = true;
        stoneFeatherCooldownTimer = 0;
        storedGridBeforeHide = gridPosition;
        SetHidden(true);
        SetHighlight(false);
        SetForceHideIntent(true);
        gridPosition = OffBoardSentinel;

        if (battleManager == null)
        {
            battleManager = FindObjectOfType<BattleManager>();
        }
    }

    private void ResolveStoneFeather(Player player)
    {
        if (player != null && stoneFeatherTargets.Contains(player.position))
        {
            player.TakeDamage(stoneFeatherDamage);
        }

        stoneFeatherPending = false;
        ClearStoneFeatherIndicators();
        ReappearAfterStoneFeather();
    }

    private void HighlightRow(Board board, int row)
    {
        List<Vector2Int> allPositions = board.GetAllPositions();
        foreach (Vector2Int pos in allPositions)
        {
            if (pos.y != row)
            {
                continue;
            }

            BoardTile tile = board.GetTileAt(pos);
            if (tile == null)
            {
                continue;
            }

            if (!stoneFeatherTargets.Add(pos))
            {
                continue;
            }

            if (!stoneFeatherHighlightedTiles.Contains(tile))
            {
                tile.SetAttackHighlight(true);
                stoneFeatherHighlightedTiles.Add(tile);
            }
        }
    }

    private void ClearStoneFeatherIndicators()
    {
        foreach (BoardTile tile in stoneFeatherHighlightedTiles)
        {
            if (tile != null)
            {
                tile.SetAttackHighlight(false);
            }
        }

        stoneFeatherHighlightedTiles.Clear();
        stoneFeatherTargets.Clear();
    }

    private void ReappearAfterStoneFeather()
    {
        waitingForStoneFeatherTakeOff = false;

        if (battleManager == null)
        {
            battleManager = FindObjectOfType<BattleManager>();
        }

        Board board = FindObjectOfType<Board>();
        List<Vector2Int> availablePositions = board != null ? board.GetAllPositions() : new List<Vector2Int>();
        Player player = FindObjectOfType<Player>();
        if (player != null)
        {
            availablePositions.Remove(player.position);
        }

        Enemy[] allEnemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in allEnemies)
        {
            if (enemy != null && enemy != this)
            {
                availablePositions.Remove(enemy.gridPosition);
            }
        }

        Vector2Int targetPos = storedGridBeforeHide;
        if (availablePositions.Count > 0)
        {
            targetPos = availablePositions[Random.Range(0, availablePositions.Count)];
        }

        MoveToPosition(targetPos);
        SetHidden(false);
        PlaySkillEnd();
        SetForceHideIntent(false);

        if (battleManager != null && !battleManager.enemies.Contains(this))
        {
            battleManager.enemies.Add(this);
        }
    }

    private bool TryHandleFirstDeath()
    {
        if (awaitingRespawn || resurrectionTriggered)
        {
            return awaitingRespawn;
        }

        resurrectionTriggered = true;
        storedGridBeforeHide = gridPosition;

        Vector2Int stoneGrid = gridPosition;
        Vector3 stoneWorld = transform.position;

        gridPosition = OffBoardSentinel;
        SetHidden(true);
        SetHighlight(false);
        SetForceHideIntent(true);

        YingGeStone stone = CreateStoneInstance(stoneGrid, stoneWorld);
        if (stone == null)
        {
            gridPosition = storedGridBeforeHide;
            SetHidden(false);
            return false;
        }

        activeStone = stone;
        stoneRespawnCompleted = false;
        awaitingRespawn = true;

        if (battleManager == null)
        {
            battleManager = FindObjectOfType<BattleManager>();
        }

        if (battleManager != null)
        {
            battleManager.enemies.Remove(this);
            if (!battleManager.enemies.Contains(stone))
            {
                battleManager.enemies.Add(stone);
            }
        }

        return true;
    }

    private YingGeStone CreateStoneInstance(Vector2Int gridPos, Vector3 worldPos)
    {
        YingGeStone instance = null;
        if (stonePrefab != null)
        {
            instance = Instantiate(stonePrefab, worldPos, Quaternion.identity);
        }
        else
        {
            GameObject go = new GameObject("YingGeStone");
            go.transform.position = worldPos;
            instance = go.AddComponent<YingGeStone>();
        }

        if (instance == null)
        {
            return null;
        }

        instance.ConfigureFromOwner(this, gridPos, worldPos, stoneRespawnWaitTurns, stoneHealth);
        return instance;
    }

    public void HandleStoneReady(YingGeStone stone)
    {
        if (stone == null || stone != activeStone)
        {
            return;
        }

        stoneRespawnCompleted = true;
        awaitingRespawn = false;
        activeStone = null;

        Vector2Int respawnGrid = stone.gridPosition;
        Vector3 respawnWorld = stone.transform.position;

        stone.DetachOwner();
        if (battleManager != null)
        {
            battleManager.enemies.Remove(stone);
        }

        RespawnFromStone(respawnGrid, respawnWorld);
        Destroy(stone.gameObject);
    }

    public void OnStoneDestroyed(YingGeStone stone)
    {
        if (stone == null || stone != activeStone)
        {
            return;
        }

        activeStone = null;
        awaitingRespawn = false;

        if (stoneRespawnCompleted)
        {
            return;
        }

        FinalizeDeath();
    }

    private void RespawnFromStone(Vector2Int gridPos, Vector3 worldPos)
    {
        hasRespawned = true;
        block = 0;
        transform.position = worldPos;
        gridPosition = gridPos;
        SetHidden(false);
        SetForceHideIntent(false);

        if (battleManager == null)
        {
            battleManager = FindObjectOfType<BattleManager>();
        }

        if (battleManager != null && !battleManager.enemies.Contains(this))
        {
            battleManager.enemies.Add(this);
        }

        EnterPhaseTwo();
        RaiseStatusChanged();
    }

    private void EnterPhaseTwo()
    {
        inPhaseTwo = true;
        maxHP = Mathf.Max(1, phaseTwoHealth);
        currentHP = maxHP;
        BaseAttackDamage = phaseTwoBaseAttackDamage;
        currentArmorPerTurn = Mathf.Max(0, phaseTwoArmorPerTurn);
        attackRangeOffsets = new List<Vector2Int>(PhaseTwoMeleeAttackOffsets);
        phaseTwoSummonPending = HasConfiguredPhaseTwoSummons();

        stoneFeatherCooldownTimer = 0;
        stoneFeatherPending = false;
        waitingForStoneFeatherTakeOff = false;
        ClearStoneFeatherIndicators();

        if (!string.IsNullOrWhiteSpace(phaseTwoSkillDescription))
        {
            skillDescription = phaseTwoSkillDescription;
        }
    }

    private bool HasConfiguredPhaseTwoSummons()
    {
        if (phaseTwoSummons == null)
        {
            return false;
        }

        foreach (EnemySpawnConfig config in phaseTwoSummons)
        {
            if (config != null && config.enemyPrefab != null && config.count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void SummonPhaseTwoEnemies()
    {
        if (!HasConfiguredPhaseTwoSummons())
        {
            return;
        }

        if (battleManager == null)
        {
            battleManager = FindObjectOfType<BattleManager>();
        }

        Board board = FindObjectOfType<Board>();
        if (board == null)
        {
            return;
        }

        List<Vector2Int> availablePositions = GetAvailableSummonPositions(board);
        foreach (EnemySpawnConfig config in phaseTwoSummons)
        {
            if (config == null || config.enemyPrefab == null)
            {
                continue;
            }

            int spawnCount = Mathf.Max(0, config.count);
            for (int i = 0; i < spawnCount && availablePositions.Count > 0; i++)
            {
                int index = Random.Range(0, availablePositions.Count);
                Vector2Int spawnPos = availablePositions[index];
                availablePositions.RemoveAt(index);

                BoardTile tile = board.GetTileAt(spawnPos);
                if (tile == null)
                {
                    continue;
                }

                Enemy summonedEnemy = Instantiate(config.enemyPrefab, tile.transform.position, Quaternion.identity);
                summonedEnemy.gridPosition = spawnPos;
                phaseTwoSummonedEnemies.Add(summonedEnemy);

                if (battleManager != null && !battleManager.enemies.Contains(summonedEnemy))
                {
                    battleManager.enemies.Add(summonedEnemy);
                }
            }
        }
    }

    private List<Vector2Int> GetAvailableSummonPositions(Board board)
    {
        List<Vector2Int> availablePositions = board.GetAllPositions();

        Player player = battleManager != null ? battleManager.player : FindObjectOfType<Player>();
        if (player != null)
        {
            availablePositions.Remove(player.position);
        }

        availablePositions.Remove(gridPosition);

        Enemy[] allEnemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in allEnemies)
        {
            if (enemy != null)
            {
                availablePositions.Remove(enemy.gridPosition);
            }
        }

        return availablePositions;
    }

    private void CleanupPhaseTwoSummons()
    {
        if (phaseTwoSummonedEnemies.Count == 0)
        {
            return;
        }

        if (battleManager == null)
        {
            battleManager = FindObjectOfType<BattleManager>();
        }

        phaseTwoSummonedEnemies.RemoveAll(enemy => enemy == null);
        foreach (Enemy summonedEnemy in phaseTwoSummonedEnemies)
        {
            if (summonedEnemy == null)
            {
                continue;
            }

            if (battleManager != null)
            {
                battleManager.enemies.Remove(summonedEnemy);
            }

            Destroy(summonedEnemy.gameObject);
        }

        phaseTwoSummonedEnemies.Clear();
    }

    private void FinalizeDeath()
    {
        if (finalDeathHandled)
        {
            return;
        }

        finalDeathHandled = true;
        CleanupPhaseTwoSummons();
        ClearStoneFeatherIndicators();
        SetHidden(false);
        base.Die();
    }

    private void SetHidden(bool hidden)
    {
        EnsureRendererCache();
        foreach (SpriteRenderer renderer in cachedRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = !hidden;
            }
        }

        SetForceHideIntent(hidden);
        SetAreaDamagePreview(false);

        if (elementStatusDisplay == null)
        {
            elementStatusDisplay = GetComponentInChildren<EnemyElementStatusDisplay>(true);
        }

        if (elementStatusDisplay != null)
        {
            elementStatusDisplay.gameObject.SetActive(!hidden);
        }

        if (bottomHud == null)
        {
            bottomHud = GetComponentInChildren<EnemyBottomHudFinal>(true);
        }

        if (bottomHud != null)
        {
            bottomHud.gameObject.SetActive(!hidden);
        }
    }

    private void EnsureRendererCache()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    private void ApplyInitialMiasma()
    {
        Board board = FindObjectOfType<Board>();
        if (board == null)
        {
            return;
        }

        List<Vector2Int> positions = board.GetAllPositions();
        if (positions.Count == 0)
        {
            return;
        }

        int count = Mathf.Clamp(miasmaCenters, 0, positions.Count);
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, positions.Count);
            Vector2Int center = positions[index];
            positions.RemoveAt(index);
            SpreadMiasma(board, center);
        }
    }

    private void SpreadMiasma(Board board, Vector2Int center)
    {
        BoardTile centerTile = board.GetTileAt(center);
        if (centerTile != null)
        {
            ApplyMiasmaToTile(centerTile);
            miasmaTiles.Add(centerTile.gridPosition);
        }

        foreach (BoardTile tile in board.GetAdjacentTiles(center))
        {
            if (tile == null)
            {
                continue;
            }

            ApplyMiasmaToTile(tile);
            miasmaTiles.Add(tile.gridPosition);
        }
    }

    private void ApplyMiasmaToTile(BoardTile tile)
    {
        tile.SetMiasma(true, miasmaDamage);
    }
}
