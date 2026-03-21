using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class EnemyBottomHudFinal : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Enemy enemy;

    [Header("Top Bar Numbers")]
    [SerializeField] private GameObject intentValueRoot;
    [SerializeField] private SpriteNumber intentValueNumber;

    [SerializeField] private GameObject shieldValueRoot;
    [SerializeField] private SpriteNumber shieldValueNumber;
    [SerializeField] private GameObject shieldIconRoot;

    [Header("Skills (Dynamic, keep 0)")]
    [SerializeField] private Transform skillRowRoot;
    [SerializeField] private SkillSlotView skillSlotPrefab;
    private readonly List<SkillSlotView> skillSlots = new();

    [Header("Statuses (Dynamic, hide when 0)")]
    [SerializeField] private Transform statusGridRoot;
    [SerializeField] private StatusSlotView statusSlotPrefab;
    private readonly List<StatusSlotView> statusSlots = new();

    [Header("Status Icons (optional) - if you don't assign, prefab default icon will be used")]
    [SerializeField] private Sprite burnIcon;
    [SerializeField] private Sprite frozenIcon;
    [SerializeField] private Sprite chargedIcon;
    [SerializeField] private Sprite frostIcon;

    [Header("Layout Spacing (local units)")]
    [SerializeField] private float _skillStep = 0.35f;
    [SerializeField] private float _statusStep = 0.35f;

    private void Awake()
    {
        if (enemy == null) enemy = GetComponentInParent<Enemy>();
    }

    public void Refresh(EnemyIntentType intentType, int intentValue)
    {
        RefreshIntentValue(intentType, intentValue);
        RefreshShield();
        RefreshSkills();
        RefreshStatuses();
    }

    private void RefreshIntentValue(EnemyIntentType intentType, int value)
    {
        bool show = intentType == EnemyIntentType.Attack || intentType == EnemyIntentType.Defend;

        if (intentValueRoot != null)
            intentValueRoot.SetActive(show);

        if (show && intentValueNumber != null)
            intentValueNumber.SetValue(Mathf.Max(0, value));
    }

    private void RefreshShield()
    {
        if (shieldValueRoot == null || shieldValueNumber == null)
            return;

        if (enemy == null)
        {
            shieldValueRoot.SetActive(false);
            if (shieldIconRoot != null) shieldIconRoot.SetActive(false);
            return;
        }

        int blockValue = TryGetInt(enemy, "block", "Block", "currentBlock");
        if (blockValue <= 0 && enemy.Combat != null)
            blockValue = TryGetInt(enemy.Combat, "block", "Block", "currentBlock");

        bool show = blockValue > 0;
        shieldValueRoot.SetActive(show);
        if (shieldIconRoot != null) shieldIconRoot.SetActive(show);

        if (show)
            shieldValueNumber.SetValue(blockValue);
    }

    private void RefreshSkills()
    {
        if (enemy == null)
        {
            DisableExtra(skillSlots, 0);
            return;
        }

        IEnemySkillCooldownProvider provider = enemy.GetComponentInChildren<IEnemySkillCooldownProvider>(true);
        List<EnemyBattleSkillDisplayData> list = provider != null
            ? provider.GetSkillCooldowns()
            : new List<EnemyBattleSkillDisplayData>();

        EnsureSkillSlots(list.Count);

        for (int i = 0; i < list.Count; i++)
        {
            skillSlots[i].gameObject.SetActive(true);
            skillSlots[i].Bind(list[i].Icon, list[i].Cooldown, list[i].ShowCooldown);
        }

        DisableExtra(skillSlots, list.Count);
        LayoutSkillSlotsLeftToRight(list.Count);
    }

    private void EnsureSkillSlots(int need)
    {
        if (skillSlotPrefab == null || skillRowRoot == null) return;

        while (skillSlots.Count < need)
        {
            SkillSlotView slot = Instantiate(skillSlotPrefab, skillRowRoot);
            slot.transform.localPosition = Vector3.zero;
            slot.transform.localRotation = Quaternion.identity;
            slot.transform.localScale = Vector3.one;
            skillSlots.Add(slot);
        }
    }

    private void LayoutSkillSlotsLeftToRight(int used)
    {
        int n = Mathf.Min(used, skillSlots.Count);

        for (int i = 0; i < n; i++)
            skillSlots[i].transform.localPosition = new Vector3(i * _skillStep, 0f, 0f);

        for (int i = n; i < skillSlots.Count; i++)
            skillSlots[i].transform.localPosition = Vector3.zero;
    }

    private void RefreshStatuses()
    {
        if (enemy == null)
        {
            DisableExtra(statusSlots, 0);
            return;
        }

        var list = new List<(Sprite icon, int turns)>();

        if (enemy.burningTurns > 0) list.Add((burnIcon, enemy.burningTurns));
        if (enemy.frozenTurns > 0) list.Add((frozenIcon, enemy.frozenTurns));
        if (enemy.chargedCount > 0) list.Add((chargedIcon, enemy.chargedCount));
        if (enemy.frostStacks > 0) list.Add((frostIcon, enemy.frostStacks));

        EnsureStatusSlots(list.Count);

        for (int i = 0; i < list.Count; i++)
        {
            statusSlots[i].gameObject.SetActive(true);
            statusSlots[i].Bind(list[i].icon, Mathf.Max(0, list[i].turns));
        }

        DisableExtra(statusSlots, list.Count);
        LayoutStatusSlotsRightToLeft(list.Count);
    }

    private void EnsureStatusSlots(int need)
    {
        if (statusSlotPrefab == null || statusGridRoot == null) return;

        while (statusSlots.Count < need)
        {
            StatusSlotView slot = Instantiate(statusSlotPrefab, statusGridRoot);
            slot.transform.localPosition = Vector3.zero;
            slot.transform.localRotation = Quaternion.identity;
            slot.transform.localScale = Vector3.one;
            statusSlots.Add(slot);
        }
    }

    private void LayoutStatusSlotsRightToLeft(int used)
    {
        int n = Mathf.Min(used, statusSlots.Count);

        for (int i = 0; i < n; i++)
            statusSlots[i].transform.localPosition = new Vector3(-i * _statusStep, 0f, 0f);

        for (int i = n; i < statusSlots.Count; i++)
            statusSlots[i].transform.localPosition = Vector3.zero;
    }

    private int TryGetInt(object target, params string[] fieldOrPropNames)
    {
        if (target == null) return 0;

        System.Type type = target.GetType();

        foreach (string name in fieldOrPropNames)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(int))
                return (int)field.GetValue(target);

            PropertyInfo prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(int))
                return (int)prop.GetValue(target);
        }

        return 0;
    }

    private void DisableExtra<T>(List<T> views, int used) where T : Component
    {
        for (int i = 0; i < views.Count; i++)
            views[i].gameObject.SetActive(i < used);
    }
}
