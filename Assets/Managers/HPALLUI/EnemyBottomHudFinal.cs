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
    [SerializeField] private float _skillStep = 0.35f;   // 技能間距：左→右
    [SerializeField] private float _statusStep = 0.35f;  // 狀態間距：右→左

    private void Awake()
    {
        if (enemy == null) enemy = GetComponentInParent<Enemy>();
    }

    // Enemy 會傳進來 nextIntent.type / nextIntent.value
    public void Refresh(EnemyIntentType intentType, int intentValue)
    {
        RefreshIntentValue(intentType, intentValue);
        RefreshShield();
        RefreshSkills();
        RefreshStatuses();
    }

    // ----------------- Intent Value (Attack/Defend 才顯示) -----------------
    private void RefreshIntentValue(EnemyIntentType intentType, int value)
    {
        bool show = intentType == EnemyIntentType.Attack || intentType == EnemyIntentType.Defend;

        if (intentValueRoot != null)
            intentValueRoot.SetActive(show);

        if (show && intentValueNumber != null)
            intentValueNumber.SetValue(Mathf.Max(0, value));
    }

    // ----------------- Shield (Block) -----------------
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

        // 先抓 enemy.block
        int blockValue = TryGetInt(enemy, "block", "Block", "currentBlock");

        // 再嘗試抓 enemy.Combat 裡的 block（如果你的 block 放在 combat）
        if (blockValue <= 0 && enemy.Combat != null)
        {
            blockValue = TryGetInt(enemy.Combat, "block", "Block", "currentBlock");
        }

        bool show = blockValue > 0;
        shieldValueRoot.SetActive(show);
        if (shieldIconRoot != null) shieldIconRoot.SetActive(show);

        if (show)
            shieldValueNumber.SetValue(blockValue);
    }

    // ----------------- Skills (左→右，CD=0 也顯示) -----------------
    private void RefreshSkills()
    {
        if (enemy == null)
        {
            DisableExtra(skillSlots, 0);
            return;
        }

        // 你的 Adapter 掛在 Bottom（子物件）時，用 InChildren 才抓得到
        var provider = enemy.GetComponentInChildren<IEnemySkillCooldownProvider>(true);
        var list = provider != null ? provider.GetSkillCooldowns() : new List<(Sprite icon, int cd)>();

        EnsureSkillSlots(list.Count);

        for (int i = 0; i < list.Count; i++)
        {
            skillSlots[i].gameObject.SetActive(true);
            skillSlots[i].Bind(list[i].icon, Mathf.Max(0, list[i].cd));
        }

        DisableExtra(skillSlots, list.Count);
        LayoutSkillSlotsLeftToRight(list.Count);
    }

    private void EnsureSkillSlots(int need)
    {
        if (skillSlotPrefab == null || skillRowRoot == null) return;

        while (skillSlots.Count < need)
        {
            var s = Instantiate(skillSlotPrefab, skillRowRoot);

            // Instantiate 後歸零，避免 prefab 本身帶偏移造成疊一起
            s.transform.localPosition = Vector3.zero;
            s.transform.localRotation = Quaternion.identity;
            s.transform.localScale = Vector3.one;

            skillSlots.Add(s);
        }
    }

    private void LayoutSkillSlotsLeftToRight(int used)
    {
        int n = Mathf.Min(used, skillSlots.Count);

        for (int i = 0; i < n; i++)
            skillSlots[i].transform.localPosition = new Vector3(i * _skillStep, 0f, 0f);

        // 多的歸零（避免你手拉開後留下怪位置）
        for (int i = n; i < skillSlots.Count; i++)
            skillSlots[i].transform.localPosition = Vector3.zero;
    }

    // ----------------- Statuses (右→左，turns>0 才生成) -----------------
    private void RefreshStatuses()
    {
        if (enemy == null)
        {
            DisableExtra(statusSlots, 0);
            return;
        }

        var list = new List<(Sprite icon, int turns)>();

        if (enemy.burningTurns > 0) list.Add((burnIcon, enemy.burningTurns));
        if (enemy.frozenTurns > 0)  list.Add((frozenIcon, enemy.frozenTurns));
        if (enemy.chargedCount > 0) list.Add((chargedIcon, enemy.chargedCount));
        if (enemy.frostStacks > 0)  list.Add((frostIcon, enemy.frostStacks));

        EnsureStatusSlots(list.Count);

        for (int i = 0; i < list.Count; i++)
        {
            statusSlots[i].gameObject.SetActive(true);

            // icon 若為 null：StatusSlotView 會保留 prefab 既有 icon（前提是你的 StatusSlotView 不要強制把 null 蓋掉）
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
            var s = Instantiate(statusSlotPrefab, statusGridRoot);

            s.transform.localPosition = Vector3.zero;
            s.transform.localRotation = Quaternion.identity;
            s.transform.localScale = Vector3.one;

            statusSlots.Add(s);
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

    // ----------------- Helpers -----------------
    private int TryGetInt(object target, params string[] fieldOrPropNames)
    {
        if (target == null) return 0;

        var type = target.GetType();

        foreach (var name in fieldOrPropNames)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(int))
                return (int)field.GetValue(target);

            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
