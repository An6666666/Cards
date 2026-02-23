using UnityEngine;

public readonly struct AttackSelectionRequest
{
    public CardBase Card { get; }
    public int LockedCost { get; }
    public float SourceTimestamp { get; }

    public AttackSelectionRequest(CardBase card, int lockedCost, float sourceTimestamp)
    {
        Card = card;
        LockedCost = Mathf.Max(0, lockedCost);
        SourceTimestamp = sourceTimestamp;
    }
}

