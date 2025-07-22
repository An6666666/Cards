using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public static class GameEvents
{
    public static event Action<CardBase> CardPlayed;
    public static event Action TurnEnded;

    public static void RaiseCardPlayed(CardBase card)
    {
        CardPlayed?.Invoke(card);
    }

    public static void RaiseTurnEnded()
    {
        TurnEnded?.Invoke();
    }
}
