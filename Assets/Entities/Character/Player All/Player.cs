using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerDeckController))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerBuffController))]
public class Player : MonoBehaviour
{
    [Header("手牌設定")]
    [Tooltip("每回合起始抽牌數量，會在 Inspector 中顯示，可直接調整。")]
    public int baseHandCardCount = 5;

    public List<CardBase> relics = new List<CardBase>();

    private PlayerStats stats;
    private PlayerDeckController deckController;
    private PlayerMovement movement;
    public PlayerBuffController buffs { get; private set; }

    private PlayerStats Stats => stats != null ? stats : stats = GetComponent<PlayerStats>();
    private PlayerDeckController DeckController => deckController != null ? deckController : deckController = GetComponent<PlayerDeckController>();
    private PlayerMovement Movement => movement != null ? movement : movement = GetComponent<PlayerMovement>();
    private PlayerBuffController Buffs => buffs != null ? buffs : buffs = GetComponent<PlayerBuffController>();

    public int maxHP { get => Stats.maxHP; set => Stats.maxHP = value; }
    public int currentHP { get => Stats.currentHP; set => Stats.currentHP = value; }
    public int maxEnergy { get => Stats.maxEnergy; set => Stats.maxEnergy = value; }
    public int energy { get => Stats.energy; set => Stats.energy = value; }
    public int block { get => Stats.block; set => Stats.block = value; }
    public int gold { get => Stats.gold; set => Stats.gold = value; }

    public List<CardBase> deck
    {
        get => DeckController.Deck;
        set => DeckController.Deck = value ?? new List<CardBase>();
    }
    public List<CardBase> Hand => DeckController.Hand;
    public List<CardBase> discardPile => DeckController.DiscardPile;
    public List<CardBase> exhaustPile => DeckController.ExhaustPile;
    public int exhaustCountThisTurn { get => DeckController.exhaustCountThisTurn; set => DeckController.exhaustCountThisTurn = value; }

    public Vector2Int position { get => Movement.position; set => Movement.position = value; }

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        deckController = GetComponent<PlayerDeckController>();
        movement = GetComponent<PlayerMovement>();
        buffs = GetComponent<PlayerBuffController>();

        stats.Initialize(this, buffs);
        deckController.Initialize(this, buffs);

        if (RunManager.Instance != null)
        {
            RunManager.Instance.RegisterPlayer(this);
        }
    }

    public void StartTurn()
    {
        stats.StartTurn();
        deckController.StartTurn(baseHandCardCount);
        buffs.OnTurnStartReset();

        foreach (CardBase r in relics)
        {
            if (r is Relic_KuMuShuQian kk)
            {
                kk.OnTurnStart(this);
            }
        }
    }

    public void EndTurn()
    {
        if (buffs.needRandomDiscardAtEnd > 0)
        {
            int n = buffs.needRandomDiscardAtEnd;
            buffs.needRandomDiscardAtEnd = 0;
            for (int i = 0; i < n; i++)
            {
                if (deckController.DiscardRandomCard(relics, false))
                {
                    continue;
                }
                else
                {
                    break;
                }
            }
        }

        buffs.OnTurnEndReset(this);
    }

    public void UseEnergy(int cost) => stats.UseEnergy(cost);

    public int CalculateAttackDamage(int baseDamage) => stats.CalculateAttackDamage(baseDamage);

    public void AddBlock(int amount) => stats.AddBlock(amount, relics);

    public void TakeDamage(int dmg) => stats.TakeDamage(dmg);

    public void TakeDamageDirect(int dmg) => stats.TakeDamageDirect(dmg);

    public void TakeStatusDamage(int dmg) => stats.TakeStatusDamage(dmg);

    public void AddGold(int amount) => stats.AddGold(amount);

    public void RemoveAllNegativeEffects()
    {
        buffs.RemoveAllNegativeEffects(this);
    }

    public void RemoveEnemyNegativeEffects()
    {
        buffs.RemoveEnemyNegativeEffects(this);
    }

    public void DrawCards(int n) => deckController.DrawCards(n);

    public int GetCardCostModifier(CardBase card) => deckController.GetCardCostModifier(card);

    public void AddCardCostModifier(CardBase card, int modifier) => deckController.AddCardCostModifier(card, modifier);

    public void ClearCardCostModifier(CardBase card) => deckController.ClearCardCostModifier(card);

    public void ExhaustCard(CardBase card) => deckController.ExhaustCard(card);

    public void DrawNewHand(int count) => deckController.DrawNewHand(count);

    public void ReshuffleDiscardIntoDeck() => deckController.ReshuffleDiscardIntoDeck();

    public void ShuffleDeck() => deckController.ShuffleDeck();

    public bool HasExhaustableCardInHand(CardBase excludedCard = null) => deckController.HasExhaustableCardInHand(excludedCard);

    public void DiscardCards(int n) => deckController.DiscardCards(n, relics);

    public bool DiscardOneCard() => deckController.DiscardOneCard(relics);

    public bool DiscardRandomCard() => deckController.DiscardRandomCard(relics);

    public bool ExhaustRandomCardFromHand(CardBase excludedCard = null) => deckController.ExhaustRandomCardFromHand(excludedCard);

    public bool CheckExhaustPlan() => deckController.CheckExhaustPlan();

    public void MoveToPosition(Vector2Int targetGridPos) => movement.MoveToPosition(targetGridPos);

    public void TeleportToPosition(Vector2Int targetPos) => movement.TeleportToPosition(targetPos);
}