using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerDeckController))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerBuffController))]
public class Player : MonoBehaviour
{
    [Header("手牌設定")]
    public int baseHandCardCount = 5;

    public List<CardBase> relics = new List<CardBase>();

    private PlayerStats stats;
    private PlayerDeckController deckController;
    private PlayerMovement movement;
    public PlayerBuffController buffs { get; private set; }

    private PlayerAnimatorController animCtrl;

    // =========================
    // 快取元件
    // =========================
    private PlayerStats Stats => stats ??= GetComponent<PlayerStats>();
    private PlayerDeckController DeckController => deckController ??= GetComponent<PlayerDeckController>();
    private PlayerMovement Movement => movement ??= GetComponent<PlayerMovement>();

    // =========================
    // 對外動畫介面（唯一入口）
    // =========================
    public void PlayAttackAnim()  => animCtrl?.PlayAttack();
    public void PlayWoundedAnim() => animCtrl?.PlayWounded();
    public void SetMovingAnim(bool moving) => animCtrl?.SetMoving(moving);
    public void SetDeadAnim(bool dead) => animCtrl?.SetDead(dead);

    // =========================
    // 狀態轉接
    // =========================
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
    public int exhaustCountThisTurn
    {
        get => DeckController.exhaustCountThisTurn;
        set => DeckController.exhaustCountThisTurn = value;
    }

    public Vector2Int position
    {
        get => Movement.position;
        set => Movement.position = value;
    }

    // =========================
    // Unity Lifecycle
    // =========================
    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        deckController = GetComponent<PlayerDeckController>();
        movement = GetComponent<PlayerMovement>();
        buffs = GetComponent<PlayerBuffController>();

        animCtrl = GetComponentInChildren<PlayerAnimatorController>(true);

        stats.Initialize(this, buffs);
        deckController.Initialize(this, buffs);

        if (RunManager.Instance != null)
        {
            RunManager.Instance.RegisterPlayer(this);
        }
    }

    // =========================
    // Turn Flow
    // =========================
    public void StartTurn()
    {
        stats.StartTurn();
        deckController.StartTurn(baseHandCardCount);
        buffs.OnTurnStartReset();

        foreach (CardBase r in relics)
        {
            if (r is Relic_KuMuShuQian kk)
                kk.OnTurnStart(this);
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
                if (!deckController.DiscardRandomCard(relics, false))
                    break;
            }
        }

        buffs.OnTurnEndReset(this);
    }

    // =========================
    // Combat / Cards API
    // =========================
    public void RemoveAllNegativeEffects()
    {
        buffs.RemoveAllNegativeEffects(this);
    }

    public void RemoveEnemyNegativeEffects()
    {
        buffs.RemoveEnemyNegativeEffects(this);
    }


    public bool HasExhaustableCardInHand(CardBase excludedCard = null)
    => deckController.HasExhaustableCardInHand(excludedCard);
    // ===== Deck / Hand 轉接（BattleManager / 卡牌 / 遺物會用到）=====
    public void DrawNewHand(int count) => deckController.DrawNewHand(count);
    public void ReshuffleDiscardIntoDeck() => deckController.ReshuffleDiscardIntoDeck();
    public void ShuffleDeck() => deckController.ShuffleDeck();

    public void DiscardCards(int n) => deckController.DiscardCards(n, relics);
    public bool DiscardOneCard() => deckController.DiscardOneCard(relics);
    public bool DiscardRandomCard() => deckController.DiscardRandomCard(relics);

    public bool ExhaustRandomCardFromHand(CardBase excludedCard = null)
    => deckController.ExhaustRandomCardFromHand(excludedCard);

    public bool CheckExhaustPlan() => deckController.CheckExhaustPlan();

    public void UseEnergy(int cost) => stats.UseEnergy(cost);
    public int CalculateAttackDamage(int baseDamage) => stats.CalculateAttackDamage(baseDamage);
    public void AddBlock(int amount) => stats.AddBlock(amount, relics);
    public void TakeDamage(int dmg) => stats.TakeDamage(dmg);
    public void TakeDamageDirect(int dmg) => stats.TakeDamageDirect(dmg);
    public void TakeStatusDamage(int dmg) => stats.TakeStatusDamage(dmg);
    public void AddGold(int amount) => stats.AddGold(amount);

    public void DrawCards(int n) => deckController.DrawCards(n);
    public int GetCardCostModifier(CardBase card) => deckController.GetCardCostModifier(card);
    public void AddCardCostModifier(CardBase card, int modifier) => deckController.AddCardCostModifier(card, modifier);
    public void ClearCardCostModifier(CardBase card) => deckController.ClearCardCostModifier(card);
    public void ExhaustCard(CardBase card) => deckController.ExhaustCard(card);

    public void MoveToPosition(Vector2Int targetGridPos) => movement.MoveToPosition(targetGridPos);
    public void TeleportToPosition(Vector2Int targetPos) => movement.TeleportToPosition(targetPos);
}
