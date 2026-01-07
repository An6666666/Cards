using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class StatusPanel_Text : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text statsText;
    [SerializeField] private Text buffsText;
    [SerializeField] private Text debuffsText;

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
        // 如果你希望「數值有變就即時更新」，保留這行即可
        if (player != null || enemy != null)
            Refresh();
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
                sb.AppendLine($"防禦：{TryGetInt(enemy, "block", "Block")}");
            }

            statsText.text = sb.ToString();
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
}
