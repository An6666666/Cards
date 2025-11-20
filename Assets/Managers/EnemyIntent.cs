// EnemyIntent.cs
using System;

/// <summary>
/// 敵人的意圖種類：攻擊、移動、防禦、Buff、Debuff、蓄力、發呆之類
/// </summary>
public enum EnemyIntentType
{
    Attack,
    Move,
    Defend,
    Buff,
    Debuff,
    Charge,
    Idle
}

/// <summary>
/// 敵人下一回合的意圖資料
/// </summary>
[Serializable]
public class EnemyIntent
{
    public EnemyIntentType type;  // 要做什麼
    public int value;             // 數值型（例如預估傷害、防禦量），沒有就填 0

    public EnemyIntent()
    {
    }

    public EnemyIntent(EnemyIntentType type, int value = 0)
    {
        this.type = type;
        this.value = value;
    }
}
