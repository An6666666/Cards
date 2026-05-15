using System.Collections.Generic;
using UnityEngine;

public class CardUseRouter : MonoBehaviour
{
    private CardUI cardUI;
    private BattleManager battleManager;
    private PlayerSkillTargetHighlight highlightedSkillTarget;

    public void Initialize(CardUI ui)
    {
        cardUI = ui;
    }

    private void OnDisable()
    {
        ClearSkillTargetTileHighlight();
        ClearSkillTargetHighlight();
    }

    public void HandleBeginDrag(CardBase cardData, Vector2 worldPosition)
    {
        if (cardData == null) return;

        EnsureBattleManager();

        if (battleManager != null)
        {
            if (cardData.cardType == CardType.Attack)
            {
                battleManager.StartAttackSelect(cardData);
                battleManager.UpdateAttackHover(worldPosition);
            }
            else if (cardData.cardType == CardType.Movement)
            {
                battleManager.UseMovementCard(cardData);
                battleManager.UpdateMovementHover(worldPosition);
            }
            else if (cardData.cardType == CardType.Skill)
            {
                battleManager.SetSkillTargetTileHighlight(true);
                UpdateSkillTargetHighlight(worldPosition);
            }
        }
    }

    public void HandleDrag(CardBase cardData, Vector2 worldPosition)
    {
        if (cardData == null) return;
        EnsureBattleManager();

        if (battleManager == null) return;

        if (cardData.cardType == CardType.Attack)
        {
            battleManager.UpdateAttackHover(worldPosition);
        }
        else if (cardData.cardType == CardType.Skill)
        {
            UpdateSkillTargetHighlight(worldPosition);
        }
        else if (cardData.cardType == CardType.Movement)
        {
            battleManager.UpdateMovementHover(worldPosition);
        }
    }

    public bool TryHandleDrop(CardBase cardData, Collider2D hit, Vector2 worldPos)
    {
        EnsureBattleManager();

        if (cardData == null || battleManager == null)
        {
            ClearSkillTargetHighlight();
            ClearSkillTargetTileHighlight();
            return false;
        }

        bool used = false;

        if (cardData.cardType == CardType.Attack)
            used = TryUseAttack(hit, worldPos);
        else if (cardData.cardType == CardType.Movement)
            used = TryUseMovement(hit, worldPos);
        else if (cardData.cardType == CardType.Skill)
            used = TryUseSkill(hit, worldPos);

        ClearSkillTargetHighlight();
        ClearSkillTargetTileHighlight();
        return used;
    }

    public void BeginConsumeFlow()
    {
        StartCoroutine(ConsumeAndRefreshThenDestroy());
    }
    public void NotifyCardUsedUI(CardUI usedUI)
    {
        EnsureBattleManager();
        if (battleManager != null)
            battleManager.HandleCardUsedUI(usedUI);
    }
    public bool IsCardInteractionLocked()
    {
        EnsureBattleManager();
        return battleManager != null && battleManager.IsCardInteractionLocked;
    }

    private bool TryUseAttack(Collider2D hit, Vector2 worldPos)
    {
        Enemy enemy = ResolveAttackTarget(hit);
        if (enemy == null)
        {
            Collider2D[] overlaps = Physics2D.OverlapPointAll(worldPos);
            for (int i = 0; i < overlaps.Length; i++)
            {
                enemy = ResolveAttackTarget(overlaps[i]);
                if (enemy != null)
                    break;
            }
        }

        if (enemy != null && battleManager.OnEnemyClicked(enemy))
            return true;

        battleManager.EndAttackSelect();
        return false;
    }

    private Enemy ResolveAttackTarget(Collider2D hit)
    {
        if (hit == null)
            return null;

        Enemy enemy = hit.GetComponentInParent<Enemy>();
        if (IsAliveEnemy(enemy))
            return enemy;

        BoardTile tile = ResolveBoardTile(hit);
        return ResolveEnemyOnTile(tile);
    }

    private BoardTile ResolveBoardTile(Collider2D hit)
    {
        if (hit == null)
            return null;

        BoardTile tile;
        if (hit.TryGetComponent(out tile))
            return tile;

        return hit.GetComponentInParent<BoardTile>();
    }

    private Enemy ResolveEnemyOnTile(BoardTile tile)
    {
        if (tile == null || battleManager == null)
            return null;

        IReadOnlyList<Enemy> enemies = battleManager.RuntimeContext != null
            ? battleManager.RuntimeContext.Enemies
            : battleManager.enemies;

        if (enemies == null)
            return null;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (IsAliveEnemy(enemy) && enemy.gridPosition == tile.gridPosition)
                return enemy;
        }

