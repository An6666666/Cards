using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackSelectionController
{
    private readonly Player player;
    private readonly Board board;
    private readonly BattleHandUIController handUIController;
    private readonly MonoBehaviour coroutineHost;
    private readonly IEnemyQueryService enemyQueryService;

    private bool isSelectingAttackTarget;
    private bool isResolvingAttack;
    private AttackSelectionRequest currentRequest;

    private readonly List<Enemy> validEnemies = new List<Enemy>();
    private readonly List<Enemy> areaTargetBuffer = new List<Enemy>();
    private readonly List<Enemy> areaHighlightedEnemies = new List<Enemy>();
    private readonly List<BoardTile> highlightedTiles = new List<BoardTile>();
    private Enemy currentHighlightedEnemy;

    private static readonly List<Vector2Int> DefaultOffsets = new List<Vector2Int>
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    private readonly struct PendingAttackExecution
    {
        public readonly CardBase Card;
        public readonly Enemy Target;
        public readonly int LockedCost;
        public readonly float SourceTimestamp;

        public PendingAttackExecution(CardBase card, Enemy target, int lockedCost, float sourceTimestamp)
        {
            Card = card;
            Target = target;
            LockedCost = lockedCost;
            SourceTimestamp = sourceTimestamp;
        }
    }

    public AttackSelectionController(
        Player player,
        Board board,
        BattleHandUIController handUIController,
        MonoBehaviour coroutineHost,
        IEnemyQueryService enemyQueryService)
    {
        this.player = player;
        this.board = board;
        this.handUIController = handUIController;
        this.coroutineHost = coroutineHost;
        this.enemyQueryService = enemyQueryService;
    }

    public void StartAttackSelect(AttackSelectionRequest request)
    {
        if (isResolvingAttack || request.Card == null || player == null)
        {
            return;
        }

        if (player.energy < request.LockedCost)
        {
            Debug.Log("Not enough energy");
            return;
        }

        EndAttackSelect();

        isSelectingAttackTarget = true;
        currentRequest = request;

        IReadOnlyList<Vector2Int> offsets = GetOffsets(request.Card);
        CacheValidEnemiesWithOffsets(player.position, offsets);
        HighlightTilesWithOffsets(player.position, offsets);
    }

    public bool OnEnemyClicked(Enemy enemy)
    {
        if (!isSelectingAttackTarget || currentRequest.Card == null)
        {
            return false;
        }

        if (!IsValidTarget(enemy))
        {
            return false;
        }

        PendingAttackExecution execution = new PendingAttackExecution(
            currentRequest.Card,
            enemy,
            currentRequest.LockedCost,
            currentRequest.SourceTimestamp);

        isSelectingAttackTarget = false;
        currentRequest = default;
        ClearCurrentEnemyHighlight();
        ClearRangeHighlights();
        validEnemies.Clear();

        isResolvingAttack = true;
        coroutineHost.StartCoroutine(AttackRoutine(execution, 0.2f));
        return true;
    }

    public void UpdateAttackHover(Vector2 worldPosition)
    {
        if (!isSelectingAttackTarget)
        {
            return;
        }

        Collider2D hit = Physics2D.OverlapPoint(worldPosition);
        Enemy targetEnemy = hit != null ? hit.GetComponentInParent<Enemy>() : null;

        if (IsValidTarget(targetEnemy))
        {
            SetCurrentEnemyHighlight(targetEnemy);
        }
        else
        {
            ClearCurrentEnemyHighlight();
        }
    }

    public void EndAttackSelect()
    {
        isSelectingAttackTarget = false;
        currentRequest = default;
        ClearCurrentEnemyHighlight();
        validEnemies.Clear();
        ClearRangeHighlights();
    }

    private IEnumerator AttackRoutine(PendingAttackExecution execution, float hitDelay)
    {
        if (player == null || execution.Card == null)
        {
            isResolvingAttack = false;
            yield break;
        }

        player.PlayAttackAnim();
        yield return new WaitForSeconds(hitDelay);

        Enemy target = execution.Target;
        bool targetAlive = enemyQueryService != null
            ? enemyQueryService.IsAlive(target)
            : target != null && target.currentHP > 0 && !target.IsDead;

        List<ElementType> targetElementsBefore = null;
        List<ElementType> targetElementsAfter = null;
        Enemy contextTarget = null;

        if (targetAlive)
        {
            contextTarget = target;
            FaceUtils.Face(player.gameObject, target.transform);
            targetElementsBefore = new List<ElementType>(target.GetElementTags());
            execution.Card.ExecuteEffect(player, target);
            targetElementsAfter = new List<ElementType>(target.GetElementTags());
        }

        GameEvents.RaiseCardPlayed(execution.Card);
        GameEvents.RaiseCardPlayedWithContext(
            new CardPlayContext(execution.Card, contextTarget, targetElementsBefore, targetElementsAfter));

        ConsumeCardAndEnergy(execution.Card, execution.LockedCost);
        handUIController.UpdateHandMetaUI();

        isResolvingAttack = false;
    }

    private void ConsumeCardAndEnergy(CardBase card, int lockedCost)
    {
        bool removedFromHand = player.Hand.Remove(card);
        if (removedFromHand)
        {
            player.ClearCardCostModifier(card);
            if (card.exhaustOnUse)
            {
                player.ExhaustCard(card);
            }
            else
            {
                player.discardPile.Add(card);
            }
        }

        player.UseEnergy(Mathf.Max(0, lockedCost));
    }

    private IReadOnlyList<Vector2Int> GetOffsets(CardBase card)
    {
        AttackCardBase attackCard = card as AttackCardBase;
        if (attackCard != null && attackCard.rangeOffsets != null && attackCard.rangeOffsets.Count > 0)
        {
            return attackCard.rangeOffsets;
        }

        return DefaultOffsets;
    }

    private void CacheValidEnemiesWithOffsets(Vector2Int center, IReadOnlyList<Vector2Int> offsets)
    {
        validEnemies.Clear();
        enemyQueryService?.EnemiesInOffsets(center, offsets, validEnemies);
    }

    private bool IsValidTarget(Enemy enemy)
    {
        if (enemy == null || !validEnemies.Contains(enemy))
        {
            return false;
        }

        if (enemyQueryService != null)
        {
            return enemyQueryService.IsAlive(enemy);
        }

        return enemy.currentHP > 0 && !enemy.IsDead;
    }

    private void HighlightTilesWithOffsets(Vector2Int center, IReadOnlyList<Vector2Int> offsets)
    {
        ClearRangeHighlights();
        if (board == null || offsets == null)
        {
            return;
        }

        for (int i = 0; i < offsets.Count; i++)
        {
            BoardTile tile = board.GetTileAt(center + offsets[i]);
            if (tile != null)
            {
                tile.SetHighlight(true);
                highlightedTiles.Add(tile);
            }
        }
    }

    private void ClearRangeHighlights()
    {
        for (int i = 0; i < highlightedTiles.Count; i++)
        {
            BoardTile tile = highlightedTiles[i];
            if (tile != null)
            {
                tile.SetHighlight(false);
            }
        }

        highlightedTiles.Clear();
    }

    private void SetCurrentEnemyHighlight(Enemy enemy)
    {
        if (enemy == currentHighlightedEnemy)
        {
            return;
        }

        ClearCurrentEnemyHighlight();
        currentHighlightedEnemy = enemy;

        CardBase selectedCard = currentRequest.Card;
        IAreaTargetingCard areaCard = selectedCard as IAreaTargetingCard;
        if (areaCard != null && enemyQueryService != null)
        {
            areaCard.GetPreviewTargets(enemy, enemyQueryService.AliveEnemies, areaTargetBuffer);
            for (int i = 0; i < areaTargetBuffer.Count; i++)
            {
                Enemy target = areaTargetBuffer[i];
                if (target == null || !enemyQueryService.IsAlive(target))
                {
                    continue;
                }

                target.SetCardTargeted(true);
                areaHighlightedEnemies.Add(target);
            }

            areaTargetBuffer.Clear();
            return;
        }

        enemy.SetCardTargeted(true);
        areaHighlightedEnemies.Add(enemy);
    }

    private void ClearCurrentEnemyHighlight()
    {
        for (int i = 0; i < areaHighlightedEnemies.Count; i++)
        {
            Enemy enemy = areaHighlightedEnemies[i];
            if (enemy != null)
            {
                enemy.SetCardTargeted(false);
            }
        }

        areaHighlightedEnemies.Clear();
        currentHighlightedEnemy = null;
    }
}

