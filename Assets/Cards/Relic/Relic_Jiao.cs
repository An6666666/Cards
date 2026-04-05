using UnityEngine;

[CreateAssetMenu(fileName = "Relic_Jiao", menuName = "Cards/Relic/筊")]
public class Relic_Jiao : RelicBase
{
    private enum JiaoOutcome
    {
        None,
        ShengJiao,
        XiaoJiao,
        YinJiao
    }

    [Header("Jiao Settings")]
    [Min(0)] public int shengJiaoMaxHpGain = 5;
    [Min(0)] public int xiaoJiaoBlockPerTurn = 4;
    [Min(0)] public int yinJiaoWeakTurns = 5;

    [Header("Battle State")]
    [SerializeField, HideInInspector] private JiaoOutcome currentOutcome = JiaoOutcome.None;

    public override void OnBattleStart(Player player)
    {
        currentOutcome = RollOutcome();

        switch (currentOutcome)
        {
            case JiaoOutcome.ShengJiao:
                ApplyShengJiao(player);
                break;

            case JiaoOutcome.XiaoJiao:
                ApplyXiaoJiao(player);
                break;

            case JiaoOutcome.YinJiao:
                ApplyYinJiao(player);
                break;
        }
    }

    public override bool TryGetBattleUiCounter(out string counterText)
    {
        counterText = GetOutcomeDisplayText(currentOutcome);
        return !string.IsNullOrWhiteSpace(counterText);
    }

    private JiaoOutcome RollOutcome()
    {
        int rolledIndex = Random.Range(0, 3);
        return rolledIndex switch
        {
            0 => JiaoOutcome.ShengJiao,
            1 => JiaoOutcome.XiaoJiao,
            _ => JiaoOutcome.YinJiao
        };
    }

    private void ApplyShengJiao(Player player)
    {
        if (player == null)
        {
            return;
        }

        int gainedMaxHp = Mathf.Max(0, shengJiaoMaxHpGain);
        if (gainedMaxHp <= 0)
        {
            return;
        }

        player.maxHP += gainedMaxHp;

        RunManager runManager = RunManager.Instance;
        PersistMaxHpGainToRunSnapshot(runManager, gainedMaxHp);
    }

    private void ApplyYinJiao(Player player)
    {
        if (player == null || player.buffs == null)
        {
            return;
        }

        int weakTurns = Mathf.Max(0, yinJiaoWeakTurns);
        if (weakTurns > 0)
        {
            player.buffs.IncreaseWeakFromPlayer(weakTurns);
        }
    }

    private void ApplyXiaoJiao(Player player)
    {
        if (player == null || player.buffs == null)
        {
            return;
        }

        int blockGain = Mathf.Max(0, xiaoJiaoBlockPerTurn);
        if (blockGain > 0)
        {
            player.buffs.blockGainAtTurnStart += blockGain;
        }
    }

    private static string GetOutcomeDisplayText(JiaoOutcome outcome)
    {
        return outcome switch
        {
            JiaoOutcome.ShengJiao => "聖筊",
            JiaoOutcome.XiaoJiao => "笑筊",
            JiaoOutcome.YinJiao => "陰筊",
            _ => null
        };
    }

    private static void PersistMaxHpGainToRunSnapshot(RunManager runManager, int gainedMaxHp)
    {
        if (runManager == null || gainedMaxHp <= 0)
        {
            return;
        }

        PlayerRunSnapshot snapshot = runManager.CurrentRunSnapshot;
        if (snapshot == null)
        {
            return;
        }

        snapshot.maxHP += gainedMaxHp;
        snapshot.currentHP = Mathf.Clamp(snapshot.currentHP, 0, snapshot.maxHP);
    }
}
