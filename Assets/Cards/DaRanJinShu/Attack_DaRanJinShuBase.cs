using System.Collections.Generic;              // 使用泛型集合（List、IEnumerable、HashSet 等）
using UnityEngine;                             // 使用 Unity 引擎核心 API（GameObject、Vector2Int 等）

/// <summary>
/// 大燃盡術基底：消耗總數決定傷害的範圍攻擊卡。
/// </summary>
public abstract class Attack_DaRanJinShuBase : AttackCardBase
// 抽象類別：所有「大燃盡術」系列攻擊卡的共用基底，繼承 AttackCardBase
{
    [Header("數值設定")]                      // Inspector 分組標題：數值相關設定
    [Tooltip("每張已消耗卡片提供的傷害。")]
    public int damagePerExhaust = 2;          // 每 1 張 Exhaust（已消耗卡）所提供的傷害值

    [Header("特效設定")]                      // Inspector 分組標題：特效相關設定
    [Tooltip("命中時產生的特效 (選填)。")]
    public GameObject hitEffectPrefab;        // 攻擊命中敵人時生成的特效 Prefab（可不填）

    [Header("元素設定")]
    [SerializeField]
    [Tooltip("此卡片所使用的元素屬性。")]
    private ElementType elementType = ElementType.Fire;

    protected virtual ElementType Element => elementType;

    private void OnEnable()
    {
        cardType = CardType.Attack;           // 強制設定此卡為攻擊卡類型
        cost = 0;                             // 設定卡牌能量消耗為 0
        elementType = Element;
    }

    private void OnValidate()
    {
        cost = 0;                             // 在 Inspector 變更時強制保持 cost 為 0（防止誤改）
        elementType = Element;
    }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        if (player == null) return;           // 若玩家不存在，直接中止執行

        int exhaustCount = player.exhaustPile != null ? player.exhaustPile.Count : 0;
        // 取得玩家 Exhaust 區的卡牌數量（若為 null 則視為 0）

        if (exhaustCount < 0) exhaustCount = 0;
        // 防呆處理：確保消耗數量不會是負值

        int totalDamage = exhaustCount * damagePerExhaust;
        // 總傷害 = 已消耗卡數 × 每張卡提供的傷害

        if (totalDamage <= 0) return;
        // 若計算後沒有傷害，直接結束

        ElementType element = Element;         // 取得此卡牌所使用的元素類型
        bool hitAnyTarget = false;             // 是否至少命中一名敵人的旗標

        foreach (Enemy target in GetEnemiesInRange(player))
        // 對所有在攻擊範圍內的敵人進行處理
        {
            if (target == null) continue;      // 若敵人為 null，跳過

            int appliedDamage = target.ApplyElementalAttack(element, totalDamage, player);
            // 對敵人套用元素攻擊計算，取得實際應用的傷害值

            target.TakeDamage(appliedDamage);
            // 對敵人造成實際傷害

            hitAnyTarget = true;               // 標記本次攻擊至少命中一個目標

            if (hitEffectPrefab != null)
            {
                GameObject.Instantiate(
                    hitEffectPrefab,
                    target.transform.position,
                    Quaternion.identity);
                // 若有設定命中特效，則在敵人位置生成特效
            }
        }

        if (hitAnyTarget && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAttackSFX(element);
            // 若有命中敵人，且 AudioManager 存在，播放對應元素的攻擊音效
        }
    }

    private IEnumerable<Enemy> GetEnemiesInRange(Player player)
    {
        List<Vector2Int> offsets = (rangeOffsets != null && rangeOffsets.Count > 0)
            ? rangeOffsets
            // 若卡牌有自訂攻擊範圍（rangeOffsets），則使用該設定
            : new List<Vector2Int>
            // 否則使用預設 8 方向（上下左右＋四個斜角）
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 1), new Vector2Int(1, -1),
                new Vector2Int(-1, 1), new Vector2Int(-1, -1)
            };

        Vector2Int center = player.position;
        // 以玩家所在的格子位置作為中心點

        Enemy[] allEnemies = GameObject.FindObjectsOfType<Enemy>();
        // 從場景中取得所有 Enemy 物件

        HashSet<Enemy> uniqueTargets = new HashSet<Enemy>();
        // 使用 HashSet 確保同一個敵人不會被重複回傳

        foreach (Vector2Int offset in offsets)
        {
            Vector2Int targetPos = center + offset;
            // 計算攻擊範圍內的目標格子位置

            foreach (Enemy target in allEnemies)
            {
                if (target != null &&
                    target.gridPosition == targetPos &&
                    uniqueTargets.Add(target))
                {
                    // 若敵人存在、站在該目標格子，且尚未被加入過
                    yield return target;       // 回傳該敵人作為可攻擊目標
                }
            }
        }
    }
}
