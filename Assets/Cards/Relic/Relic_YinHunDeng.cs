using UnityEngine;

[CreateAssetMenu(fileName = "Relic_YinHunDeng", menuName = "Cards/Relic/引魂燈")]
public class Relic_YinHunDeng : RelicBase
{
    [Header("Yin Hun Deng Settings")]
    [Min(0)] public int frostStacksOnFreezeSuccess = 1;
    [Min(0)] public int frostStacksOnBossFreezeFail = 2;

    public override void OnFreezeReactionResolved(Player player, Enemy target, bool freezeApplied)
    {
        if (player == null || target == null)
        {
            return;
        }

        int frostStacksToAdd = freezeApplied
            ? Mathf.Max(0, frostStacksOnFreezeSuccess)
            : (target.isBoss ? Mathf.Max(0, frostStacksOnBossFreezeFail) : 0);

        if (frostStacksToAdd <= 0)
        {
            return;
        }

        target.AddFrostStacks(frostStacksToAdd);
    }
}
