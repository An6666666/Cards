using UnityEngine;

/// <summary>
/// 急急如律令的基礎攻擊卡。處理共通的傷害計算、特效播放與音效邏輯，
/// 透過覆寫 Element 與 EffectPrefab 來定義各元素版本的行為，必要時可覆寫
/// OnAfterDamage 以追加額外效果（如冰凍等）。
/// </summary>
public abstract class Attack_JiJiRuLvLing : AttackCardBase
{
    [Header("基本設定")]
    public int baseDamage = 6;

    [SerializeField]
    [Tooltip("此卡片所使用的元素屬性。")]
    private ElementType elementType = ElementType.Fire;

    protected virtual ElementType Element => elementType;

    protected abstract GameObject EffectPrefab { get; }

    private void OnEnable()
    {
        cardType = CardType.Attack;
    }

    private void OnValidate()
    {
        elementType = Element;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        if (enemy == null) return;

        ElementType element = Element;
        int damage = enemy.ApplyElementalAttack(element, baseDamage, player);
        enemy.TakeDamage(damage);

        OnAfterDamage(player, enemy, element, damage);

        GameObject effectPrefab = EffectPrefab;
        if (effectPrefab != null)
        {
            GameObject.Instantiate(effectPrefab, enemy.transform.position, Quaternion.identity);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAttackSFX(element);
        }
    }

    /// <summary>
    /// 造成傷害後執行的額外行為。預設無行為，可由子類別覆寫。
    /// </summary>
    protected virtual void OnAfterDamage(Player player, Enemy enemy, ElementType element, int damage)
    {
    }
}
