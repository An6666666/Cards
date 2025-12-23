using UnityEngine;

public class CardUseRouter : MonoBehaviour
{
    private CardUI cardUI;
    private BattleManager battleManager;

    public void Initialize(CardUI ui)
    {
        cardUI = ui;
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
        }
    }

    public void HandleDrag(CardBase cardData, Vector2 worldPosition)
    {
        if (cardData == null || cardData.cardType != CardType.Attack) return;
        EnsureBattleManager();

        if (battleManager != null)
            battleManager.UpdateAttackHover(worldPosition);
    }

    public bool TryHandleDrop(CardBase cardData, Collider2D hit, Vector2 worldPos)
    {
        EnsureBattleManager();

        if (cardData == null || battleManager == null)
            return false;

        bool used = false;

        if (cardData.cardType == CardType.Attack)
            used = TryUseAttack(hit);
        else if (cardData.cardType == CardType.Movement)
            used = TryUseMovement(hit);
        else if (cardData.cardType == CardType.Skill)
            used = TryUseSkill(hit);

        return used;
    }

    public void BeginConsumeFlow()
    {
        StartCoroutine(ConsumeAndRefreshThenDestroy());
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

    private bool TryUseMovement(Collider2D hit)
    {
        if (hit != null)
        {
            BoardTile tile;
            if (hit.TryGetComponent(out tile))
                return battleManager.OnTileClicked(tile);
        }
        battleManager.CancelMovementSelection();
        return false;
    }

    private bool TryUseSkill(Collider2D hit)
    {
        if (!IsCardPlayableFromHand())
            return false;

        if (hit != null)
        {
            Player playerTarget = hit.GetComponentInParent<Player>();
            if (playerTarget != null && playerTarget == battleManager.player)
            {
                return battleManager.PlayCard(cardUI.cardData);
            }
        }
        return false;
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