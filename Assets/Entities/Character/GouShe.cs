using System.Collections.Generic;         // 使用泛�??��?，�?�?List<T>
using UnityEngine;                        // 使用 Unity 引�??�核心�???
using UnityEngine.SceneManagement;

public class GouShe : Enemy, IEnemyCooldownProvider               // ?��??�物類別，繼?�自 Enemy ?��?�?
{
    private static readonly Vector2Int OffBoardSentinel = new Vector2Int(int.MinValue / 2, int.MinValue / 2);
    // 一?�特殊座標�??��?�?��?�暫?�離?��??�」�?不在任�??��??��?上�?

    [Header("Gou She Settings")]
    [SerializeField] private int waterArmor = 2;                 // 站在水格上�??��??��?外護?��?
    [SerializeField] private int columnStrikeDamage = 10;        // ?��??��??�?��??�害
    [SerializeField] private int columnStrikeWeakDuration = 2;   // ?��??��??��??�弱?�?��??��???
    [SerializeField] private int columnStrikeCooldownTurns = 2;  // ?��??��??�?�冷?��??�數

    [Header("Passive Settings")]
    [SerializeField, Range(0f, 1f)] private float extraStrikeChance = 0.5f;
    // ?�通攻?��?額�?多�?一段傷害�?機�?�?~1 之�?�?

    [SerializeField, Range(0f, 1f)] private float extraStrikeDamageRatio = 0.3f;

    [Header("Column Strike FX")]
    [SerializeField] private string columnStrikeAnimationTriggerName = "SkillStart";
    [SerializeField] private float columnStrikeAnimationDuration = 0.8f;
    [SerializeField] private float columnStrikeFullScreenFxDuration = 0.8f;    // 額�?一段傷害�?比�?（相對於?�次?��??�害�?
    [SerializeField] private RuntimeAnimatorController columnStrikeAreaFxController;
    [SerializeField] private Vector3 columnStrikeAreaFxOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private int columnStrikeAreaFxSortingOrderOffset = 20;
    [SerializeField] private Vector3 columnStrikeAreaFxScale = new Vector3(1f, 1.6f, 1f);

    private int columnStrikeCooldownRemaining;              // ?��?距離?��??��??�用?�剩幾�??�冷??
    private bool columnStrikePending = false;                    // ?�否已�??�入?�直線�??��??��??��?等�??��??��???
    private readonly HashSet<int> columnStrikeTargetColumns = new HashSet<int>();
    // 要攻?��??��?欄�?（x 座�?）�??��??��?條直�?
    private readonly List<BoardTile> columnStrikeHighlightedTiles = new List<BoardTile>();
    // 被�?記為?��?被直線�??��??��?清單，用來�?後�??��?�?

    private Vector2Int storedGridBeforeHide;                     // ?��?失�?記�??��?來�??�座�?
    private SpriteRenderer[] cachedRenderers;                    // 快�?身�??�??SpriteRenderer，方便�??�隱??顯示
    private EnemyElementStatusDisplay elementStatusDisplay;      //  ?��?：�?素�?示控?��?件�??��?
    private bool initialWaterPrepared = false;                   // ?�否已�?建�??��?始水?��???
    private GameObject gouSheFullScreenFxObject;
    private Animator gouSheFullScreenFxAnimator;
    private Coroutine gouSheFullScreenFxHideRoutine;
    private bool isResolvingColumnStrike;
    private readonly List<GameObject> spawnedColumnStrikeFxObjects = new List<GameObject>();
    public override bool SupportsSharedSquadTactics => false;

    protected override void Awake()
    {
        base.Awake();               // ?�叫?��? Enemy.Awake() ?�通用?��???
        columnStrikeCooldownRemaining = columnStrikeCooldownTurns; // ?�場?�設定�?
    }

    protected override void Start()
    {
        base.Start();
        PrepareInitialWaterZones(); // ?�場?�建立�?始�?水�?素�???
    }

    public override void ProcessTurnStart()
    {
        base.ProcessTurnStart();    // ?�執行基底�??��??��?流�?（�???buff 等�?
    }

