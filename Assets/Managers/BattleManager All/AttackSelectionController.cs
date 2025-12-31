using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackSelectionController
{
    private readonly Player player;
    private readonly Board board;
    private readonly BattleHandUIController handUIController;
    private readonly MonoBehaviour coroutineHost;

    private bool isSelectingAttackTarget = false;
    private CardBase currentAttackCard = null;

    private readonly List<Enemy> validEnemies = new List<Enemy>();
    private Enemy currentHighlightedEnemy = null;
    private readonly List<Enemy> areaHighlightedEnemies = new List<Enemy>();
    private readonly List<BoardTile> highlightedTiles = new List<BoardTile>();

    // =========================
    // Constructor（一定要有）
    // =========================
    public AttackSelectionController(
        Player player,
        Board board,
        BattleHandUIController handUIController,
        MonoBehaviour coroutineHost)
    {
        this.player = player;
        this.board = board;
        this.handUIController = handUIController;
        this.coroutineHost = coroutineHost;
    }

    // =========================
    // 開始選取攻擊目標
    // =========================
    public void StartAttackSelect(CardBase attackCard)
    {
        int finalCost = attackCard.cost
                        + player.GetCardCostModifier(attackCard)
                        + player.buffs.nextAttackCostModify;
        finalCost = Mathf.Max(0, finalCost);

        if (player.energy < finalCost)
        {
            Debug.Log("Not enough energy");
            return;
        }

        isSelectingAttackTarget = true;
        currentAttackCard = attackCard;

        AttackCardBase aCard = attackCard as AttackCardBase;
        List<Vector2Int> offsets = (aCard != null && aCard.rangeOffsets?.Count > 0)
            ? aCard.rangeOffsets
            : new List<Vector2Int>
            {
                new Vector2Int(1,0), new Vector2Int(-1,0),
                new Vector2Int(0,1), new Vector2Int(0,-1),
                new Vector2Int(1,1), new Vector2Int(1,-1),
                new Vector2Int(-1,1), new Vector2Int(-1,-1)
            };

        CacheValidEnemiesWithOffsets(player.position, offsets);
        HighlightTilesWithOffsets(player.position, offsets);
    }

    // =========================
    // 點擊敵人（攻擊）
    // =========================
    public bool OnEnemyClicked(Enemy e)
    {
        if (!isSelectingAttackTarget) return false;
        if (!validEnemies.Contains(e)) return false;
        if (currentAttackCard == null) return false;

        isSelectingAttackTarget = false;
        ClearCurrentEnemyHighlight();
        ClearRangeHighlights();

        coroutineHost.StartCoroutine(AttackRoutine(e, 0.2f));
        return true;
    }

    // =========================
    // 攻擊動畫 → 命中 → 結算
    // =========================
    private IEnumerator AttackRoutine(Enemy e, float hitDelay)
    {
        if (player == null || currentAttackCard == null || e == null)
        yield break;

        // 播玩家攻擊動畫
        player.PlayAttackAnim();

        yield return new WaitForSeconds(hitDelay);

        // 真正執行攻擊效果
        if (currentAttackCard != null)
        currentAttackCard.ExecuteEffect(player, e);
        else
        yield break;

        int finalCost = currentAttackCard.cost
                        + player.GetCardCostModifier(currentAttackCard)
                        + player.buffs.nextAttackCostModify;
        finalCost = Mathf.Max(0, finalCost);

        if (player.Hand.Remove(currentAttackCard))
            player.ClearCardCostModifier(currentAttackCard);

        if (currentAttackCard.exhaustOnUse)
            player.ExhaustCard(currentAttackCard);
        else
            player.discardPile.Add(currentAttackCard);

        player.UseEnergy(finalCost);

        currentAttackCard = null;
        validEnemies.Clear();

        handUIController.RefreshHandUI();
    }

    // =========================
    // Hover 高亮
    // =========================
    public void UpdateAttackHover(Vector2 worldPosition)
    {
        if (!isSelectingAttackTarget) return;

        Collider2D hit = Physics2D.OverlapPoint(worldPosition);
        Enemy targetEnemy = hit != null ? hit.GetComponentInParent<Enemy>() : null;

        if (targetEnemy != null && validEnemies.Contains(targetEnemy))
            SetCurrentEnemyHighlight(targetEnemy);
        else
            ClearCurrentEnemyHighlight();
    }

    // =========================
    // 高亮處理
    // =========================
    private void CacheValidEnemiesWithOffsets(Vector2Int center, List<Vector2Int> offsets)
    {
        validEnemies.Clear();
        Enemy[] all = Object.FindObjectsOfType<Enemy>();

        foreach (var off in offsets)
        {
            Vector2Int pos = center + off;
            foreach (var e in all)
            {
                if (e.gridPosition == pos && !validEnemies.Contains(e))
                    validEnemies.Add(e);
            }
        }
    }

    private void HighlightTilesWithOffsets(Vector2Int center, List<Vector2Int> offsets)
    {
        ClearRangeHighlights();
        if (board == null) return;

        foreach (var off in offsets)
        {
            BoardTile tile = board.GetTileAt(center + off);
            if (tile != null)
            {
                tile.SetHighlight(true);
                highlightedTiles.Add(tile);
            }
        }
    }

    private void ClearRangeHighlights()
    {
        foreach (var tile in highlightedTiles)
            tile.SetHighlight(false);

        highlightedTiles.Clear();
    }

    private void SetCurrentEnemyHighlight(Enemy enemy)
    {
        if (enemy == currentHighlightedEnemy) return;

        ClearCurrentEnemyHighlight();
        currentHighlightedEnemy = enemy;

        Attack_TianFa tianFa = currentAttackCard as Attack_TianFa;
        if (tianFa != null)
        {
            HighlightAreaTargets(enemy, tianFa.effectRadius);
        }
        else
        {
            enemy.SetCardTargeted(true);
            areaHighlightedEnemies.Add(enemy);
        }
    }

    private void ClearCurrentEnemyHighlight()
    {
        foreach (Enemy e in areaHighlightedEnemies)
            if (e != null) e.SetCardTargeted(false);

        areaHighlightedEnemies.Clear();
        currentHighlightedEnemy = null;
    }

    private void HighlightAreaTargets(Enemy centerEnemy, float radius)
    {
        if (centerEnemy == null) return;

        Vector2Int center = centerEnemy.gridPosition;
        Enemy[] allEnemies = Object.FindObjectsOfType<Enemy>();

        foreach (Enemy target in allEnemies)
        {
            if (target == null) continue;

            float dist = Vector2Int.Distance(center, target.gridPosition);
            if (target != centerEnemy && dist > radius) continue;

            target.SetCardTargeted(true);
            areaHighlightedEnemies.Add(target);
        }
    }

    // =========================
    // 強制結束選取（保留）
    // =========================
    public void EndAttackSelect()
    {
        isSelectingAttackTarget = false;
        currentAttackCard = null;
        ClearCurrentEnemyHighlight();
        validEnemies.Clear();
        ClearRangeHighlights();
    }
}