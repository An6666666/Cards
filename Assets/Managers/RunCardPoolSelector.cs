using System.Collections.Generic;
using UnityEngine;

public static class RunCardPoolSelector
{
    public const float DefaultShopOtherElementChance = 0.1f;

    public static List<CardBase> GetRewardChoices(IEnumerable<CardBase> source, int count)
    {
        CardPoolLayers layers = BuildCardPoolLayers(source);
        return DrawRandomCards(layers.primaryPool, count);
    }

    public static List<CardBase> GetShopChoices(
        IEnumerable<CardBase> source,
        int attackCount,
        int skillCount,
        int movementCount,
        float otherElementChance = DefaultShopOtherElementChance)
    {
        ShopCardPools pools = BuildShopCardPools(source);
        List<CardBase> result = new List<CardBase>();

        result.AddRange(DrawAttackShopCards(
            pools.attackPrimaryPool,
            pools.attackOtherElementPool,
            attackCount,
            otherElementChance));
        result.AddRange(DrawCardsWithReplacement(pools.skillPool, skillCount));
        result.AddRange(DrawCardsWithReplacement(pools.movementPool, movementCount));

        Shuffle(result);
        return result;
    }

    public static CardPoolLayers BuildCardPoolLayers(IEnumerable<CardBase> source)
    {
        List<CardBase> primaryPool = new List<CardBase>();
        List<CardBase> otherElementPool = new List<CardBase>();
        HashSet<ElementType> selectedElements = GetSelectedElements();
        bool useElementFiltering = selectedElements.Count > 0;

        if (source == null)
            return new CardPoolLayers(primaryPool, otherElementPool);

        foreach (CardBase card in source)
        {
            if (card == null || card.cardType == CardType.Relic)
                continue;

            if (card.cardType == CardType.Skill || card.cardType == CardType.Movement)
            {
                AddIfMissing(primaryPool, card);
                continue;
            }

            if (card.cardType != CardType.Attack || !useElementFiltering)
            {
                AddIfMissing(primaryPool, card);
                continue;
            }

            if (!card.TryGetElementType(out ElementType elementType) || selectedElements.Contains(elementType))
            {
                AddIfMissing(primaryPool, card);
                continue;
            }

            AddIfMissing(otherElementPool, card);
        }

        return new CardPoolLayers(primaryPool, otherElementPool);
    }

    public readonly struct CardPoolLayers
    {
        public CardPoolLayers(List<CardBase> primaryPool, List<CardBase> otherElementPool)
        {
            this.primaryPool = primaryPool ?? new List<CardBase>();
            this.otherElementPool = otherElementPool ?? new List<CardBase>();
        }

        public readonly List<CardBase> primaryPool;
        public readonly List<CardBase> otherElementPool;
    }

    public readonly struct ShopCardPools
    {
        public ShopCardPools(
            List<CardBase> attackPrimaryPool,
            List<CardBase> attackOtherElementPool,
            List<CardBase> skillPool,
            List<CardBase> movementPool)
        {
            this.attackPrimaryPool = attackPrimaryPool ?? new List<CardBase>();
            this.attackOtherElementPool = attackOtherElementPool ?? new List<CardBase>();
            this.skillPool = skillPool ?? new List<CardBase>();
            this.movementPool = movementPool ?? new List<CardBase>();
        }

        public readonly List<CardBase> attackPrimaryPool;
        public readonly List<CardBase> attackOtherElementPool;
        public readonly List<CardBase> skillPool;
        public readonly List<CardBase> movementPool;
    }

