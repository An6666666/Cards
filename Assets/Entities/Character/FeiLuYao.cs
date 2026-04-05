using UnityEngine;

public class FeiLuYao : Enemy
{
    public override bool SupportsSharedSquadTactics => false;

    private enum DebuffType
    {
        Weak,
        Bleed,
        Imprison
    }

    private static readonly DebuffType[] AllDebuffTypes =
    {
        DebuffType.Weak,
        DebuffType.Bleed,
        DebuffType.Imprison
    };

    [Header("Fei Lu Yao Settings")]
    [SerializeField] private int weakDuration = 2;
    [SerializeField] private int bleedDuration = 2;
    [SerializeField] private int imprisonDuration = 1;

    protected override void Awake()
    {
        enemyName = "飛盧妖";
        base.Awake();
    }

    protected internal override void MoveOneStepTowards(Player player)
    {
    }

    public override void EnemyAction(Player player)
    {
        if (frozenTurns > 0)
        {
            SetFrozenTurns(Mathf.Max(0, frozenTurns - 1));
            return;
        }

        if (player == null)
            return;

        ApplyRandomDebuff(player);
    }

    public override void DecideNextIntent(Player player)
    {
        if (player == null || frozenTurns > 0)
        {
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        nextIntent.type = EnemyIntentType.Skill;
        nextIntent.value = 0;
        UpdateIntentIcon();
    }

    private void ApplyRandomDebuff(Player player)
    {
        DebuffType choice = AllDebuffTypes[Random.Range(0, AllDebuffTypes.Length)];
        switch (choice)
        {
            case DebuffType.Weak:
                player.buffs.ApplyWeakFromEnemy(weakDuration);
                break;
            case DebuffType.Bleed:
                player.buffs.ApplyBleedFromEnemy(bleedDuration);
                break;
            case DebuffType.Imprison:
                player.buffs.ApplyImprisonFromEnemy(imprisonDuration);
                break;
        }
    }
}
