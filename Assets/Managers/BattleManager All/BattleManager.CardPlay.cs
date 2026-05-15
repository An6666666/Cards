using System.Collections.Generic;
using UnityEngine;

public partial class BattleManager
{
    public int CalculateCardEnergyCost(CardBase cardData)
    {
        if (cardData == null || player == null)
        {
            return 0;
        }

        int finalCost = cardData.cost + player.GetCardCostModifier(cardData);

        if (cardData.cardType == CardType.Attack)
        {
            finalCost += player.buffs.nextAttackCostModify;
        }

        if (cardData.cardType == CardType.Movement)
        {
            finalCost += player.buffs.movementCostModify;
        }

        return Mathf.Max(0, finalCost);
    }

    public bool PlayCard(CardBase cardData)
    {
        if (!(stateMachine.Current is PlayerTurnState))
        {
            return false;
        }

        if (cardData == null || player == null || player.Hand == null)
        {
            return false;
        }

        if (!player.Hand.Contains(cardData))
        {
            return false;
        }

        int finalCost = CalculateCardEnergyCost(cardData);
        if (player.energy < finalCost)
        {
            Debug.Log("Not enough energy");
            return false;
        }

        if (cardData is Skill_ZhiJiao && !player.HasExhaustableCardInHand(cardData))
        {
            Debug.Log("No exhaustable cards in hand for ?脩?");
            return false;
        }

        Enemy target = enemies.Find(e => e != null && e.currentHP > 0);
        if (target != null)
        {
            player.FaceTowards(target.transform);
        }

        List<ElementType> targetElementsBefore = target != null
            ? new List<ElementType>(target.GetElementTags())
            : null;

        PlayNonAttackCardAnimation(cardData);
        player.NotifyCardPlayStarted(cardData);
        cardData.ExecuteEffect(player, target);

        List<ElementType> targetElementsAfter = target != null
            ? new List<ElementType>(target.GetElementTags())
            : null;

        if (cardData.cardType == CardType.Attack && player.buffs.nextAttackPlus > 0)
        {
            player.buffs.nextAttackPlus = 0;
        }

        bool isGuaranteedMovement = IsGuaranteedMovementCard(cardData);
        bool removedFromHand = player.Hand.Remove(cardData);

        if (removedFromHand)
        {
            player.ClearCardCostModifier(cardData);

            if (isGuaranteedMovement)
            {
                RemoveGuaranteedMovementCardFromPiles();
            }
            else if (cardData.exhaustOnUse)
            {
                player.ExhaustCard(cardData);
            }
            else
            {
                player.discardPile.Add(cardData);
            }
        }
        else if (isGuaranteedMovement)
        {
            RemoveGuaranteedMovementCardFromPiles();
        }

        player.UseEnergy(finalCost);
        GameEvents.RaiseCardPlayed(cardData);
        GameEvents.RaiseCardPlayedWithContext(
            new CardPlayContext(cardData, target, targetElementsBefore, targetElementsAfter));
        player.NotifyCardPlayed(cardData);

        if (removedFromHand)
        {
            handUIController.RefreshHandUI(false);
        }

        return true;
    }

    public void HandleCardUsedUI(CardUI usedUI)
    {
        handUIController.HandleCardUsedUI(usedUI);
    }

    public void MarkCardPendingConsumeUI(CardUI pendingUI)
    {
        handUIController?.MarkCardPendingConsume(pendingUI);
    }

    public void ClearCardPendingConsumeUI(CardUI pendingUI)
    {
        handUIController?.ClearCardPendingConsume(pendingUI);
    }

    public void RefreshHandUI(bool playDrawAnimation = false)
    {
        handUIController.RefreshHandUI(playDrawAnimation);
    }

    public void RefreshHandMetaUI()
    {
        handUIController?.UpdateHandMetaUI();
    }

    private void PlayNonAttackCardAnimation(CardBase cardData)
    {
        if (player == null || cardData == null || cardData.cardType == CardType.Attack)
        {
            return;
        }

        if (IsDefendCard(cardData))
        {
            player.PlayDefendAnim();
            return;
        }

        if (IsUtilityCard(cardData))
        {
            player.PlayUtilityAnim();
        }
    }

    private static bool IsDefendCard(CardBase cardData)
    {
        return cardData is Skill_HuWoZhenShen
            || cardData is Skill_ShenLingBiYou
            || cardData is Skill_QimenDunjia
            || cardData is Skill_JinShen
            || cardData is Skill_BuMieYiZhi
            || cardData is Skill_WeiLingZhou;
    }

    private static bool IsUtilityCard(CardBase cardData)
    {
        return cardData is Skill_ZhiJiao
            || cardData is Skill_ZhaoHun
            || cardData is Skill_ShenLin
            || cardData is Skill_LingHunZhenDang;
    }
}