    public override void ProcessEnemyTurnEnd()
    {
        base.ProcessEnemyTurnEnd();
        TickColumnStrikeCooldown(); // ?��??��??��??�?��??�卻?��??��?
        ApplyWaterArmorIfOnTile();  // ?��?結�??�置護甲後�??��??�水?��??��?護甲
    }

    public override void EnemyAction(Player player)
    {
        if (HandleFrozen())   // ?��??��??�?��??��??��?消耗�??�接結�?行�?
        {
            return;
        }

        if (columnStrikePending)       // ?�已?�入?��??��?準�?完�??�??
        {
            return;
        }

        if (columnStrikeCooldownRemaining <= 0 && IsOnWaterTile() && TryPrepareColumnStrike(player))
        {
            // ?�卻結�? + 站在水格�?+ ?��?準�??��??��? ???��??�只?��??�就 return
            return;
        }
        if (IsPlayerInRange(player))   // ?�玩家在?�通攻?��??�內
        {
            PerformAttackWithBonus(player); // ?��?帶�?被�?額�??�害機�??�普?�攻??
        }
        else
        {
            if (CanMoveToAdjacentWater()) // ?��??��?水格，優?�直?�踩�?
            {
                return;
            }

            if (TryMoveOneStepTowardNearestWater(2)) // ??3 步內?�水?��??��??��?
            {
                return;
            }
            if (CanMoveThisTurn())
            {
                MoveOneStepTowards(player); // ?��??�玩家移?��???
            }
        }
    }

