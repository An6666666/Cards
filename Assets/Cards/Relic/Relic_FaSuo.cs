using UnityEngine;

[CreateAssetMenu(fileName = "Relic_FaSuo", menuName = "Cards/Relic/\u6CD5\u7D22")]
public class Relic_FaSuo : RelicBase
{
    [Header("Fa Suo Settings")]
    [Min(1)] public int requiredAttackCards = 3;
    [Min(1)] public int immobilizeTurns = 1;

    [Header("Battle State")]
    [SerializeField, HideInInspector] private int currentAttackStacks;
    [SerializeField, HideInInspector] private bool nextAttackPrimed;

    [System.NonSerialized] private AttackCardBase armedAttackCard;

    public override void OnBattleStart(Player player)
    {
        ResetBattleState();
    }

    public override void OnCardPlayStarted(Player player, CardBase card)
    {
        if (!nextAttackPrimed || card is not AttackCardBase attackCard)
        {
            return;
        }

        armedAttackCard = attackCard;
        nextAttackPrimed = false;
        currentAttackStacks = 0;
    }

    public override void OnCardPlayed(Player player, CardBase card)
    {
        if (ReferenceEquals(card, armedAttackCard))
        {
            armedAttackCard = null;
            currentAttackStacks = 0;
            return;
        }

        if (card is not AttackCardBase)
        {
            return;
        }

        currentAttackStacks += 1;
        if (currentAttackStacks >= Mathf.Max(1, requiredAttackCards))
        {
            currentAttackStacks = Mathf.Max(1, requiredAttackCards);
            nextAttackPrimed = true;
        }
    }

    public override void OnAttackCardDamagedEnemy(Player player, AttackCardBase card, Enemy target, int hpDamage)
    {
        if (!ReferenceEquals(card, armedAttackCard) || target == null || hpDamage <= 0)
        {
            return;
        }

        int appliedTurns = Mathf.Max(1, immobilizeTurns);
        target.SetImmobilizedTurns(target.immobilizedTurns + appliedTurns);
    }

    public override bool TryGetBattleUiCounter(out string counterText)
    {
        if (nextAttackPrimed)
        {
            counterText = Mathf.Max(1, requiredAttackCards).ToString();
            return true;
        }

        if (currentAttackStacks <= 0)
        {
            counterText = null;
            return false;
        }

        counterText = currentAttackStacks.ToString();
        return true;
    }

    private void ResetBattleState()
    {
        currentAttackStacks = 0;
        nextAttackPrimed = false;
        armedAttackCard = null;
    }
}
