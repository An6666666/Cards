using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemySkillCooldownProviderAdapter : MonoBehaviour, IEnemySkillCooldownProvider
{
    [Serializable]
    private class SkillDisplayEntry
    {
        [SerializeField] private Sprite icon;
        [SerializeField, Min(-1)] private int cooldownSlotIndex = -1;
        [SerializeField] private bool showCooldown = true;
        [SerializeField] private bool showIfCooldownSlotUnavailable;

        public Sprite Icon => icon;
        public int CooldownSlotIndex => cooldownSlotIndex;
        public bool ShowCooldown => showCooldown && cooldownSlotIndex >= 0;
        public bool ShowIfCooldownSlotUnavailable => showIfCooldownSlotUnavailable;
    }

    [Header("Read cooldown from (optional)")]
    [Tooltip("不指定就會自動找 Parent 的 IEnemyCooldownProvider。")]
    [SerializeField] private MonoBehaviour cooldownProviderSource;

    [Header("Battle Skill Entries")]
    [Tooltip("每筆代表戰鬥 HUD 上的一個技能圖示。cooldownSlotIndex = -1 表示被動，只顯示 icon。")]
    [SerializeField] private List<SkillDisplayEntry> skillEntries = new();

    [Header("Legacy Skill Icons (order = cooldown slot index)")]
    [Tooltip("舊資料用：若 skillEntries 為空，仍會依照 cooldown slot 順序讀這個 icon 清單。")]
    [SerializeField] private List<Sprite> skillIcons = new();

    [Header("Display")]
    [Tooltip("icon 沒填時是否仍保留空白 slot。")]
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
            provider = cooldownProviderSource as IEnemyCooldownProvider;

        if (provider == null)
            provider = GetComponentInParent<IEnemyCooldownProvider>();
    }

    public List<EnemyBattleSkillDisplayData> GetSkillCooldowns()
    {
        ResolveProvider();

        var result = new List<EnemyBattleSkillDisplayData>();

        if (skillEntries != null && skillEntries.Count > 0)
        {
            AppendConfiguredEntries(result);
            return result;
        }

        AppendLegacyCooldownEntries(result);
        return result;
    }

    private void AppendConfiguredEntries(List<EnemyBattleSkillDisplayData> result)
    {
        for (int i = 0; i < skillEntries.Count; i++)
        {
            SkillDisplayEntry entry = skillEntries[i];
            if (entry == null)
                continue;

            Sprite icon = entry.Icon;
            if (!showSlotsEvenIfNoIcon && icon == null)
                continue;

            int cooldown = 0;
            bool hasCooldownSlot = entry.CooldownSlotIndex >= 0;
            bool hasValidCooldownSlot = hasCooldownSlot && provider != null && entry.CooldownSlotIndex < provider.CooldownSlotCount;

            if (hasCooldownSlot)
            {
                if (!hasValidCooldownSlot)
                {
                    if (!entry.ShowIfCooldownSlotUnavailable)
                        continue;
                }
                else
                {
                    cooldown = provider.GetCooldownTurnsRemaining(entry.CooldownSlotIndex);
                }
            }

            result.Add(new EnemyBattleSkillDisplayData(icon, cooldown, entry.ShowCooldown && hasValidCooldownSlot));
        }
    }

    private void AppendLegacyCooldownEntries(List<EnemyBattleSkillDisplayData> result)
    {
        if (provider == null || provider.CooldownSlotCount <= 0)
            return;

        int count = provider.CooldownSlotCount;
        for (int i = 0; i < count; i++)
        {
            int cooldown = Mathf.Max(0, provider.GetCooldownTurnsRemaining(i));

            Sprite icon = null;
            if (i >= 0 && i < skillIcons.Count)
                icon = skillIcons[i];

            if (!showSlotsEvenIfNoIcon && icon == null)
                continue;

            result.Add(new EnemyBattleSkillDisplayData(icon, cooldown, true));
        }
    }
}
