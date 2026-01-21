using System.Collections.Generic;
using UnityEngine; // 引用 Unity 引擎命名空間

public class HuGuPo : Enemy, IEnemyCooldownProvider // 定義 HuGuPo 類別，繼承自 Enemy
{
    [Header("Hu Gu Po Settings")] // 在 Inspector 中顯示標題
    [SerializeField] private int bleedDuration = 2; // 流血狀態持續回合數
    [SerializeField] private int imprisonDuration = 2; // 禁錮狀態持續回合數
    [SerializeField] private int imprisonCooldownTurns = 3; // 禁錮技能冷卻回合需求
    [SerializeField] private int chargeCooldownTurns = 4; // 蓄力衝撞技能冷卻回合數
    [SerializeField] private int chargeDamage = 15; // 衝撞造成的傷害
    private int imprisonCooldownRemaining = 0; // 禁錮技能剩餘冷卻
    private int chargeCooldownRemaining = 0; // 蓄力衝撞技能剩餘冷卻

    private bool isPreparingCharge = false; // 是否正在準備衝撞
    private Vector2Int? lockedChargePosition = null; // 鎖定衝撞目標的位置（nullable）
    private BoardTile lockedChargeTile = null; // 目標位置上的地板 Tile，用於顯示警告

    private static readonly Vector2Int[] HexDirections = new Vector2Int[]
    {
        new Vector2Int(2, 0),
        new Vector2Int(-2, 0),
        new Vector2Int(-1, -2),
        new Vector2Int(1, -2),
        new Vector2Int(-1, 2),
        new Vector2Int(1, 2)
    };

    protected override void Awake()
    {
        enemyName = "虎姑婆"; // 設定敵人名稱
        base.Awake(); // 呼叫父類別 Awake()
        imprisonCooldownRemaining = imprisonCooldownTurns; // 開場即進入冷卻
        chargeCooldownRemaining = chargeCooldownTurns; // 開場即進入冷卻
    }
    public override void ProcessEnemyTurnEnd()
    {
        base.ProcessEnemyTurnEnd();
        TickCooldowns(); // 冷卻只在妖怪回合結束時計算
    }
    public override void EnemyAction(Player player)
    {
        if (HandleFrozen()) // 若處於冰凍，處理後直接結束行動
        {
            return;
        }
        DecideNextIntent(player);

        if (ProcessChargeMovement(player)) // 若正在執行衝撞移動
        {
            DecideNextIntent(player);
            return;
        }

        if (TryStartCharge(player)) // 嘗試開始蓄力衝撞
        {
            DecideNextIntent(player);
            return;
        }

        if (TryUseImprison(player)) // 嘗試對玩家施展禁錮
        {
            DecideNextIntent(player);
            return;
        }

        PerformAttackOrMoveWithBleed(player); // 若上述皆未觸發，則進行普通攻擊或靠近玩家
    }

