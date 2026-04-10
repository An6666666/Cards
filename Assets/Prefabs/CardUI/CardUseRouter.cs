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
            }
            else if (cardData.cardType == CardType.Skill)
            {
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
    }

    public bool TryHandleDrop(CardBase cardData, Collider2D hit, Vector2 worldPos)
    {
        EnsureBattleManager();

        if (cardData == null || battleManager == null)
        {
            ClearSkillTargetHighlight();
            return false;
        }

        bool used = false;

        if (cardData.cardType == CardType.Attack)
            used = TryUseAttack(hit);
        else if (cardData.cardType == CardType.Movement)
            used = TryUseMovement(hit, worldPos);
        else if (cardData.cardType == CardType.Skill)
            used = TryUseSkill(hit, worldPos);

        ClearSkillTargetHighlight();
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

    private bool TryUseAttack(Collider2D hit)
    {
        if (hit != null)
        {
            var enemy = hit.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                if (battleManager.OnEnemyClicked(enemy))
                    return true;
            }
            else
            {
                Debug.LogWarning($"[CardUseRouter] Attack drop hit {hit.name} but no Enemy found in parents.");
            }
        }
        battleManager.EndAttackSelect();
        return false;
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

        if (IsPlayerSkillTarget(hit))
        {
            return battleManager.PlayCard(cardUI.cardData);
        }

        Collider2D[] overlaps = Physics2D.OverlapPointAll(worldPos);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (IsPlayerSkillTarget(overlaps[i]))
            {
                return battleManager.PlayCard(cardUI.cardData);
            }
        }

        return false;
    }

    private bool IsPlayerSkillTarget(Collider2D hit)
    {
        if (hit == null || battleManager == null)
        {
            return false;
        }

        Player playerTarget = hit.GetComponentInParent<Player>();
        return playerTarget != null && playerTarget == battleManager.player;
    }

    private void UpdateSkillTargetHighlight(Vector2 worldPosition)
    {
        if (!IsCardPlayableFromHand())
        {
            ClearSkillTargetHighlight();
            return;
        }

        Collider2D[] overlaps = Physics2D.OverlapPointAll(worldPosition);
        Player target = null;

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider2D overlap = overlaps[i];
            if (overlap == null) continue;

            Player candidate = overlap.GetComponentInParent<Player>();
            if (candidate != null && candidate == battleManager.player)
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
