using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public static class GameEvents
{
    public static event Action<CardBase> CardPlayed;
    public static event Action<CardPlayContext> CardPlayedWithContext;
    public static event Action TurnEnded;

    public static void RaiseCardPlayed(CardBase card)
    {
        CardPlayed?.Invoke(card);
    }
    public static void RaiseCardPlayedWithContext(CardPlayContext context)
    {
        CardPlayedWithContext?.Invoke(context);
    }
    public static void RaiseTurnEnded()
    {
        TurnEnded?.Invoke();
    }
}
public class CardPlayContext
{
    public CardBase Card { get; }
    public Enemy Target { get; }
    public IReadOnlyList<ElementType> TargetElementsBefore { get; }
    public IReadOnlyList<ElementType> TargetElementsAfter { get; }

    public CardPlayContext(
        CardBase card,
        Enemy target,
        IReadOnlyList<ElementType> targetElementsBefore,
        IReadOnlyList<ElementType> targetElementsAfter)
    {
        Card = card;
        Target = target;
        TargetElementsBefore = targetElementsBefore;
        TargetElementsAfter = targetElementsAfter;
    }

    public bool TryGetElementType(out ElementType elementType)
    {
        if (Card != null)
        {
            return Card.TryGetElementType(out elementType);
        }

        elementType = default;
        return false;
    }
}