    public override void DecideNextIntent(Player player)
    {
        if (player == null)
        {
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        if (frozenTurns > 0)
        {
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        if (isPreparingCharge)
        {
            nextIntent.type = EnemyIntentType.Skill;
            nextIntent.value = chargeDamage;
            UpdateIntentIcon();
            return;
        }

        bool canStartCharge = player.buffs.imprison > 0 && chargeCooldownRemaining == 0;
        if (canStartCharge)
        {
            nextIntent.type = EnemyIntentType.Skill;
            nextIntent.value = chargeDamage;
            UpdateIntentIcon();
            return;
        }

        bool canImprison = player.buffs.imprison <= 0 && imprisonCooldownRemaining == 0;
        if (canImprison)
        {
            nextIntent.type = EnemyIntentType.Skill;
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        if (IsPlayerInRange(player))
        {
            nextIntent.type = EnemyIntentType.Attack;
            nextIntent.value = CalculateAttackDamage();
        }
        else if (canMove)
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

    private bool HandleFrozen()
    {
        if (frozenTurns > 0) // 若冰凍中
        {
            SetFrozenTurns(Mathf.Max(0, frozenTurns - 1)); // 冰凍回合遞減
            return true; // 本回合無法行動
        }

        return false; // 未冰凍 → 可以繼續行動
    }

    private void PerformAttackOrMoveWithBleed(Player player)
    {
        if (player == null) // 玩家不存在 → 不做任何事
        {
            return;
        }

        if (IsPlayerInRange(player)) // 若玩家在攻擊範圍內
        {
            int damage = CalculateAttackDamage(); // 計算傷害
            if (damage > 0)
            {
                player.TakeDamage(damage); // 對玩家造成傷害
                player.buffs.ApplyBleedFromEnemy(bleedDuration); // 施加流血
            }
            return; // 已攻擊，結束行動
        }

        MoveOneStepTowards(player); // 玩家不在範圍內 → 向玩家移動一步
    }

    private bool TryUseImprison(Player player)
    {
        if (player == null) // 玩家不存在
        {
            return false;
        }

        if (player.buffs.imprison > 0) // 玩家已經被禁錮 → 不重複施放
        {
            return false;
        }

        if (imprisonCooldownRemaining > 0) // 冷卻未完成
        {
            return false;
        }

        player.buffs.ApplyImprisonFromEnemy(imprisonDuration); // 施加禁錮
        imprisonCooldownRemaining = imprisonCooldownTurns; // 進入禁錮冷卻
        return true;
    }

    private bool TryStartCharge(Player player)
    {
        if (isPreparingCharge) // 若已經在蓄力中 → 不重複開始
        {
            return false;
        }

        if (player == null || player.buffs.imprison <= 0) // 玩家未被禁錮 → 不啟動衝撞
        {
            return false;
        }

        if (chargeCooldownRemaining > 0) // 冷卻未完成
        {
            return false;
        }

        lockedChargePosition = player.position; // 鎖定玩家所在位置（衝撞目標）
        Board board = FindObjectOfType<Board>(); // 取得棋盤
        lockedChargeTile = board != null && lockedChargePosition.HasValue
            ? board.GetTileAt(lockedChargePosition.Value) // 取得目標的地板 Tile
            : null;
        lockedChargeTile?.SetAttackHighlight(true); // 地板上顯示危險提示

        isPreparingCharge = true; // 進入蓄力狀態
        return true;
    }

    private bool ProcessChargeMovement(Player player)
    {
        if (!isPreparingCharge) // 若沒有在蓄力 → 不進入衝撞流程
        {
            return false;
        }

        Vector2Int targetPos = lockedChargePosition ?? gridPosition; // 目標位置
        Vector2Int startPos = gridPosition; // 起始位置
        Board board = FindObjectOfType<Board>(); // 取得棋盤

        bool playerAtLockedSpot = player != null && player.position == targetPos; // 玩家是否在原地沒動
        if (playerAtLockedSpot)
        {
            TryKnockbackPlayer(player, board, startPos, targetPos); // 嘗試擊退玩家
            player.TakeDamage(chargeDamage); // 造成衝撞傷害
        }

        bool playerStillOnTarget = player != null && player.position == targetPos;
        if (!playerStillOnTarget)
        {
            MoveTowardsLockedPosition(board, targetPos);
        }

        isPreparingCharge = false; // 衝撞完成 → 結束蓄力狀態
        lockedChargePosition = null; // 清除鎖定位置
        if (lockedChargeTile != null)
        {
            lockedChargeTile.SetAttackHighlight(false); // 移除 Tile 的攻擊提示
            lockedChargeTile = null;
        }

        chargeCooldownRemaining = chargeCooldownTurns; // 衝撞流程結束 → 進入冷卻
        return true;
    }
    public int GetCooldownTurnsRemaining()
    {
        int maxCooldown = Mathf.Max(imprisonCooldownRemaining, chargeCooldownRemaining);
        return Mathf.Max(0, maxCooldown);
    }
    private void MoveTowardsLockedPosition(Board board, Vector2Int targetPos)
    {
        if (board == null) // 無棋盤 → 不執行
        {
            return;
        }

        BoardTile tile = board.GetTileAt(targetPos); // 找到目標位置的 Tile
        if (tile == null) // Tile 不存在
        {
            return;
        }

        bool occupiedByEnemy = board.IsTileOccupied(targetPos) && gridPosition != targetPos; // 目標被其他敵人占據
        if (occupiedByEnemy)
        {
            return; // 不能移動
        }

        gridPosition = targetPos; // 更新邏輯座標
        transform.position = tile.transform.position; // 更新場景位置
        UpdateSpriteSortingOrder(); // 更新圖層排序
    }

        private bool TryKnockbackPlayer(Player player, Board board, Vector2Int startPos, Vector2Int targetPos)    {
        if (player == null || board == null)
        {
            return false;
        }

        Vector2Int direction = targetPos - startPos; // 計算衝撞方向
        if (direction == Vector2Int.zero)
        {
            return false; // 無方向 → 不擊退
        }

        List<(Vector2Int dir, float dot)> scoredDirections = new List<(Vector2Int dir, float dot)>();
        Vector2 normalized = new Vector2(direction.x, direction.y).normalized;

        foreach (Vector2Int dir in HexDirections)
        {
            Vector2 dirFloat = new Vector2(dir.x, dir.y);
            float dot = Vector2.Dot(normalized, dirFloat.normalized);
            scoredDirections.Add((dir, dot));
        }

        scoredDirections.Sort((a, b) => b.dot.CompareTo(a.dot));

        foreach (var scored in scoredDirections)
        {
            Vector2Int knockbackPos = targetPos + scored.dir;
            BoardTile knockbackTile = board.GetTileAt(knockbackPos);

            if (knockbackTile == null || board.IsTileOccupied(knockbackPos))
            {
                continue;
            }

            player.position = knockbackPos;
            player.transform.position = knockbackTile.transform.position;
            knockbackTile.HandlePlayerEntered(player);
            return true;
        }

        return false;
    }

    private Vector2Int GetClosestHexDirection(Vector2Int rawDirection)
    {
        if (rawDirection == Vector2Int.zero)
        {
            return Vector2Int.zero;
        }

        Vector2 raw = new Vector2(rawDirection.x, rawDirection.y);
        Vector2Int bestDir = Vector2Int.zero;
        float bestDot = float.NegativeInfinity;

        foreach (Vector2Int dir in HexDirections)
        {
            Vector2 dirFloat = new Vector2(dir.x, dir.y);
            float dot = Vector2.Dot(raw.normalized, dirFloat.normalized);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestDir = dir;
            }
        }

        return bestDir;
    }

    private void TickCooldowns()
    {
        imprisonCooldownRemaining = Mathf.Max(0, imprisonCooldownRemaining - 1); // 禁錮冷卻倒數
        if (!isPreparingCharge)
        {
            chargeCooldownRemaining = Mathf.Max(0, chargeCooldownRemaining - 1); // 衝撞冷卻倒數
        }
    }
}
