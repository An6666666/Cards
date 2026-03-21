using UnityEngine;

public readonly struct EnemyBattleSkillDisplayData
{
    public EnemyBattleSkillDisplayData(Sprite icon, int cooldown, bool showCooldown)
    {
        Icon = icon;
        Cooldown = Mathf.Max(0, cooldown);
        ShowCooldown = showCooldown;
    }

    public Sprite Icon { get; }
    public int Cooldown { get; }
    public bool ShowCooldown { get; }
}
