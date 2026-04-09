using System.Collections.Generic;         // ä½¿ç”¨æ³›å??†å?ï¼Œä?å¦?List<T>
using UnityEngine;                        // ä½¿ç”¨ Unity å¼•æ??„æ ¸å¿ƒå???
using UnityEngine.SceneManagement;

public class GouShe : Enemy, IEnemyCooldownProvider               // ?¤è??ªç‰©é¡åˆ¥ï¼Œç¹¼?¿è‡ª Enemy ?ºå?é¡?
{
    private static readonly Vector2Int OffBoardSentinel = new Vector2Int(int.MinValue / 2, int.MinValue / 2);
    // ä¸€?‹ç‰¹æ®Šåº§æ¨™ï??¨ä?ä»?¡¨?Œæš«?‚é›¢?‹æ??¤ã€ï?ä¸åœ¨ä»»ä??‰æ??¼å?ä¸Šï?

    [Header("Gou She Settings")]
    [SerializeField] private int waterArmor = 2;                 // ç«™åœ¨æ°´æ ¼ä¸Šæ??²å??„é?å¤–è­·?²å€?
    [SerializeField] private int columnStrikeDamage = 10;        // ?´ç??“æ??€?½ç??·å®³
    [SerializeField] private int columnStrikeWeakDuration = 2;   // ?´ç??“æ??„å??›å¼±?€?‹ç??å???
    [SerializeField] private int columnStrikeCooldownTurns = 2;  // ?´ç??“æ??€?½å†·?»å??ˆæ•¸

    [Header("Passive Settings")]
    [SerializeField, Range(0f, 1f)] private float extraStrikeChance = 0.5f;
    // ?®é€šæ”»?Šæ?é¡å?å¤šæ?ä¸€æ®µå‚·å®³ç?æ©Ÿç?ï¼?~1 ä¹‹é?ï¼?

    [SerializeField, Range(0f, 1f)] private float extraStrikeDamageRatio = 0.3f;

    [Header("Column Strike FX")]
    [SerializeField] private string columnStrikeAnimationTriggerName = "SkillStart";
    [SerializeField] private float columnStrikeAnimationDuration = 0.8f;
    [SerializeField] private float columnStrikeFullScreenFxDuration = 0.8f;    // é¡å?ä¸€æ®µå‚·å®³ç?æ¯”ä?ï¼ˆç›¸å°æ–¼?¬æ¬¡?»æ??·å®³ï¼?
    [SerializeField] private RuntimeAnimatorController columnStrikeAreaFxController;
    [SerializeField] private Vector3 columnStrikeAreaFxOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private int columnStrikeAreaFxSortingOrderOffset = 20;
    [SerializeField] private Vector3 columnStrikeAreaFxScale = new Vector3(1f, 1.6f, 1f);

    private int columnStrikeCooldownRemaining;              // ?®å?è·é›¢?´ç??“æ??¯ç”¨?„å‰©å¹¾å??ˆå†·??
    private bool columnStrikePending = false;                    // ?¯å¦å·²ç??²å…¥?Œç›´ç·šæ??Šæ??™å??ï?ç­‰å??¼å??ç???
    private readonly HashSet<int> columnStrikeTargetColumns = new HashSet<int>();
    // è¦æ”»?Šç??®æ?æ¬„ä?ï¼ˆx åº§æ?ï¼‰ï??¯å??«å?æ¢ç›´ç·?
    private readonly List<BoardTile> columnStrikeHighlightedTiles = new List<BoardTile>();
    // è¢«æ?è¨˜ç‚º?³å?è¢«ç›´ç·šæ??Šç??¼å?æ¸…å–®ï¼Œç”¨ä¾†ä?å¾Œæ??¤é?äº?

    private Vector2Int storedGridBeforeHide;                     // ?¨æ?å¤±å?è¨˜é??„å?ä¾†æ??¤åº§æ¨?
    private SpriteRenderer[] cachedRenderers;                    // å¿«å?èº«ä??€??SpriteRendererï¼Œæ–¹ä¾¿ä??µéš±??é¡¯ç¤º
    private EnemyElementStatusDisplay elementStatusDisplay;      //  ?°å?ï¼šå?ç´ å?ç¤ºæ§?¶å?ä»¶ç??ƒè€?
    private bool initialWaterPrepared = false;                   // ?¯å¦å·²ç?å»ºç??å?å§‹æ°´?Ÿå???
    private GameObject gouSheFullScreenFxObject;
    private Animator gouSheFullScreenFxAnimator;
    private Coroutine gouSheFullScreenFxHideRoutine;
    private bool isResolvingColumnStrike;
    private readonly List<GameObject> spawnedColumnStrikeFxObjects = new List<GameObject>();
    public override bool SupportsSharedSquadTactics => false;

