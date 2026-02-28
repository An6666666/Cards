// 引入泛型集合型別（List、HashSet、IReadOnlyList）。
using System.Collections.Generic;
// 引入 Unity 常用 API（Vector2Int、Mathf、ScriptableObject 等）。
using UnityEngine;

/// <summary>
/// 星芒破陣（1費）：
/// 直線移動 2 格，對路徑敵人造成 5 點傷害，並擊退 1 格。
/// </summary>
[CreateAssetMenu(fileName = "Move_XingMangPoZhen", menuName = "Cards/Movement/星芒破陣")]
public class Move_XingMangPoZhen : MovementCardBase
{
    // 六角棋盤的六個基本方向（與專案其他移動邏輯一致）。
    private static readonly Vector2Int[] HexDirections =
    {
        // 向右。
        new Vector2Int(2, 0),
        // 向左。
        new Vector2Int(-2, 0),
        // 左下。
        new Vector2Int(-1, -2),
        // 右下。
        new Vector2Int(1, -2),
        // 左上。
        new Vector2Int(-1, 2),
        // 右上。
        new Vector2Int(1, 2)
    };

    // 位移步數（最小 1）。
    [Min(1)] public int moveSteps = 2;
    // 路徑命中傷害（最小 0）。
    [Min(0)] public int pathDamage = 5;
    // 擊退步數（最小 0）。
    [Min(0)] public int knockbackSteps = 1;