    public static ShopCardPools BuildShopCardPools(IEnumerable<CardBase> source)
    {
        List<CardBase> attackPrimaryPool = new List<CardBase>();
        List<CardBase> attackOtherElementPool = new List<CardBase>();
        List<CardBase> skillPool = new List<CardBase>();
        List<CardBase> movementPool = new List<CardBase>();
        HashSet<ElementType> selectedElements = GetSelectedElements();
        bool useElementFiltering = selectedElements.Count > 0;

        if (source == null)
        {
            return new ShopCardPools(
                attackPrimaryPool,
                attackOtherElementPool,
                skillPool,
                movementPool);
        }

        foreach (CardBase card in source)
        {
            if (card == null || card.cardType == CardType.Relic)
                continue;

            switch (card.cardType)
            {
                case CardType.Attack:
                    if (!useElementFiltering ||
                        !card.TryGetElementType(out ElementType elementType) ||
                        selectedElements.Contains(elementType))
                    {
                        AddIfMissing(attackPrimaryPool, card);
                    }
                    else
                    {
                        AddIfMissing(attackOtherElementPool, card);
                    }
                    break;

                case CardType.Skill:
                    AddIfMissing(skillPool, card);
                    break;

                case CardType.Movement:
                    AddIfMissing(movementPool, card);
                    break;
            }
        }

        return new ShopCardPools(
            attackPrimaryPool,
            attackOtherElementPool,
            skillPool,
            movementPool);
    }

    private static HashSet<ElementType> GetSelectedElements()
    {
        if (!StartingDeckSelection.TryGetRunSelectedElements(out IReadOnlyList<ElementType> selectedElements) ||
            selectedElements == null ||
            selectedElements.Count == 0)
        {
            return new HashSet<ElementType>();
        }

        return new HashSet<ElementType>(selectedElements);
    }

    private static List<CardBase> DrawRandomCards(List<CardBase> pool, int count)
    {
        List<CardBase> result = new List<CardBase>();
        if (pool == null || pool.Count == 0)
            return result;

        List<CardBase> remaining = new List<CardBase>(pool);
        int targetCount = count <= 0 ? remaining.Count : Mathf.Min(count, remaining.Count);

        for (int i = 0; i < targetCount && remaining.Count > 0; i++)
        {
            int index = Random.Range(0, remaining.Count);
            result.Add(remaining[index]);
            remaining.RemoveAt(index);
        }

        return result;
    }

    private static List<CardBase> DrawAttackShopCards(
        List<CardBase> primaryPool,
        List<CardBase> otherElementPool,
        int count,
        float otherElementChance)
    {
        List<CardBase> primary = primaryPool ?? new List<CardBase>();
        List<CardBase> otherElements = otherElementPool ?? new List<CardBase>();
        List<CardBase> result = new List<CardBase>();
        int targetCount = Mathf.Max(0, count);
        float clampedChance = Mathf.Clamp01(otherElementChance);

        while (result.Count < targetCount && (primary.Count > 0 || otherElements.Count > 0))
        {
            List<CardBase> selectedPool = primary;
            if (otherElements.Count > 0 && Random.value < clampedChance)
            {
                selectedPool = otherElements;
            }

            if (selectedPool.Count == 0)
            {
                selectedPool = primary.Count > 0 ? primary : otherElements;
            }

            if (selectedPool.Count == 0)
                break;

            int index = Random.Range(0, selectedPool.Count);
            result.Add(selectedPool[index]);
        }

        return result;
    }

    private static List<CardBase> DrawCardsWithReplacement(List<CardBase> pool, int count)
    {
        List<CardBase> result = new List<CardBase>();
        if (pool == null || pool.Count == 0 || count <= 0)
            return result;

        int targetCount = Mathf.Max(0, count);
        for (int i = 0; i < targetCount; i++)
        {
            int index = Random.Range(0, pool.Count);
            result.Add(pool[index]);
        }

        return result;
    }

    private static void Shuffle(List<CardBase> cards)
    {
        if (cards == null || cards.Count <= 1)
            return;

        for (int i = cards.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            CardBase temp = cards[i];
            cards[i] = cards[swapIndex];
            cards[swapIndex] = temp;
        }
    }

    private static void AddIfMissing(List<CardBase> target, CardBase card)
    {
        if (target == null || card == null || target.Contains(card))
            return;

        target.Add(card);
    }
}