    protected override void Awake()
    {
        enemyName = "?¤è?";          // è¨­å??µäºº?ç¨±
        base.Awake();               // ?¼å«?ºå? Enemy.Awake() ?šé€šç”¨?å???
        columnStrikeCooldownRemaining = columnStrikeCooldownTurns; // ?‹å ´?¨è¨­å®šå€?
    }

    protected override void Start()
    {
        base.Start();
        PrepareInitialWaterZones(); // ?‹å ´?‚å»ºç«‹å?å§‹ç?æ°´å?ç´ å???
    }

    public override void ProcessTurnStart()
    {
        base.ProcessTurnStart();    // ?ˆåŸ·è¡ŒåŸºåº•ç??å??‹å?æµç?ï¼ˆè???buff ç­‰ï?
    }

    public override void ProcessEnemyTurnEnd()
    {
        base.ProcessEnemyTurnEnd();
        TickColumnStrikeCooldown(); // ?•ç??´ç??“æ??€?½ç??·å»?å??æ?
        ApplyWaterArmorIfOnTile();  // ?å?çµæ??ç½®è­·ç”²å¾Œï??¥ç??¨æ°´?¼ä??è?è­·ç”²
    }

    public override void EnemyAction(Player player)
    {
        if (HandleFrozen())   // ?¥æ??ç??€?‹ï??•ç??å?æ¶ˆè€—å??´æ¥çµæ?è¡Œå?
        {
            return;
        }

        if (columnStrikePending)       // ?¥å·²?²å…¥?´ç??“æ?æº–å?å®Œæ??€??
        {
            return;
        }

        if (columnStrikeCooldownRemaining <= 0 && IsOnWaterTile() && TryPrepareColumnStrike(player))
        {
            // ?·å»çµæ? + ç«™åœ¨æ°´æ ¼ä¸?+ ?å?æº–å??´ç??“æ? ???¬å??ˆåª?šæ??™å°± return
            return;
        }
        if (IsPlayerInRange(player))   // ?¥ç©å®¶åœ¨?®é€šæ”»?Šç??å…§
        {
            PerformAttackWithBonus(player); // ?²è?å¸¶æ?è¢«å?é¡å??·å®³æ©Ÿç??„æ™®?šæ”»??
        }
        else
        {
            if (CanMoveToAdjacentWater()) // ?¥æ??Šæ?æ°´æ ¼ï¼Œå„ª?ˆç›´?¥è¸©æ°?
            {
                return;
            }

            if (TryMoveOneStepTowardNearestWater(2)) // ??3 æ­¥å…§?‰æ°´?¼ï??ªå?? è?
            {
                return;
            }
            if (CanMoveThisTurn())
            {
                MoveOneStepTowards(player); // ?¦å??ç©å®¶ç§»?•ä???
            }
        }
    }