    // 這張牌是「選格型移動牌」，實際效果在 ExecuteOnPosition 內處理。
    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        // 刻意留空：由移動選格流程呼叫 ExecuteOnPosition。
    }

    // 資產啟用時初始化卡牌資料。
    private void OnEnable()
    {
        // 指定本卡為移動牌。
        cardType = CardType.Movement;

        // 若尚未設定費用，預設為 1。
        if (cost <= 0)
        {
            cost = 1;
        }

        // 依 moveSteps 重建可選目標偏移。
        RebuildRangeOffsets();
    }

    // Inspector 修改數值時自動同步可選範圍。
    private void OnValidate()
    {
        // 重新計算偏移，避免資料不同步。
        RebuildRangeOffsets();
    }

    // 依參數重建可點選的目標偏移。
    private void RebuildRangeOffsets()
    {
        // 防呆：位移步數至少 1。
        moveSteps = Mathf.Max(1, moveSteps);
        // 防呆：傷害不能為負數。
        pathDamage = Mathf.Max(0, pathDamage);
        // 防呆：擊退步數不能為負數。
        knockbackSteps = Mathf.Max(0, knockbackSteps);

        // 建立偏移清單，容量先配置為「六方向 * 每層步數」。
        List<Vector2Int> offsets = new List<Vector2Int>(HexDirections.Length * moveSteps);
        // 逐一加入「每個方向的第 1~moveSteps 格」。
        for (int i = 0; i < HexDirections.Length; i++)
        {
            for (int step = 1; step <= moveSteps; step++)
            {
                offsets.Add(HexDirections[i] * step);
            }
        }

        // 寫回給移動選格系統使用。
        rangeOffsets = offsets;
    }

    // 玩家點選目標格後執行效果。
    public override void ExecuteOnPosition(Player player, Vector2Int targetGridPos)
    {
        // 防呆：無玩家參考時直接返回。
        if (player == null)
        {
            return;
        }

        // 記錄移動前玩家座標。
        Vector2Int startPos = player.position;
        // 解析本次移動方向與實際步數；若不是合法目標則中止。
        if (!TryGetDashDirection(startPos, targetGridPos, out Vector2Int dashDirection, out int dashSteps))
        {
            return;
        }

        // 取得棋盤參考，優先走 RuntimeContext，沒有再 fallback 場景搜尋。
        Board board = BattleRuntimeContext.Active?.Board ?? Object.FindObjectOfType<Board>();
        // 取得當前敵人清單。
        IReadOnlyList<Enemy> enemies = BattleRuntimeContext.Active?.Enemies;
        // 先收集路徑上會被命中的敵人（先收集可避免移動後狀態干擾判定）。
        List<Enemy> hitEnemies = CollectPathEnemies(startPos, dashDirection, dashSteps, enemies);

        // 先執行玩家移動。
        player.MoveToPosition(targetGridPos);
        // 若移動實際失敗（位置沒變到目標），則不結算傷害與擊退。
        if (player.position != targetGridPos)
        {
            return;
        }

        // 逐一處理被命中的敵人。
        for (int i = 0; i < hitEnemies.Count; i++)
        {
            // 取出當前目標敵人。
            Enemy hitEnemy = hitEnemies[i];
            // 防呆：敵人不存在或已死亡則跳過。
            if (hitEnemy == null || hitEnemy.currentHP <= 0)
            {
                continue;
            }

            // 套用路徑傷害。
            hitEnemy.TakeDamage(pathDamage);

            // 只有存活且設定有擊退步數時才嘗試擊退。
            if (hitEnemy.currentHP > 0 && knockbackSteps > 0)
            {
                // 沿玩家衝刺方向把敵人往後推。
                TryKnockbackEnemy(hitEnemy, dashDirection, knockbackSteps, board, player);
            }
        }
    }

    // 檢查目標格是否為合法衝刺終點，並回傳對應方向與實際步數。
    private bool TryGetDashDirection(
        Vector2Int startPos,
        Vector2Int targetPos,
        out Vector2Int dashDirection,
        out int dashSteps)
    {
        // 遍歷六個方向找匹配。
        for (int i = 0; i < HexDirections.Length; i++)
        {
            // 取出候選方向。
            Vector2Int dir = HexDirections[i];
            // 若「起點 + 方向 * 1~moveSteps」其中之一等於目標，表示合法。
            for (int step = 1; step <= moveSteps; step++)
            {
                if (startPos + dir * step == targetPos)
                {
                    // 回傳找到的方向與步數。
                    dashDirection = dir;
                    dashSteps = step;
                    // 表示成功。
                    return true;
                }
            }
        }

        // 沒找到方向時給預設值。
        dashDirection = Vector2Int.zero;
        dashSteps = 0;
        // 表示失敗。
        return false;
    }

    // 收集路徑上所有會被命中的敵人（去重）。
    private List<Enemy> CollectPathEnemies(
        Vector2Int startPos,
        Vector2Int dashDirection,
        int dashSteps,
        IReadOnlyList<Enemy> enemies)
    {
        // 準備結果清單。
        List<Enemy> results = new List<Enemy>();
        // 若敵人清單不存在或為空，直接回傳空結果。
        if (enemies == null || enemies.Count == 0)
        {
            return results;
        }

        dashSteps = Mathf.Max(0, dashSteps);

        // 用 HashSet 去重，避免同一敵人重複加入。
        HashSet<Enemy> unique = new HashSet<Enemy>();
        // 檢查從第 1 步到第 dashSteps 步的每個格子。
        for (int step = 1; step <= dashSteps; step++)
        {
            // 算出本步要檢查的路徑格子。
            Vector2Int checkPos = startPos + dashDirection * step;
            // 掃描所有敵人是否站在該格。
            for (int i = 0; i < enemies.Count; i++)
            {
                // 取出當前敵人。
                Enemy enemy = enemies[i];
                // 忽略空參考或已死亡敵人。
                if (enemy == null || enemy.currentHP <= 0)
                {
                    continue;
                }

                // 若敵人在檢查格且尚未加入，則加入結果。
                if (enemy.gridPosition == checkPos && unique.Add(enemy))
                {
                    results.Add(enemy);
                }
            }
        }

        // 回傳收集到的命中敵人列表。
        return results;
    }

    // 嘗試把敵人往 dashDirection 方向擊退指定步數。
    private static void TryKnockbackEnemy(
        Enemy enemy,
        Vector2Int dashDirection,
        int steps,
        Board board,
        Player player)
    {
        // 基本防呆：必要參考不存在或步數無效就中止。
        if (enemy == null || board == null || steps <= 0)
        {
            return;
        }

        // 逐步擊退：每一步都以「當前位置」的周圍格子重新選目標。
        for (int step = 0; step < steps; step++)
        {
            Vector2Int currentPos = enemy.gridPosition;

            // 在當前位置周圍找可擊退目標；若找不到（都被妖怪卡住等）就取消後續擊退。
            if (!TryPickAdjacentKnockbackTarget(currentPos, dashDirection, board, player, out Vector2Int nextPos))
            {
                return;
            }

            // 執行一步擊退。
            enemy.MoveToPosition(nextPos);

            // 若移動系統拒絕本次位移，就停止後續擊退。
            if (enemy.gridPosition != nextPos)
            {
                return;
            }
        }
    }

    private static bool TryPickAdjacentKnockbackTarget(
        Vector2Int currentPos,
        Vector2Int preferredDirection,
        Board board,
        Player player,
        out Vector2Int targetPos)
    {
        targetPos = Vector2Int.zero;

        List<BoardTile> adjacentTiles = board.GetAdjacentTiles(currentPos);
        if (adjacentTiles == null || adjacentTiles.Count == 0)
        {
            return false;
        }

        Vector2 preferred = new Vector2(preferredDirection.x, preferredDirection.y);
        bool hasPreferredDirection = preferred.sqrMagnitude > 0.0001f;
        if (hasPreferredDirection)
        {
            preferred.Normalize();
        }

        bool found = false;
        float bestScore = float.NegativeInfinity;

        // 從周圍格中選一個可站立格；若有原衝刺方向則優先選最接近該方向的格。
        for (int i = 0; i < adjacentTiles.Count; i++)
        {
            BoardTile tile = adjacentTiles[i];
            if (tile == null)
            {
                continue;
            }

            Vector2Int candidatePos = tile.gridPosition;

            if (board.IsTileOccupied(candidatePos))
            {
                continue;
            }

            if (player != null && player.position == candidatePos)
            {
                continue;
            }

            float score = 0f;
            if (hasPreferredDirection)
            {
                Vector2 candidateDir = new Vector2(candidatePos.x - currentPos.x, candidatePos.y - currentPos.y);
                if (candidateDir.sqrMagnitude > 0.0001f)
                {
                    candidateDir.Normalize();
                    score = Vector2.Dot(preferred, candidateDir);
                }
            }

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                targetPos = candidatePos;
            }
        }

        return found;
    }
}
