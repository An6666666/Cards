using UnityEngine;

[CreateAssetMenu(fileName = "Relic_QiXingJian", menuName = "Cards/Relic/\u4E03\u661F\u528D")]
public class Relic_QiXingJian : RelicBase
{
    [Header("QiXingJian Settings")]
    [Min(1)] public int stacksPerBonus = 5;
    [Min(1)] public int bonusDamagePerThreshold = 1;

    [Header("Battle State")]
    [SerializeField, HideInInspector] private int currentStarPower;
    [SerializeField, HideInInspector] private int currentJiJiBonusDamage;

    public int CurrentStarPower => currentStarPower;
    public int CurrentJiJiBonusDamage => currentJiJiBonusDamage;
    public int CurrentTotalStarPower
    {
        get
        {
            int requiredStacks = Mathf.Max(1, stacksPerBonus);
            int bonusPerThreshold = Mathf.Max(1, bonusDamagePerThreshold);
            int reachedThresholdCount = Mathf.Max(0, currentJiJiBonusDamage) / bonusPerThreshold;
            return Mathf.Max(0, currentStarPower) + reachedThresholdCount * requiredStacks;
        }
    }

    public override void OnBattleStart(Player player)
    {
        ResetBattleState();
    }

    public override void OnCardPlayed(Player player, CardBase card)
    {
        if (!(card is Attack_JiJiRuLvLing))
        {
            return;
        }

        currentStarPower += 1;

        int requiredStacks = Mathf.Max(1, stacksPerBonus);
        while (currentStarPower >= requiredStacks)
        {
            currentStarPower -= requiredStacks;
            currentJiJiBonusDamage += Mathf.Max(1, bonusDamagePerThreshold);
        }
    }

    public override int GetAdditionalDamage(Player player, CardBase card, Enemy target)
    {
        if (card is Attack_JiJiRuLvLing)
        {
            return Mathf.Max(0, currentJiJiBonusDamage);
        }

        return 0;
    }

    public override bool TryGetBattleUiCounter(out string counterText)
    {
        int totalStarPower = CurrentTotalStarPower;
        if (totalStarPower <= 0)
        {
            counterText = null;
            return false;
        }

        counterText = totalStarPower.ToString();
        return true;
    }

    private void ResetBattleState()
    {
        currentStarPower = 0;
        currentJiJiBonusDamage = 0;
    }
}
