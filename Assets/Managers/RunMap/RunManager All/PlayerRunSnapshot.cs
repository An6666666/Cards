using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class PlayerRunSnapshot
{
    public int maxHP;
    public int currentHP;
    public int gold;
    public List<CardBase> deck;
    public List<CardBase> relics;
    public List<CardBase> exhaustPile;

    public static PlayerRunSnapshot Capture(Player source)
    {
        if (source == null)
            return new PlayerRunSnapshot
            {
                deck = new List<CardBase>(),
                relics = new List<CardBase>()
            };

        return new PlayerRunSnapshot
        {
            maxHP = source.maxHP,
            currentHP = source.currentHP,
            gold = source.gold,
            deck = source.deck != null ? new List<CardBase>(source.deck.Where(card => card != null)) : new List<CardBase>(),
            relics = source.relics != null ? new List<CardBase>(source.relics.Where(card => card != null)) : new List<CardBase>(),
            exhaustPile = source.exhaustPile != null ? new List<CardBase>(source.exhaustPile.Where(card => card != null)) : new List<CardBase>()
        };
    }

    public PlayerRunSnapshot Clone()
    {
        return new PlayerRunSnapshot
        {
            maxHP = this.maxHP,
            currentHP = this.currentHP,
            gold = this.gold,
            deck = this.deck != null ? new List<CardBase>(this.deck) : new List<CardBase>(),
            relics = this.relics != null ? new List<CardBase>(this.relics) : new List<CardBase>(),
            exhaustPile = this.exhaustPile != null ? new List<CardBase>(this.exhaustPile) : new List<CardBase>()
        };
    }

    public void ApplyTo(Player target)
    {
        if (target == null)
            return;

        target.maxHP = maxHP;
        target.currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        target.gold = gold;
        target.deck = deck != null ? new List<CardBase>(deck) : new List<CardBase>();
        target.relics = relics != null ? new List<CardBase>(relics) : new List<CardBase>();

        target.discardPile.Clear();
        target.Hand.Clear();
        target.block = 0;
        target.energy = target.maxEnergy;
        target.exhaustCountThisTurn = 0;
        target.buffs.ResetAll();
        target.ShuffleDeck();
    }
}
