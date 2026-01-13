using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class StatusPanel_Text : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text statsText;     // 基本屬性（血量、格擋、金幣等）
    [SerializeField] private Text buffsText;     // 正面BUFF
    [SerializeField] private Text debuffsText;   // 負面BUFF
    [SerializeField] private Text skillText;     // 技能

    [Header("Options")]
    [SerializeField] private bool showZeroEffects = false;
    [SerializeField] private int floatDigits = 2;

    private Player player;
    private Enemy enemy;
    private PlayerBuffController buffs;

    private void Awake()
    {
        Hide(); // 一開始先隱藏，等 hover 再顯示
    }

    private void Update()
    {
        // 不再每幀 Refresh（避免反射一直跑）
    }

    private void Refresh()
    {
            // 沒目標就不要顯示
        if (player == null && enemy == null)
        {
            Hide();
            return;
        }
        // ====== Stats ======
        if (statsText != null)
        {
            var sb = new StringBuilder();

            // 目標是玩家
            if (player != null)
            {   
                sb.AppendLine($"防禦：{TryGetInt(player, "block", "Block")}");
                sb.AppendLine($"金幣：{player.gold}");
            }
            // 目標是敵人（沒有 gold 就不顯示）
            else if (enemy != null)
            {   
                sb.AppendLine($"名稱：{enemy.enemyName}");
                // ✅ 敵人血量開回來（Enemy 你先前程式看起來也有 currentHP / maxHP）
                sb.AppendLine($"血量：{enemy.currentHP}/{enemy.maxHP}");
                sb.AppendLine($"防禦：{TryGetInt(enemy, "block", "Block")}");
                // ✅ 新增：附加元素（你要把 GetEnemyElementText 寫出來）
                sb.AppendLine($"元素：{GetEnemyElementText(enemy)}");
            }

            statsText.text = sb.ToString();
        }

        // ========== Skill / Element ==========
    if (skillText != null)
    {
        if (enemy != null)
        {
            string skill = GetEnemySkillDesc(enemy);

            skillText.text =
                $"{skill}";
        }
        else
        {
            // 玩家要不要顯示技能看你；不需要就顯示空或「無」
            skillText.text = "";
        }
    }


        // ====== Buff/Debuff ======
    // 如果不是玩家（沒有 buffs），就顯示「無」或直接清空
    if (buffs == null)
    {
        if (buffsText != null) buffsText.text = "正面效果：\n無";
        if (debuffsText != null) debuffsText.text = "負面效果：\n無";
        return;
    }
        var positive = new List<string>();
        var negative = new List<string>();

        // ====== Debuffs (negative) ======
        AddIntEffect(negative, "虛弱", buffs.weak);
        AddIntEffect(negative, "流血", buffs.bleed);
        AddIntEffect(negative, "禁錮", buffs.imprison);

        // 這個在你 Inspector 是 int（0/1 或次數），用 int 顯示
        AddIntEffect(negative, "回合結束隨機棄牌", buffs.needRandomDiscardAtEnd);

        AddBoolEffect(negative, "本回合抽牌被阻止", buffs.drawBlockedThisTurn);

        // damageTakenRatio 預設 1，≠1 才顯示
        AddFloatEffect(negative, "受到傷害倍率", buffs.damageTakenRatio, onlyIfNotDefault: true, defaultValue: 1f);

        // 下次受傷增加：通常是負面（如果你其實設計成正面就移到 positive）
        AddIntEffect(negative, "下次受傷增加", buffs.nextDamageTakenUp);

        // ====== Buffs (positive) ======
        AddIntEffect(positive, "下次攻擊+傷", buffs.nextAttackPlus);
        AddIntEffect(positive, "下回合全攻擊+傷", buffs.nextTurnAllAttackPlus);

        AddIntEffect(positive, "近戰傷害減免", buffs.meleeDamageReduce);
        AddIntEffect(positive, "回合結束獲得格擋", buffs.blockGainAtTurnEnd);
        AddBoolEffect(positive, "格擋保留到下回合", buffs.retainBlockNextTurn);

        // ====== Mixed modifiers (depends on sign) ======
        AddSignedIntEffect(positive, negative, "下次攻擊耗能", buffs.nextAttackCostModify, positiveWhenNegative: true);
        AddSignedIntEffect(positive, negative, "移動耗能", buffs.movementCostModify, positiveWhenNegative: true);
        AddSignedIntEffect(positive, negative, "下回合抽牌", buffs.nextTurnDrawChange, positiveWhenNegative: false);

        // ====== Output ======
        if (buffsText != null)
            buffsText.text = BuildListText("正面效果", positive);

        if (debuffsText != null)
            debuffsText.text = BuildListText("負面效果", negative);
    }

    private void Show()
    {
        gameObject.SetActive(true);
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetTarget(GameObject target)
    {
        if (target == null)
        {
            ClearTarget();
            return;
        }

        // 先清掉舊目標
        player = null;
        enemy = null;
        buffs = null;

        // 抓 Player / Enemy
        player = target.GetComponent<Player>();
        enemy  = target.GetComponent<Enemy>();

        // 只有玩家才有 buffs（你目前是這樣設計）
        if (player != null)
        {
            buffs = player.GetComponent<PlayerBuffController>();
            if (buffs == null) buffs = player.GetComponentInChildren<PlayerBuffController>();
        }

        Show();     // 把面板打開
        Refresh();  // 立刻刷新一次
        StartCoroutine(RefreshNextFrame());
    }

    public void ClearTarget()
    {
        player = null;
        enemy = null;
        buffs = null;

        Hide();
    }

    // ---------- Helpers ----------
    private string BuildListText(string title, List<string> items)
    {
        if (items.Count == 0) return $"{title}：\n無";
        var sb = new StringBuilder();
        sb.AppendLine($"{title}：");
        for (int i = 0; i < items.Count; i++)
            sb.AppendLine($"- {items[i]}");
        return sb.ToString();
    }

    private void AddIntEffect(List<string> list, string name, int value)
    {
        if (!showZeroEffects && value == 0) return;
        list.Add($"{name} {value}");
    }

    private void AddSignedIntEffect(List<string> positive, List<string> negative, string name, int value, bool positiveWhenNegative)
    {
        if (!showZeroEffects && value == 0) return;

        // positiveWhenNegative = true 代表「數值為負」是好事（例如耗能 -1）
        bool isPositive = positiveWhenNegative ? (value < 0) : (value > 0);

        string signText = value > 0 ? $"+{value}" : value.ToString();
        string line = $"{name} {signText}";

        (isPositive ? positive : negative).Add(line);
    }

    private void AddBoolEffect(List<string> list, string name, bool enabled)
    {
        if (!showZeroEffects && !enabled) return;
        list.Add(name);
    }

    private void AddFloatEffect(List<string> list, string name, float value, bool onlyIfNotDefault, float defaultValue)
    {
        if (onlyIfNotDefault && Mathf.Approximately(value, defaultValue) && !showZeroEffects) return;
        list.Add($"{name} {value.ToString($"F{floatDigits}")}");
    }

    private System.Collections.IEnumerator RefreshNextFrame()
    {
        yield return null;                 // 下一幀
        Refresh();
        yield return new WaitForSeconds(0.05f); // 再等一下（給 tween/狀態同步時間）
        Refresh();
    }

    private int TryGetInt(object obj, params string[] possibleNames)
    {
        if (obj == null) return 0;

        var t = obj.GetType();
        foreach (var n in possibleNames)
        {
            var prop = t.GetProperty(n);
            if (prop != null && prop.PropertyType == typeof(int))
                return (int)prop.GetValue(obj);

            var field = t.GetField(n);
            if (field != null && field.FieldType == typeof(int))
                return (int)field.GetValue(obj);
        }
        return 0;
    }

    private string GetEnemyElementText(Enemy e)
    {
        if (e == null) return "無";

        // 以你現有的「元素 icon 顯示」系統為準
        var display = e.GetComponentInChildren<EnemyElementStatusDisplay>(true);
        if (display == null) return "無";

        var list = new List<string>();

        // 這裡的字串請用你 Hierarchy 看到的實際名稱
        var fire = display.transform.Find("Fire Icon");
        if (fire != null && fire.gameObject.activeInHierarchy) list.Add("火");

        var wood = display.transform.Find("Wood Icon");
        if (wood != null && wood.gameObject.activeInHierarchy) list.Add("木");

        var water = display.transform.Find("Water Icon");
        if (water != null && water.gameObject.activeInHierarchy) list.Add("水");

        var thunder = display.transform.Find("Thunder Icon");
        if (thunder != null && thunder.gameObject.activeInHierarchy) list.Add("雷");

        var ice = display.transform.Find("Ice Icon");
        if (ice != null && ice.gameObject.activeInHierarchy) list.Add("冰");

        if (list.Count == 0) return "無";
        return string.Join(" + ", list);
    }

    private bool TryGetElementFromMember(System.Reflection.MemberInfo member, Enemy e, out string value)
    {
        value = null;
        if (member == null) return false;

        object raw = null;
        switch (member)
        {
            case System.Reflection.FieldInfo f:
                raw = f.GetValue(e);
                break;
            case System.Reflection.PropertyInfo p:
                raw = p.GetValue(e);
                break;
        }

        if (raw == null) return false;

        if (raw is string s && !string.IsNullOrEmpty(s))
        {
            value = s;
            return true;
        }

        var rawType = raw.GetType();
        if (rawType.IsEnum)
        {
            value = raw.ToString();
            return true;
        }

        return false;
    }

    private string GetEnemySkillDesc(Enemy e)
    {
        if (e == null) return "（無）";

        // 先讀 Enemy 自己的欄位（最快最穩）
        var desc = TryGetString(e, "skillDescription", "SkillDescription");
        if (!string.IsNullOrEmpty(desc)) return desc;

        // 退回你原本 skillData / attackCard
        var t = e.GetType();
        object skillData = GetMemberValue(t, e, "skillData");
        if (TryGetDescription(skillData, out var d)) return d;

        object attackCard = GetMemberValue(t, e, "attackCard");
        if (TryGetDescription(attackCard, out d)) return d;

    return "（未設定）";
    }

    private string TryGetString(object obj, params string[] possibleNames)
    {
        if (obj == null) return null;
        var t = obj.GetType();

        foreach (var n in possibleNames)
        {
            var f = t.GetField(n);
            if (f != null && f.FieldType == typeof(string))
                return (string)f.GetValue(obj);

            var p = t.GetProperty(n);
            if (p != null && p.PropertyType == typeof(string))
                return (string)p.GetValue(obj);
        }
        return null;
    }
    private object GetMemberValue(System.Type t, object obj, string name)
    {
        var f = t.GetField(name);
        if (f != null) return f.GetValue(obj);

        var p = t.GetProperty(name);
        if (p != null) return p.GetValue(obj);

        return null;
    }

    private bool TryGetDescription(object source, out string desc)
    {
        desc = null;
        if (source == null) return false;

        var t = source.GetType();
        var f = t.GetField("description");
        if (f != null && f.FieldType == typeof(string))
        {
            desc = (string)f.GetValue(source);
            return !string.IsNullOrEmpty(desc);
        }

        var p = t.GetProperty("description");
        if (p != null && p.PropertyType == typeof(string))
        {
            desc = (string)p.GetValue(source);
            return !string.IsNullOrEmpty(desc);
        }

        return false;
    }

        
}