        return null;
    }

    private bool IsAliveEnemy(Enemy enemy)
    {
        if (enemy == null)
            return false;

        if (battleManager != null && battleManager.RuntimeContext != null)
            return battleManager.RuntimeContext.IsAliveEnemy(enemy);

        return enemy.currentHP > 0 && !enemy.IsDead;
    }

    private bool TryUseMovement(Collider2D hit, Vector2 worldPos)
    {
        if (TryUseMovementHit(hit))
        {
            return true;
        }

        Collider2D[] overlaps = Physics2D.OverlapPointAll(worldPos);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (TryUseMovementHit(overlaps[i]))
            {
                return true;
            }
        }

        battleManager.CancelMovementSelection();
        return false;
    }

    private bool TryUseMovementHit(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        BoardTile tile;
        if (hit.TryGetComponent(out tile))
        {
            return battleManager.OnTileClicked(tile);
        }

        tile = hit.GetComponentInParent<BoardTile>();
        if (tile != null)
        {
            return battleManager.OnTileClicked(tile);
        }

        Enemy enemy = hit.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            return battleManager.OnEnemyClicked(enemy);
        }

        return false;
    }

    private bool TryUseSkill(Collider2D hit, Vector2 worldPos)
    {
        if (!IsCardPlayableFromHand())
            return false;

        if (ResolvePlayerSkillTarget(hit) != null)
        {
            return TryPlaySkillCard();
        }

        Collider2D[] overlaps = Physics2D.OverlapPointAll(worldPos);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (ResolvePlayerSkillTarget(overlaps[i]) != null)
            {
                return TryPlaySkillCard();
            }
        }

        return false;
    }

    private bool TryPlaySkillCard()
    {
        battleManager.MarkCardPendingConsumeUI(cardUI);
        bool played = battleManager.PlayCard(cardUI.cardData);
        if (!played)
        {
            battleManager.ClearCardPendingConsumeUI(cardUI);
        }

        return played;
    }

    private Player ResolvePlayerSkillTarget(Collider2D hit)
    {
        if (hit == null || battleManager == null)
        {
            return null;
        }

        Player playerTarget = hit.GetComponentInParent<Player>();
        if (playerTarget != null && playerTarget == battleManager.player)
        {
            return playerTarget;
        }

        BoardTile tile = ResolveBoardTile(hit);
        if (tile != null && battleManager.player != null && tile.gridPosition == battleManager.player.position)
        {
            return battleManager.player;
        }

        return null;
    }

    private void UpdateSkillTargetHighlight(Vector2 worldPosition)
    {
        if (!IsCardPlayableFromHand())
        {
            ClearSkillTargetTileHighlight();
            ClearSkillTargetHighlight();
            return;
        }

        Collider2D[] overlaps = Physics2D.OverlapPointAll(worldPosition);
        Player target = null;

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider2D overlap = overlaps[i];
            if (overlap == null) continue;

            Player candidate = ResolvePlayerSkillTarget(overlap);
            if (candidate != null)
            {
                target = candidate;
                break;
            }
        }

        if (target == null)
        {
            ClearSkillTargetHighlight();
            return;
        }

        PlayerSkillTargetHighlight nextHighlight = target.GetComponent<PlayerSkillTargetHighlight>();
        if (nextHighlight == null)
        {
            ClearSkillTargetHighlight();
            return;
        }

        battleManager.SetSkillTargetTileHighlight(true);

        if (highlightedSkillTarget != null && highlightedSkillTarget != nextHighlight)
        {
            highlightedSkillTarget.SetHighlighted(false);
        }

        highlightedSkillTarget = nextHighlight;
        highlightedSkillTarget.SetHighlighted(true);
    }

    private void ClearSkillTargetHighlight()
    {
        if (highlightedSkillTarget == null)
        {
            return;
        }

        highlightedSkillTarget.SetHighlighted(false);
        highlightedSkillTarget = null;
    }

    private void ClearSkillTargetTileHighlight()
    {
        EnsureBattleManager();
        battleManager?.SetSkillTargetTileHighlight(false);
    }

    private bool IsCardPlayableFromHand()
    {
        EnsureBattleManager();

        if (battleManager == null || cardUI == null || cardUI.cardData == null)
            return false;

        Player playerReference = battleManager.player;
        if (playerReference == null || playerReference.Hand == null)
            return false;

        return playerReference.Hand.Contains(cardUI.cardData);
    }

    private void EnsureBattleManager()
    {
        if (battleManager == null)
            battleManager = FindObjectOfType<BattleManager>();
    }

    private System.Collections.IEnumerator RefreshDeckDiscardPanelsNextFrame()
    {
        Debug.Log("[CardUseRouter] RefreshDeckDiscardPanelsNextFrame start");

        yield return null;

        Player playerRef = null;
        for (int i = 0; i < 10 && playerRef == null; i++)
        {
            EnsureBattleManager();
            if (battleManager != null) playerRef = battleManager.player;
            if (playerRef == null) playerRef = FindObjectOfType<Player>();
            if (playerRef == null) yield return null;
        }

        Debug.Log("[CardUseRouter] Bus refresh. views=" + DeckUIBus.ViewCount + ", player=" + (playerRef ? "OK" : "NULL"));
        DeckUIBus.RefreshAll(playerRef);
    }

    private System.Collections.IEnumerator ConsumeAndRefreshThenDestroy()
    {
        yield return null;
        yield return RefreshDeckDiscardPanelsNextFrame();
        Destroy(gameObject);
    }
}
