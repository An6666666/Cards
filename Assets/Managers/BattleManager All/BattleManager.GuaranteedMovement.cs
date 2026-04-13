using UnityEngine;

public partial class BattleManager
{
    internal void EnsureMovementCardInHand()
    {
        if (player == null)
        {
            return;
        }

        Move_YiDong movementCard = GetGuaranteedMovementCardInstance();
        if (movementCard == null)
        {
            return;
        }

        RemoveGuaranteedMovementCardFromPiles();

        int removedDuplicateCount = 0;
        for (int i = player.Hand.Count - 1; i >= 0; i--)
        {
            CardBase card = player.Hand[i];
            if (card is Move_YiDong && !ReferenceEquals(card, movementCard))
            {
                player.Hand.RemoveAt(i);
                removedDuplicateCount++;
            }
        }

        if (!player.Hand.Contains(movementCard))
        {
            player.Hand.Add(movementCard);
        }

        if (removedDuplicateCount > 0)
        {
            player.DrawCards(removedDuplicateCount);
        }
    }

    internal void DiscardAllHand()
    {
        Move_YiDong movementCard = guaranteedMovementCardInstance;
        if (movementCard != null)
        {
            player.Hand.Remove(movementCard);
            player.discardPile.Remove(movementCard);
        }

        player.discardPile.AddRange(player.Hand);
        player.Hand.Clear();

        RemoveGuaranteedMovementCardFromPiles();
        handUIController.RefreshHandUI();
    }

    internal bool IsGuaranteedMovementCard(CardBase card)
    {
        if (card == null)
        {
            return false;
        }

        Move_YiDong instance = GetGuaranteedMovementCardInstance();
        if (instance == null)
        {
            return false;
        }

        return ReferenceEquals(card, instance)
            || (guaranteedMovementCard != null && ReferenceEquals(card, guaranteedMovementCard));
    }

    internal void RemoveGuaranteedMovementCardFromPiles()
    {
        if (player == null)
        {
            return;
        }

        player.deck.RemoveAll(card => card is Move_YiDong);
        player.discardPile.RemoveAll(card => card is Move_YiDong);
    }

    private Move_YiDong GetGuaranteedMovementCardInstance()
    {
        if (guaranteedMovementCardInstance != null)
        {
            return guaranteedMovementCardInstance;
        }

        if (guaranteedMovementCard == null)
        {
            Debug.LogWarning("Guaranteed movement card template is not assigned.");
            return null;
        }

        guaranteedMovementCardInstance = Instantiate(guaranteedMovementCard);
        return guaranteedMovementCardInstance;
    }
}