    public override void DecideNextIntent(Player player)
    {
        if (player == null)                       // æ²’æ??©å®¶?®æ???
        {
            nextIntent.type = EnemyIntentType.Idle;   // é¡¯ç¤º?ºå?æ©?
            nextIntent.value = 0;
            UpdateIntentIcon();                      // ?´æ–°?­ä??å??–ç¤º
            return;
        }

        if (frozenTurns > 0)   // ?¥ä??å??ƒè¢«?ç?
        {
            nextIntent.type = EnemyIntentType.Idle;   // ?å?é¡¯ç¤º?ºç„¡è¡Œå?
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        if (columnStrikePending)                // ?¥å·²ç¶“æ??™å¥½?´ç??“æ?ï¼Œä?ä¸€æ­¥å°±?¯ç™¼?•æ???
        {
            nextIntent.type = EnemyIntentType.Skill;  // é¡¯ç¤º?€?½æ???
            nextIntent.value = columnStrikeDamage;    // é¡¯ç¤º?è??·å®³
            UpdateIntentIcon();
            return;
        }

        bool specialReady = columnStrikeCooldownRemaining <= 0 && IsOnWaterTile();
        // ?¤æ–·?´ç??“æ??¯å¦?¯æ??™ï??·å»æ­¸é›¶ä¸”åœ¨æ°´æ ¼ï¼?

        if (specialReady)
        {
            nextIntent.type = EnemyIntentType.Skill;  // ä¸‹ä?æ­¥æ?ç®—æ–½?¾æ???
            nextIntent.value = columnStrikeDamage;
            UpdateIntentIcon();
            return;
        }

        if (IsPlayerInRange(player))           // ?¦å??‹ç©å®¶æ˜¯?¦åœ¨?®é€šæ”»?Šç??å…§
        {
            nextIntent.type = EnemyIntentType.Attack;     // é¡¯ç¤º?®é€šæ”»?Šæ???
            nextIntent.value = CalculateAttackDamage();   // é¡¯ç¤º?®é€šæ”»?Šå‚·å®?
        }
        else if (CanMoveThisTurn())            // ä¸åœ¨?»æ?ç¯„å?ï¼Œä??¯ä»¥ç§»å?
        {
            nextIntent.type = EnemyIntentType.Move;       // é¡¯ç¤ºç§»å??å?
            nextIntent.value = 0;
        }
        else                                   // ?¡æ?ç§»å?ä¹Ÿç„¡æ³•æ”»??
        {
            nextIntent.type = EnemyIntentType.Idle;       // é¡¯ç¤ºå¾…æ?
            nextIntent.value = 0;
        }

        UpdateIntentIcon();                    // ?€å¾Œæ›´?°æ??–å?ç¤?
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
        if (frozenTurns > 0)         // ?¥ç›®?æ??ç??å?
        {
            SetFrozenTurns(Mathf.Max(0, frozenTurns - 1));
            return true;            // ?å??´æ¥çµæ?ï¼ˆé€™å??ˆä??½å?ï¼?
        }

        return false;               // æ²’æ??ç?ï¼Œå¯ä»¥æ­£å¸¸è???
    }

    private void ApplyWaterArmorIfOnTile()
    {
        if (waterArmor <= 0)        // ?¥è¨­å®šç‚º 0 ?–ä»¥ä¸‹ï?å°±ä??•ç?
        {
            return;
        }

        if (!IsOnWaterTile())       // ?¥æ?ç«™åœ¨æ°´å?ç´ æ ¼
        {
            return;
        }

        block += waterArmor;        // å¢å?è­·ç”²ï¼ˆblockï¼?
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
        Board board = FindObjectOfType<Board>(); // å°‹æ‰¾æ£‹ç›¤?©ä»¶
        if (board == null)
        {
            return false;                        // æ²’æ?æ£‹ç›¤å°±ç„¡æ³•åˆ¤??
        }

        BoardTile tile = board.GetTileAt(gridPosition); // ?–å??¶å??€?¨æ ¼å­?
        return tile != null && tile.HasElement(ElementType.Water);
        // ?¥æ ¼å­å??¨ä??·æ?æ°´å?ç´ ï??‡å???true
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
        if (player == null)             // ?¥æ??‰ç©å®¶ç›®æ¨?
        {
            return;
        }

        int damage = CalculateAttackDamage(); // ??Enemy ?ºå?è¨ˆç?å¯¦é??»æ??·å®³ï¼ˆå« buff ç­‰ï?
        if (damage <= 0)              // ?¥å‚·å®³ä?å¤§æ–¼ 0ï¼Œå°±ä¸æ”»??
        {
            return;
        }

        player.TakeDamage(damage);    // å°ç©å®¶é€ æ?ä¸€æ¬¡åŸº?¬æ”»?Šå‚·å®?

        if (Random.value <= extraStrikeChance)   // ä¾ç…§æ©Ÿç?é¡å??æ?ä¸€æ®µå‚·å®?
        {
            int extraDamage = Mathf.CeilToInt(damage * extraStrikeDamageRatio);
            // é¡å??·å®³ = ?¬æ¬¡?·å®³ * æ¯”ä?ï¼Œå?ä¸Šå???

            if (extraDamage > 0)
            {
                player.TakeDamage(extraDamage);  // ?æ¬¡å°ç©å®¶é€ æ?é¡å??·å®³
            }
        }
    }

    private bool TryPrepareColumnStrike(Player player)
    {
        if (player == null)           // ?¡ç©å®¶ç›®æ¨™å°±?¡æ?æº–å??´ç??“æ?
        {
            return false;
        }

        Board board = FindObjectOfType<Board>(); // ?–å?æ£‹ç›¤
        if (board == null)
        {
            return false;
        }

        List<Vector2Int> columnPositions = new List<Vector2Int>(); // ?¨ä?è¨˜é??®æ??´ç??„æ??‰æ ¼å­åº§æ¨?
        columnStrikeTargetColumns.Clear();
        columnStrikeTargetColumns.Add(player.position.x);
        columnStrikeTargetColumns.Add(player.position.x - 1);
        columnStrikeTargetColumns.Add(player.position.x + 1);
        foreach (Vector2Int pos in board.GetAllPositions())        // èµ°è¨ªæ£‹ç›¤ä¸Šæ??‰ä?ç½?
        {
            if (columnStrikeTargetColumns.Contains(pos.x))          // ?¥è©²ä½ç½®??x ?¨ç›®æ¨™æ?ä½ä¸­
            {
                columnPositions.Add(pos);                          // ? å…¥?®æ??´ç?æ¸…å–®
            }
        }

        if (columnPositions.Count == 0)                            // ?¥æ??‰ä»»ä½•å?æ¬„ä??¼å?ï¼ˆç?è«–ä?ä¸æ??¼ç?ï¼?
        {
            return false;
        }

        ClearColumnHighlights();                                   // æ¸…é™¤?Šç?é«˜äº®?¼å?

        foreach (Vector2Int pos in columnPositions)                // å°‡å?æ¬„ä??„æ?ä¸€?‹æ ¼å­æ?è¨˜ç‚º?»æ?ç¯„å?
        {
            BoardTile tile = board.GetTileAt(pos);
            if (tile != null)
            {
                tile.SetAttackHighlight(true);                     // é¡¯ç¤º?»æ?é«˜äº®
                columnStrikeHighlightedTiles.Add(tile);            // ? å…¥?°ç›®?é?äº®æ??®ä¸­
            }
        }

        storedGridBeforeHide = gridPosition;                       // è¨˜é?æ¶ˆå¤±?ç??Ÿæœ¬åº§æ?
        columnStrikePending = true;                                // æ¨™è??ºã€Œå·²æº–å?å¥½ï?ä¸‹å??ˆç™¼?•ã€?
        SetHidden(true);                                           // ?Šè‡ªå·±éš±?ï?SpriteRenderer.enabled = falseï¼?
        SetHighlight(false);                                       // ?œé??ªèº«?„é¸?–é?äº?
        SetForceHideIntent(true);                                  // ?­ä??„æ??–ä?ä¸€èµ·é???
        gridPosition = OffBoardSentinel;                           // ?Šæ??¤åº§æ¨™è¨­?ºã€Œé›¢?‹æ??¤ã€ç??¹æ???
        return true;                                               // æº–å??å?
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
        Vector2Int bestPos = storedGridBeforeHide;                    // ?è¨­?åˆ°æ¶ˆå¤±?ç?ä½ç½®
        float bestDistance = float.MaxValue;                          // ?¨æ–¼?¾è??¢ç©å®¶æ?è¿‘ç??®æ?

        if (board != null)
        {
            foreach (Vector2Int pos in board.GetAllPositions())       // èµ°è¨ªæ£‹ç›¤ä¸Šæ??‰æ ¼
            {
                BoardTile tile = board.GetTileAt(pos);
                if (tile == null || !tile.HasElement(ElementType.Water))
                {
                    continue;                                         // å¿…é??¯å??¨ã€è€Œä??‰æ°´?ƒç??„æ ¼å­?
                }

                if (IsPositionBlocked(board, pos, player))
                {
                    continue;                                         // ?¥è©²ä½ç½®è¢«ä??¨å°±?¥é?
                }

                float dist = player != null ? Vector2Int.Distance(pos, player.position) : 0f;
                // ?¥æ??©å®¶ï¼Œå°±è¨ˆç??‡ç©å®¶ç?è·é›¢ï¼›å¦?‡è??¢è¨­??0

                if (dist < bestDistance)                              // ?¾è??¢ç©å®¶æ?è¿‘ç?ä½ç½®
                {
                    bestDistance = dist;
                    bestPos = pos;
                }
            }
        }

        if (board != null && (board.GetTileAt(bestPos) == null || IsPositionBlocked(board, bestPos, player)))
        {
            // ?¥å??›é¸?ºä??„ä?ç½®å·²ç¶“ä??¯ç”¨ï¼ˆæ?æ²’æ??¼å?ï¼‰ï?å°±é€€?Œæ??¶æ¬¡?¾ä»»ä¸€æ²’è¢«?»æ??„æ ¼å­?
            foreach (Vector2Int pos in board.GetAllPositions())
            {
                if (!IsPositionBlocked(board, pos, player))
                {
                    bestPos = pos;                                    // ?¾åˆ°ç¬¬ä??‹å¯ç«™ç??¼å?å°±ç”¨å®?
                    break;
                }
            }
        }

        return bestPos;                                               // ?å‚³?€å¾Œæ±ºå®šç??¾èº«ä½ç½®
    }

    private bool IsPositionBlocked(Board board, Vector2Int pos, Player player)
    {
        if (board == null)
        {
            return true;                                              // æ²’æ?æ£‹ç›¤å°±è??ºä??¯ç?
        }

        if (player != null && player.position == pos)
        {
            return true;                                              // ?¥è©²?¼æ˜¯?©å®¶?®å?ä½ç½®ï¼Œä?è¦–ç‚ºè¢«å???
        }

        return board.IsTileOccupied(pos);                             // ?¥æ??¤åˆ¤å®šè©²?¼æ??¶ä??®ä?ï¼Œä?è¦–ç‚ºè¢«å???
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