    public override void DecideNextIntent(Player player)
    {
        if (player == null)                       // 沒�??�家?��???
        {
            nextIntent.type = EnemyIntentType.Idle;   // 顯示?��?�?
            nextIntent.value = 0;
            UpdateIntentIcon();                      // ?�新?��??��??�示
            return;
        }

        if (frozenTurns > 0)   // ?��??��??�被?��?
        {
            nextIntent.type = EnemyIntentType.Idle;   // ?��?顯示?�無行�?
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        if (columnStrikePending)                // ?�已經�??�好?��??��?，�?一步就?�發?��???
        {
            nextIntent.type = EnemyIntentType.Skill;  // 顯示?�?��???
            nextIntent.value = columnStrikeDamage;    // 顯示?��??�害
            UpdateIntentIcon();
            return;
        }

        bool specialReady = columnStrikeCooldownRemaining <= 0 && IsOnWaterTile();
        // ?�斷?��??��??�否?��??��??�卻歸零且在水格�?

        if (specialReady)
        {
            nextIntent.type = EnemyIntentType.Skill;  // 下�?步�?算施?��???
            nextIntent.value = columnStrikeDamage;
            UpdateIntentIcon();
            return;
        }

        if (IsPlayerInRange(player))           // ?��??�玩家是?�在?�通攻?��??�內
        {
            nextIntent.type = EnemyIntentType.Attack;     // 顯示?�通攻?��???
            nextIntent.value = CalculateAttackDamage();   // 顯示?�通攻?�傷�?
        }
        else if (CanMoveThisTurn())            // 不在?��?範�?，�??�以移�?
        {
            nextIntent.type = EnemyIntentType.Move;       // 顯示移�??��?
            nextIntent.value = 0;
        }
        else                                   // ?��?移�?也無法攻??
        {
            nextIntent.type = EnemyIntentType.Idle;       // 顯示待�?
            nextIntent.value = 0;
        }

        UpdateIntentIcon();                    // ?�後更?��??��?�?
    }

    public override System.Collections.IEnumerator EnemyActionRoutine(Player player)
    {
        if (!columnStrikePending)
        {
            yield return base.EnemyActionRoutine(player);
            yield break;
        }

        bool wasFrozenBeforeAction = frozenTurns > 0;
        if (HandleFrozen())
        {
            if (immobilizedTurns > 0)
            {
                SetImmobilizedTurns(immobilizedTurns - 1);
            }
            float frozenDelay = GetSequentialActionDelay(nextIntent.type, wasFrozenBeforeAction);
            if (frozenDelay > 0f)
            {
                yield return new WaitForSeconds(frozenDelay);
            }

            yield break;
        }

        yield return ResolveColumnStrikeRoutine(player);
        if (immobilizedTurns > 0)
        {
            SetImmobilizedTurns(immobilizedTurns - 1);
        }
        if (Movement != null && Movement.IsMoving)
        {
            yield return new WaitUntil(() => Movement == null || !Movement.IsMoving);
        }
    }

    private bool HandleFrozen()
    {
        if (frozenTurns > 0)         // ?�目?��??��??��?
        {
            SetFrozenTurns(Mathf.Max(0, frozenTurns - 1));
            return true;            // ?��??�接結�?（這�??��??��?�?
        }

        return false;               // 沒�??��?，可以正常�???
    }

    private void ApplyWaterArmorIfOnTile()
    {
        if (waterArmor <= 0)        // ?�設定為 0 ?�以下�?就�??��?
        {
            return;
        }

        if (!IsOnWaterTile())       // ?��?站在水�?素格
        {
            return;
        }

        block += waterArmor;        // 增�?護甲（block�?
        RaiseStatusChanged();
    }
    public int CooldownSlotCount => 1;

    public int GetCooldownTurnsRemaining(int slotIndex)
    {
        if (slotIndex != 0)
        {
            return 0;
        }
        return Mathf.Max(0, columnStrikeCooldownRemaining);
    }
    private bool IsOnWaterTile()
    {
        Board board = FindObjectOfType<Board>(); // 尋找棋盤?�件
        if (board == null)
        {
            return false;                        // 沒�?棋盤就無法判??
        }

        BoardTile tile = board.GetTileAt(gridPosition); // ?��??��??�?�格�?
        return tile != null && tile.HasElement(ElementType.Water);
        // ?�格子�??��??��?水�?素�??��???true
    }
    private bool CanMoveToAdjacentWater()
    {
        if (!CanMoveThisTurn())
        {
            return false;
        }

        Board board = FindObjectOfType<Board>();
        if (board == null)
        {
            return false;
        }

        Player player = FindObjectOfType<Player>();

        foreach (BoardTile tile in board.GetAdjacentTiles(gridPosition))
        {
            if (tile == null || !tile.HasElement(ElementType.Water))
            {
                continue;
            }

            Vector2Int targetPos = tile.gridPosition;
            if (IsPositionBlocked(board, targetPos, player))
            {
                continue;
            }

            MoveToPosition(targetPos);
            return true;
        }

        return false;
    }

    private bool TryMoveOneStepTowardNearestWater(int maxSteps)
    {
        if (!CanMoveThisTurn())
        {
            return false;
        }

        if (IsOnWaterTile())
        {
            return false;
        }

        Board board = FindObjectOfType<Board>();
        if (board == null)
        {
            return false;
        }

        Vector2Int? targetWater = FindNearestWaterWithinSteps(board, maxSteps);
        if (!targetWater.HasValue)
        {
            return false;
        }

        Player player = FindObjectOfType<Player>();
        Vector2Int bestPos = gridPosition;
        int bestDistance = int.MaxValue;

        foreach (BoardTile tile in board.GetAdjacentTiles(gridPosition))
        {
            if (tile == null)
            {
                continue;
            }

            Vector2Int nextPos = tile.gridPosition;
            if (IsPositionBlocked(board, nextPos, player))
            {
                continue;
            }

            int distance = ComputeStepDistance(board, nextPos, targetWater.Value, player);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPos = nextPos;
            }
        }

        if (bestPos != gridPosition && bestDistance < int.MaxValue)
        {
            MoveToPosition(bestPos);
            return true;
        }

        return false;
    }

    private Vector2Int? FindNearestWaterWithinSteps(Board board, int maxSteps)
    {
        if (board == null)
        {
            return null;
        }

        Player player = FindObjectOfType<Player>();
        Queue<(Vector2Int pos, int steps)> pending = new Queue<(Vector2Int pos, int steps)>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int> { gridPosition };

        pending.Enqueue((gridPosition, 0));

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (current.steps > maxSteps)
            {
                continue;
            }

            if (current.steps > 0)
            {
                BoardTile tile = board.GetTileAt(current.pos);
                if (tile != null && tile.HasElement(ElementType.Water))
                {
                    return current.pos;
                }
            }

            if (current.steps == maxSteps)
            {
                continue;
            }

            foreach (BoardTile tile in board.GetAdjacentTiles(current.pos))
            {
                if (tile == null)
                {
                    continue;
                }

                Vector2Int next = tile.gridPosition;
                if (!visited.Add(next))
                {
                    continue;
                }

                if (IsPositionBlocked(board, next, player))
                {
                    continue;
                }

                pending.Enqueue((next, current.steps + 1));
            }
        }

