using System.Collections.Generic;
using UnityEngine;

public class EnemySkillCooldownProviderAdapter : MonoBehaviour, IEnemySkillCooldownProvider
{
    [Header("Read cooldown from (optional)")]
    [Tooltip("不指定就會自動找 Parent 的 IEnemyCooldownProvider（通常是 Enemy 子類）")]
    [SerializeField] private MonoBehaviour cooldownProviderSource;

    [Header("Skill Icons (order = slot index)")]
    [Tooltip("可留空；若不夠長就視為該 slot 沒有 icon")]
    [SerializeField] private List<Sprite> skillIcons = new();

    [Header("Display")]
    [Tooltip("即使 icon 是空的也要顯示 slot（只顯示數字）")]
    [SerializeField] private bool showSlotsEvenIfNoIcon = true;

    private IEnemyCooldownProvider provider;

    private void Awake()
    {
        ResolveProvider();
    }

    private void OnEnable()
    {
        ResolveProvider();
    }

    private void ResolveProvider()
    {
        if (cooldownProviderSource != null)
        {
            provider = cooldownProviderSource as IEnemyCooldownProvider;
        }

        if (provider == null)
        {
            provider = GetComponentInParent<IEnemyCooldownProvider>();
        }
    }

    public List<(Sprite icon, int cd)> GetSkillCooldowns()
    {
        ResolveProvider();

        var result = new List<(Sprite icon, int cd)>();

        if (provider == null || provider.CooldownSlotCount <= 0)
            return result;

        int count = provider.CooldownSlotCount;

        // ✅ 重點：就算沒有 icon 也照 slot 數產生（符合你需求）
        for (int i = 0; i < count; i++)
        {
            int cd = Mathf.Max(0, provider.GetCooldownTurnsRemaining(i));

            Sprite icon = null;
            if (i >= 0 && i < skillIcons.Count)
                icon = skillIcons[i];

            if (!showSlotsEvenIfNoIcon && icon == null)
                continue;

            result.Add((icon, cd));
        }

        return result;
    }
}
