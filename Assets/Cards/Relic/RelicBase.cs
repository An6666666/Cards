using UnityEngine;

/// <summary>
/// 遺物的抽象基底類別。
/// 保留與舊資料相同的欄位名稱，讓既有遺物資產在拆分期間仍能沿用名稱、描述、圖片與商店價格。
/// </summary>
public abstract class RelicBase : ScriptableObject
{
    [Header("遺物基本屬性")]
    public string cardName;
    [TextArea] public string description;
    public Sprite cardImage;

    [Header("商店設定")]
    [Tooltip("商店販售這個遺物時的價格。設定為 0 表示使用系統預設價格。")]
    public int shopPrice = 0;

    public virtual void OnAcquire(Player player)
    {
    }

    public virtual void OnBattleStart(Player player)
    {
    }

    public virtual void OnTurnStart(Player player)
    {
    }

    public virtual void OnTurnEnd(Player player)
    {
    }

    public virtual void OnPlayerDiscard(Player player, int discardCount)
    {
    }

    public virtual void OnAddBlock(Player player, int blockAdded)
    {
    }

    public virtual void OnPlayerUseSkillAfterTwoAttacks(Player player)
    {
    }

    public virtual void OnCardPlayed(Player player, CardBase card)
    {
    }

    public virtual void OnCardPlayStarted(Player player, CardBase card)
    {
    }

    public virtual void OnCardExhausted(Player player, CardBase card)
    {
    }

    public virtual void OnEnemyDefeated(Player player, Enemy enemy)
    {
    }

    public virtual void OnAttackCardDamagedEnemy(Player player, AttackCardBase card, Enemy target, int hpDamage)
    {
    }

    public virtual void OnAttackCardHitEnemy(Player player, AttackCardBase card, Enemy target, int attemptedDamage, int hpDamage)
    {
    }

    public virtual int GetAdditionalDamage(Player player, CardBase card, Enemy target)
    {
        return 0;
    }

    public virtual bool TryGetBattleUiCounter(out string counterText)
    {
        counterText = null;
        return false;
    }
}