        return null;
    }

    private int ComputeStepDistance(Board board, Vector2Int start, Vector2Int target, Player player)
    {
        if (start == target)
        {
            return 0;
        }

        Queue<(Vector2Int pos, int dist)> pending = new Queue<(Vector2Int pos, int dist)>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int> { start };

        pending.Enqueue((start, 0));

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            int nextDist = current.dist + 1;

            foreach (BoardTile tile in board.GetAdjacentTiles(current.pos))
            {
                if (tile == null)
                {
                    continue;
                }

                Vector2Int next = tile.gridPosition;
                if (!visited.Add(next))
                {
                    continue;
                }

                if (IsPositionBlocked(board, next, player))
                {
                    continue;
                }

                if (next == target)
                {
                    return nextDist;
                }

                pending.Enqueue((next, nextDist));
            }
        }

        return int.MaxValue;
    }
    private void PerformAttackWithBonus(Player player)
    {
        if (player == null)             // ?��??�玩家目�?
        {
            return;
        }

        int damage = CalculateAttackDamage(); // ??Enemy ?��?計�?實�??��??�害（含 buff 等�?
        if (damage <= 0)              // ?�傷害�?大於 0，就不攻??
        {
            return;
        }

        Visual?.PlayContactAttackToPlayer(player);
        player.TakeDamage(damage);    // 對玩家造�?一次基?�攻?�傷�?

        if (Random.value <= extraStrikeChance)   // 依照機�?額�??��?一段傷�?
        {
            int extraDamage = Mathf.CeilToInt(damage * extraStrikeDamageRatio);
            // 額�??�害 = ?�次?�害 * 比�?，�?上�???

            if (extraDamage > 0)
            {
                player.TakeDamage(extraDamage);  // ?�次對玩家造�?額�??�害
            }
        }
    }

    private bool TryPrepareColumnStrike(Player player)
    {
        if (player == null)           // ?�玩家目標就?��?準�??��??��?
        {
            return false;
        }

        Board board = FindObjectOfType<Board>(); // ?��?棋盤
        if (board == null)
        {
            return false;
        }

        List<Vector2Int> columnPositions = new List<Vector2Int>(); // ?��?記�??��??��??��??�格子座�?
        columnStrikeTargetColumns.Clear();
        columnStrikeTargetColumns.Add(player.position.x);
        columnStrikeTargetColumns.Add(player.position.x - 1);
        columnStrikeTargetColumns.Add(player.position.x + 1);
        foreach (Vector2Int pos in board.GetAllPositions())        // 走訪棋盤上�??��?�?
        {
            if (columnStrikeTargetColumns.Contains(pos.x))          // ?�該位置??x ?�目標�?位中
            {
                columnPositions.Add(pos);                          // ?�入?��??��?清單
            }
        }

        if (columnPositions.Count == 0)                            // ?��??�任何�?欄�??��?（�?論�?不�??��?�?
        {
            return false;
        }

        ClearColumnHighlights();                                   // 清除?��?高亮?��?

        foreach (Vector2Int pos in columnPositions)                // 將�?欄�??��?一?�格子�?記為?��?範�?
        {
            BoardTile tile = board.GetTileAt(pos);
            if (tile != null)
            {
                tile.SetAttackHighlight(true);                     // 顯示?��?高亮
                columnStrikeHighlightedTiles.Add(tile);            // ?�入?�目?��?亮�??�中
            }
        }

        storedGridBeforeHide = gridPosition;                       // 記�?消失?��??�本座�?
        columnStrikePending = true;                                // 標�??�「已準�?好�?下�??�發?��?
        SetHidden(true);                                           // ?�自己隱?��?SpriteRenderer.enabled = false�?
        SetHighlight(false);                                       // ?��??�身?�選?��?�?
        SetForceHideIntent(true);                                  // ?��??��??��?一起�???
        gridPosition = OffBoardSentinel;                           // ?��??�座標設?�「離?��??�」�??��???
        return true;                                               // 準�??��?
    }

    private void ResolveColumnStrike(Player player)
    {
        if (!isResolvingColumnStrike)
        {
            StartCoroutine(ResolveColumnStrikeRoutine(player));
        }
    }

    private System.Collections.IEnumerator ResolveColumnStrikeRoutine(Player player)
    {
        if (isResolvingColumnStrike)
        {
            yield break;
        }

        isResolvingColumnStrike = true;

        Board board = FindObjectOfType<Board>();
        Vector2Int targetPos = ChooseReappearPosition(board, player);

        PlayColumnStrikeAreaFx(board);
        PlayColumnStrikeFullScreenFx();

        float fxDuration = Mathf.Max(columnStrikeAnimationDuration, columnStrikeFullScreenFxDuration);
        if (fxDuration > 0f)
        {
            yield return new WaitForSeconds(fxDuration);
        }

        ClearSpawnedColumnStrikeFxObjects();

        bool playerHit = player != null && columnStrikeTargetColumns.Contains(player.position.x);
        if (playerHit)
        {
            player.TakeDamage(columnStrikeDamage);
            player.buffs.ApplyWeakFromEnemy(columnStrikeWeakDuration);
        }

        ClearColumnHighlights();

        MoveToPosition(targetPos);
        SetHidden(false);
        SetForceHideIntent(false);

        columnStrikePending = false;
        columnStrikeTargetColumns.Clear();
        columnStrikeCooldownRemaining = columnStrikeCooldownTurns;
        isResolvingColumnStrike = false;
    }

    private Vector2Int ChooseReappearPosition(Board board, Player player)
    {
        Vector2Int bestPos = storedGridBeforeHide;                    // ?�設?�到消失?��?位置
        float bestDistance = float.MaxValue;                          // ?�於?��??�玩家�?近�??��?

        if (board != null)
        {
            foreach (Vector2Int pos in board.GetAllPositions())       // 走訪棋盤上�??�格
            {
                BoardTile tile = board.GetTileAt(pos);
                if (tile == null || !tile.HasElement(ElementType.Water))
                {
                    continue;                                         // 必�??��??�、而�??�水?��??�格�?
                }

                if (IsPositionBlocked(board, pos, player))
                {
                    continue;                                         // ?�該位置被�??�就?��?
                }

                float dist = player != null ? Vector2Int.Distance(pos, player.position) : 0f;
                // ?��??�家，就計�??�玩家�?距離；否?��??�設??0

                if (dist < bestDistance)                              // ?��??�玩家�?近�?位置
                {
                    bestDistance = dist;
                    bestPos = pos;
                }
            }
        }

        if (board != null && (board.GetTileAt(bestPos) == null || IsPositionBlocked(board, bestPos, player)))
        {
            // ?��??�選?��??��?置已經�??�用（�?沒�??��?）�?就退?��??�次?�任一沒被?��??�格�?
            foreach (Vector2Int pos in board.GetAllPositions())
            {
                if (!IsPositionBlocked(board, pos, player))
                {
                    bestPos = pos;                                    // ?�到第�??�可站�??��?就用�?
                    break;
                }
            }
        }

        return bestPos;                                               // ?�傳?�後決定�??�身位置
    }

    private bool IsPositionBlocked(Board board, Vector2Int pos, Player player)
    {
        if (board == null)
        {
            return true;                                              // 沒�?棋盤就�??��??��?
        }

        if (player != null && player.position == pos)
        {
            return true;                                              // ?�該?�是?�家?��?位置，�?視為被�???
        }

        return board.IsTileOccupied(pos);                             // ?��??�判定該?��??��??��?，�?視為被�???
    }

    private void ClearColumnHighlights()
    {
        foreach (BoardTile tile in columnStrikeHighlightedTiles)
        {
            if (tile != null)
            {
                tile.SetAttackHighlight(false);
            }
        }

        columnStrikeHighlightedTiles.Clear();
    }

    private void PlayColumnStrikeAnimation()
    {
        if (string.IsNullOrWhiteSpace(columnStrikeAnimationTriggerName))
        {
            return;
        }

        Visual?.PlaySkillStart();

        Animator bodyAnimator = ResolveBodyAnimator();
        if (!HasAnimatorTrigger(bodyAnimator, columnStrikeAnimationTriggerName))
        {
            return;
        }

        bodyAnimator.ResetTrigger(columnStrikeAnimationTriggerName);
        bodyAnimator.SetTrigger(columnStrikeAnimationTriggerName);
    }

    private void PlayColumnStrikeAreaFx(Board board)
    {
        ClearSpawnedColumnStrikeFxObjects();
        if (board == null || columnStrikeAreaFxController == null)
        {
            return;
        }

        List<BoardTile> centerTiles = FindColumnStrikeCenterTiles(board);
        for (int i = 0; i < centerTiles.Count; i++)
        {
            BoardTile tile = centerTiles[i];
            if (tile == null)
            {
                continue;
            }

            GameObject fxObject = new GameObject("GouShe_ColumnStrikeFx");
            fxObject.transform.SetParent(tile.transform, false);
            fxObject.transform.localPosition = columnStrikeAreaFxOffset;
            fxObject.transform.localRotation = Quaternion.identity;
            fxObject.transform.localScale = columnStrikeAreaFxScale;

            SpriteRenderer fxRenderer = fxObject.AddComponent<SpriteRenderer>();
            SpriteRenderer tileRenderer = tile.GetComponent<SpriteRenderer>();
            if (tileRenderer == null)
            {
                tileRenderer = tile.GetComponentInChildren<SpriteRenderer>(true);
            }

            if (tileRenderer != null)
            {
                fxRenderer.sortingLayerID = tileRenderer.sortingLayerID;
                fxRenderer.sortingOrder = tileRenderer.sortingOrder + columnStrikeAreaFxSortingOrderOffset;
            }
            else
            {
                fxRenderer.sortingOrder = columnStrikeAreaFxSortingOrderOffset;
            }

            Animator fxAnimator = fxObject.AddComponent<Animator>();
            fxAnimator.runtimeAnimatorController = columnStrikeAreaFxController;
            fxAnimator.Rebind();
            fxAnimator.Update(0f);
            if (fxAnimator.layerCount > 0)
            {
                fxAnimator.Play(0, 0, 0f);
            }

            spawnedColumnStrikeFxObjects.Add(fxObject);
        }
    }

    private List<BoardTile> FindColumnStrikeCenterTiles(Board board)
    {
        List<BoardTile> result = new List<BoardTile>();
        if (board == null || columnStrikeTargetColumns.Count == 0)
        {
            return result;
        }

        List<int> orderedColumns = new List<int>(columnStrikeTargetColumns);
        orderedColumns.Sort();
        int centerColumn = orderedColumns[orderedColumns.Count / 2];

        BoardTile bestTile = null;
        int bestDistance = int.MaxValue;

        List<Vector2Int> positions = board.GetAllPositions();
        for (int i = 0; i < positions.Count; i++)
        {
            Vector2Int pos = positions[i];
            if (pos.x != centerColumn)
            {
                continue;
            }

            BoardTile tile = board.GetTileAt(pos);
            if (tile == null)
            {
                continue;
            }

            int distanceToCenter = Mathf.Abs(pos.y);
            if (bestTile == null || distanceToCenter < bestDistance || (distanceToCenter == bestDistance && pos.y > bestTile.gridPosition.y))
            {
                bestTile = tile;
                bestDistance = distanceToCenter;
            }
        }

        if (bestTile != null)
        {
            result.Add(bestTile);
        }

        return result;
    }

    private void ClearSpawnedColumnStrikeFxObjects()
    {
        for (int i = 0; i < spawnedColumnStrikeFxObjects.Count; i++)
        {
            if (spawnedColumnStrikeFxObjects[i] != null)
            {
                Destroy(spawnedColumnStrikeFxObjects[i]);
            }
        }

        spawnedColumnStrikeFxObjects.Clear();
    }

    private void PlayColumnStrikeFullScreenFx()
    {
        ResolveColumnStrikeFullScreenFx();
        if (gouSheFullScreenFxObject == null)
        {
            return;
        }

        bool hasPlayableAnimator = gouSheFullScreenFxAnimator != null
            && gouSheFullScreenFxAnimator.runtimeAnimatorController != null
            && gouSheFullScreenFxAnimator.layerCount > 0;

        if (gouSheFullScreenFxObject.activeSelf)
        {
            gouSheFullScreenFxObject.SetActive(false);
        }

        gouSheFullScreenFxObject.SetActive(true);

        if (hasPlayableAnimator)
        {
            gouSheFullScreenFxAnimator.Rebind();
            gouSheFullScreenFxAnimator.Update(0f);
            gouSheFullScreenFxAnimator.Play(0, 0, 0f);
        }

        if (gouSheFullScreenFxHideRoutine != null)
        {
            StopCoroutine(gouSheFullScreenFxHideRoutine);
        }

        gouSheFullScreenFxHideRoutine = StartCoroutine(HideColumnStrikeFullScreenFxAfterDelay());
    }

    private void ResolveColumnStrikeFullScreenFx()
    {
        if (gouSheFullScreenFxObject != null)
        {
            if (gouSheFullScreenFxAnimator == null)
            {
                gouSheFullScreenFxAnimator = gouSheFullScreenFxObject.GetComponent<Animator>();
            }
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.name != "Canvas")
            {
                continue;
            }

            Transform fxTransform = root.transform.Find("BossSkillFXRoot/GouSheFullScreenFX");
            if (fxTransform == null)
            {
                continue;
            }

            gouSheFullScreenFxObject = fxTransform.gameObject;
            gouSheFullScreenFxAnimator = gouSheFullScreenFxObject.GetComponent<Animator>();
            break;
        }
    }

    private System.Collections.IEnumerator HideColumnStrikeFullScreenFxAfterDelay()
    {
        float duration = Mathf.Max(0f, columnStrikeFullScreenFxDuration);
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        if (gouSheFullScreenFxObject != null)
        {
            gouSheFullScreenFxObject.SetActive(false);
        }

        gouSheFullScreenFxHideRoutine = null;
    }

    private static bool HasAnimatorTrigger(Animator animator, string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger
                && parameter.name == triggerName)
            {
                return true;
            }
        }

        return false;
    }

    private Animator ResolveBodyAnimator()
    {
        Transform root = spriteRoot != null ? spriteRoot : transform;
        Animator animator = root.GetComponent<Animator>();
        if (animator == null)
        {
            animator = root.GetComponentInChildren<Animator>(true);
        }

        return animator;
    }

    private void PrepareInitialWaterZones()
    {
        if (initialWaterPrepared)
        {
            return;
        }

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

        initialWaterPrepared = true;

        int clusterCount = Mathf.Min(3, positions.Count);
        for (int i = 0; i < clusterCount; i++)
        {
            int index = Random.Range(0, positions.Count);
            Vector2Int center = positions[index];
            positions.RemoveAt(index);
            ApplyWaterAround(center, board);
        }
    }

    private void ApplyWaterAround(Vector2Int center, Board board)
    {
        BoardTile centerTile = board.GetTileAt(center);
        if (centerTile != null)
        {
            centerTile.AddElement(ElementType.Water);
        }

        foreach (BoardTile tile in board.GetAdjacentTiles(center))
        {
            tile.AddElement(ElementType.Water);
        }
    }

    private void TickColumnStrikeCooldown()
    {
        if (columnStrikePending)
        {
            return;
        }
        if (columnStrikeCooldownRemaining > 0)
        {
            columnStrikeCooldownRemaining--;
        }
    }

    private void SetHidden(bool hidden)
    {
        EnsureRendererCache();
        foreach (var renderer in cachedRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = !hidden;
            }
        }

        SetForceHideIntent(hidden);

        if (elementStatusDisplay == null)
        {
            elementStatusDisplay = GetComponentInChildren<EnemyElementStatusDisplay>(true);
        }
        if (elementStatusDisplay != null)
        {
            elementStatusDisplay.gameObject.SetActive(!hidden);
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
}
