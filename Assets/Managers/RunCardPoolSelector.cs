using System.Collections.Generic;
using UnityEngine;

public static class RunCardPoolSelector
{
    public const float DefaultShopOtherElementChance = 0.2f;

    public static List<CardBase> GetRewardChoices(IEnumerable<CardBase> source, int count)
    {
        CardPoolLayers layers = BuildCardPoolLayers(source);
        return DrawRandomCards(layers.primaryPool, count);
    }

    public static List<CardBase> GetShopChoices(
        IEnumerable<CardBase> source,
        int count,
        float otherElementChance = DefaultShopOtherElementChance)
    {
        CardPoolLayers layers = BuildCardPoolLayers(source);
        return DrawShopCards(layers.primaryPool, layers.otherElementPool, count, otherElementChance);
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

    private static List<CardBase> DrawShopCards(
        List<CardBase> primaryPool,
        List<CardBase> otherElementPool,
        int count,
        float otherElementChance)
    {
        List<CardBase> primary = primaryPool != null ? new List<CardBase>(primaryPool) : new List<CardBase>();
        List<CardBase> otherElements = otherElementPool != null ? new List<CardBase>(otherElementPool) : new List<CardBase>();

        if (count <= 0)
        {
            List<CardBase> allCards = new List<CardBase>(primary);
            AddRandomSubset(otherElements, allCards, otherElementChance);
            return allCards;
        }

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
            selectedPool.RemoveAt(index);
        }

        return result;
    }

    private static void AddRandomSubset(List<CardBase> source, List<CardBase> target, float chance)
    {
        if (source == null || target == null || source.Count == 0)
            return;

        float clampedChance = Mathf.Clamp01(chance);
        for (int i = 0; i < source.Count; i++)
        {
            if (Random.value <= clampedChance)
            {
                AddIfMissing(target, source[i]);
            }
        }
    }

    private static void AddIfMissing(List<CardBase> target, CardBase card)
    {
        if (target == null || card == null || target.Contains(card))
            return;

        target.Add(card);
    }
}
