using System.Collections.Generic;

public static class CardDisplaySortUtility
{
    public readonly struct OrderedCardEntry
    {
        public OrderedCardEntry(CardBase card, int sourceIndex)
        {
            Card = card;
            SourceIndex = sourceIndex;
        }

        public CardBase Card { get; }
        public int SourceIndex { get; }
    }

    public static List<OrderedCardEntry> BuildOrderedEntries(IReadOnlyList<CardBase> source)
    {
        List<OrderedCardEntry> ordered = new List<OrderedCardEntry>();
        if (source == null)
        {
            return ordered;
        }

        for (int i = 0; i < source.Count; i++)
        {
            CardBase card = source[i];
            if (card != null)
            {
                ordered.Add(new OrderedCardEntry(card, i));
            }
        }

        ordered.Sort(CompareOrderedEntries);
        return ordered;
    }

    public static List<CardBase> BuildOrderedCards(IReadOnlyList<CardBase> source)
    {
        List<OrderedCardEntry> orderedEntries = BuildOrderedEntries(source);
        List<CardBase> orderedCards = new List<CardBase>(orderedEntries.Count);

        for (int i = 0; i < orderedEntries.Count; i++)
        {
            orderedCards.Add(orderedEntries[i].Card);
        }

        return orderedCards;
    }

    public static int CompareOrderedEntries(OrderedCardEntry left, OrderedCardEntry right)
    {
        int categoryCompare = GetCardCategorySortRank(left.Card).CompareTo(GetCardCategorySortRank(right.Card));
        if (categoryCompare != 0)
        {
            return categoryCompare;
        }

        if (left.Card != null &&
            right.Card != null &&
            left.Card.cardType == CardType.Attack &&
            right.Card.cardType == CardType.Attack)
        {
            int elementCompare = GetAttackElementSortRank(left.Card).CompareTo(GetAttackElementSortRank(right.Card));
            if (elementCompare != 0)
            {
                return elementCompare;
            }
        }

        int nameCompare = string.Compare(GetCardSortName(left.Card), GetCardSortName(right.Card), System.StringComparison.Ordinal);
        if (nameCompare != 0)
        {
            return nameCompare;
        }

        int assetNameCompare = string.Compare(GetAssetSortName(left.Card), GetAssetSortName(right.Card), System.StringComparison.Ordinal);
        if (assetNameCompare != 0)
        {
            return assetNameCompare;
        }

        return left.SourceIndex.CompareTo(right.SourceIndex);
    }

    public static int GetCardCategorySortRank(CardBase card)
    {
        if (card == null)
        {
            return int.MaxValue;
        }

        return card.cardType switch
        {
            CardType.Attack => 0,
            CardType.Skill => 1,
            CardType.Movement => 2,
            _ => 3
        };
    }

    public static int GetAttackElementSortRank(CardBase card)
    {
        if (card == null || !card.TryGetElementType(out ElementType elementType))
        {
            return int.MaxValue;
        }

        return elementType switch
        {
            ElementType.Fire => 0,
            ElementType.Water => 1,
            ElementType.Thunder => 2,
            ElementType.Ice => 3,
            ElementType.Wood => 4,
            _ => 5
        };
    }

    public static string GetCardSortName(CardBase card)
    {
        return card != null && !string.IsNullOrWhiteSpace(card.cardName)
            ? card.cardName.Trim()
            : string.Empty;
    }

    public static string GetAssetSortName(CardBase card)
    {
        return card != null && !string.IsNullOrWhiteSpace(card.name)
            ? card.name.Trim()
            : string.Empty;
    }
}
