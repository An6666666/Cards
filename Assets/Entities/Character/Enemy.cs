using System;                                     // 引用系統基本功能
using System.Collections;                          // 引用非泛型集合命名空間
using System.Collections.Generic;                 // 引用泛型集合命名空間
using UnityEngine;                                 // 引用 Unity 核心功能
using UnityEngine.Rendering;                      // 使用 SortingGroup 控制圖層排序
using TMPro;

public class Enemy : MonoBehaviour              // 敵人角色，繼承自 MonoBehaviour
{
    public string enemyName = "Slime";           // 敵人名稱，預設為 Slime
    public int maxHP = 30;                         // 最大生命值
    public int currentHP;                          // 當前生命值
    public int block = 0;                          // 格擋值，用於抵消傷害
    public virtual bool ShouldResetBlockEachTurn => true; // 預設每回合重置格擋

    public int burningTurns = 0;                   // 燃燒狀態持續回合數
    public int frozenTurns = 0;                    // 冰凍狀態持續回合數
    public bool thunderstrike = false;             // 是否被雷擊標記
    public bool superconduct = false;              // 是否處於超導狀態

    public bool hasBerserk = false;                // 是否處於狂暴狀態
    public EnemyBuffs buffs = new EnemyBuffs();    // 敵人 Buff 結構
    public Vector2Int gridPosition;                // 在格子地圖中的座標

    [Header("攻擊設定")]
    [SerializeField] private int baseAttackDamage = 10; // 基礎攻擊傷害，可於 Inspector 調整

    [Header("攻擊範圍偏移")]
    public List<Vector2Int> attackRangeOffsets = new List<Vector2Int>
    {
        new Vector2Int(-2,0), new Vector2Int(2,0),
        new Vector2Int(-1,-2), new Vector2Int(1,-2),
        new Vector2Int(-1,2), new Vector2Int(1,2)
    };

    public bool isBoss = false;                    // 是否為首領級敵人

    private HashSet<ElementType> elementTags = new HashSet<ElementType>();  // 元素標籤
    public event Action<Enemy> ElementTagsChanged;                           // 元素標籤變動事件

    [SerializeField] private Transform spriteRoot;            // 僅包含貼圖的顯示節點

    private Coroutine shakeRoutine;                 // 受擊抖動協程參考
    private Vector3 spriteDefaultLocalPosition;     // 記錄初始在父物件下的位置
    private Vector3 spriteDefaultLocalScale;        // 記錄初始縮放
    private bool spriteDefaultsInitialized = false; // 是否已取得初始值

    private Animator spriteAnimator;                 // 本體動畫控制器
    private Animator highlightAnimator;              // 高亮動畫控制器

    [Header("圖層排序設定")]
    [SerializeField] private int sortingOrderBase = 0;             // 基礎排序值
    [SerializeField] private float sortingOrderMultiplier = 100f;  // 依 Y 值放大的倍率
    private SpriteRenderer[] cachedSpriteRenderers;                // 緩存所有子節點 SpriteRenderer
    private int[] cachedSpriteBaseOrders;                          // 記錄初始排序值，保留相對順序
    private SortingGroup sortingGroup;                             // 可能存在的 SortingGroup
    private int sortingGroupBaseOrder;                             // 記錄 SortingGroup 初始排序
    private Vector3 lastWorldPosition;                             // 上一次套用排序時的世界座標
    private bool hasLastWorldPosition;                             // 是否已記錄最後的世界座標
    private int lastAppliedOrderOffset;                            // 上一次套用的排序位移量
    private bool hasLastAppliedOrder;                              // 是否已套用過排序位移

    [Header("受擊抖動設定")]
    [SerializeField] private float shakeDuration = 0.1f;      // 抖動持續時間
    [SerializeField] private float shakeMagnitude = 0.1f;     // 初始抖動幅度
    [SerializeField] private float scaleMultiplier = 1.1f;    // 放大倍率

        // ====== 攻擊意圖：世界空間小圖示（非 UI / 非 Canvas） ======
    [Header("Intent (Sprite Icon)")]
    [Tooltip("這隻敵人是否可以移動，用來決定意圖要顯示 Move 還是 Idle")]
    public bool canMove = true;

    [Tooltip("下一回合預計要做的行動（邏輯用）")]
    public EnemyIntent nextIntent = new EnemyIntent();

    [Tooltip("掛在敵人身上的 SpriteRenderer，用來顯示意圖小圖")]
    public SpriteRenderer intentIconRenderer;

    [Tooltip("意圖圖示相對於敵人中心的位置偏移")]
    public Vector3 intentWorldOffset = new Vector3(0f, 2f, 0f);

    [Header("Intent Icon Sprites")]
    public Sprite intentAttackSprite;
    public Sprite intentMoveSprite;
    public Sprite intentIdleSprite;
    public Sprite intentDefendSprite;

        [Header("Intent Value (Attack Number)")]
    [Tooltip("顯示攻擊數值用的 TextMeshPro (世界空間文字)")]
    public TMP_Text intentValueText;

    [Tooltip("數字文字相對於圖示的位置偏移")]
    public Vector3 intentValueOffset = new Vector3(0.8f, 0.1f, 0f);  // 可之後再調



    // 移動到指定格子
    public void MoveToPosition(Vector2Int targetGridPos)
    {
        Board board = FindObjectOfType<Board>();
        if (board == null) return;
        BoardTile tile = board.GetTileAt(targetGridPos);
        if (tile == null) return;
        if (board.IsTileOccupied(targetGridPos)) return;
        Player p = FindObjectOfType<Player>();
        if (p != null && p.position == targetGridPos) return;

        gridPosition = targetGridPos;
        transform.position = tile.transform.position;
        UpdateSpriteSortingOrder();
        CaptureSpriteDefaults();
    }

    private void CacheSortingComponents()
    {
        sortingGroup = GetComponentInChildren<SortingGroup>(true);
        sortingGroupBaseOrder = sortingGroup != null ? sortingGroup.sortingOrder : 0;

        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (allRenderers != null && allRenderers.Length > 0)
        {
            List<SpriteRenderer> filteredRenderers = new List<SpriteRenderer>(allRenderers.Length);
            List<int> filteredBaseOrders = new List<int>(allRenderers.Length);

            foreach (SpriteRenderer renderer in allRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.GetComponent<SpriteSortingByParentY>() != null)
                {
                    continue;
                }

                filteredRenderers.Add(renderer);
                filteredBaseOrders.Add(renderer.sortingOrder);
            }

            cachedSpriteRenderers = filteredRenderers.ToArray();
            cachedSpriteBaseOrders = filteredBaseOrders.ToArray();
        }
        else
        {
            cachedSpriteRenderers = Array.Empty<SpriteRenderer>();
            cachedSpriteBaseOrders = Array.Empty<int>();
        }
    }

    private void EnsureAnimators()
    {
        if (spriteAnimator == null)
        {
            Transform root = spriteRoot ? spriteRoot : transform;
            spriteAnimator = root.GetComponent<Animator>();
            if (spriteAnimator == null)
            {
                spriteAnimator = root.GetComponentInChildren<Animator>(true);
            }
        }

        if (highlightAnimator == null && highlightFx != null)
        {
            highlightAnimator = highlightFx.GetComponent<Animator>();
            if (highlightAnimator == null)
            {
                highlightAnimator = highlightFx.GetComponentInChildren<Animator>(true);
            }
        }
    }

    private void SyncHighlightAnimation()
    {
        EnsureAnimators();
        if (spriteAnimator == null || highlightAnimator == null)
        {
            return;
        }

        AnimatorStateInfo bodyState = spriteAnimator.GetCurrentAnimatorStateInfo(0);
        float normalizedTime = Mathf.Repeat(bodyState.normalizedTime, 1f);

        highlightAnimator.Update(0f);
        AnimatorStateInfo highlightState = highlightAnimator.GetCurrentAnimatorStateInfo(0);
        int targetStateHash = highlightState.shortNameHash;

        highlightAnimator.Play(targetStateHash, 0, normalizedTime);
        highlightAnimator.Update(0f);
        highlightAnimator.speed = spriteAnimator.speed;
    }

    private bool IsRendererOwnedByThis(SpriteRenderer renderer)
    {
        return renderer != null && renderer.transform != null && (renderer.transform == transform || renderer.transform.IsChildOf(transform));
    }

    private bool IsSortingGroupOwnedByThis(SortingGroup group)
    {
        if (group == null)
        {
            return false;
        }

        Transform groupTransform = group.transform;
        return groupTransform == transform || groupTransform.IsChildOf(transform);
    }

    private void EnsureSortingComponents()
    {
        bool needsRefresh =
            cachedSpriteRenderers == null ||
            cachedSpriteBaseOrders == null ||
            cachedSpriteRenderers.Length == 0 ||
            cachedSpriteBaseOrders.Length != cachedSpriteRenderers.Length;

        if (!needsRefresh)
        {
            for (int i = 0; i < cachedSpriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = cachedSpriteRenderers[i];
                if (renderer == null || !IsRendererOwnedByThis(renderer))
                {
                    needsRefresh = true;
                    break;
                }
            }
        }

        if (!needsRefresh && sortingGroup != null && !IsSortingGroupOwnedByThis(sortingGroup))
        {
            needsRefresh = true;
        }

        if (needsRefresh)
        {
            CacheSortingComponents();
        }
        else
        {
            if (sortingGroup == null)
            {
                sortingGroup = GetComponentInChildren<SortingGroup>(true);
            }

            if (sortingGroup != null)
            {
                sortingGroupBaseOrder = sortingGroup.sortingOrder;
            }
        }
    }

    protected void UpdateSpriteSortingOrder()
    {
        EnsureSortingComponents();

        int order = sortingOrderBase + Mathf.RoundToInt(-transform.position.y * sortingOrderMultiplier);

        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = sortingGroupBaseOrder + order;
        }

        if (cachedSpriteRenderers != null)
        {
            for (int i = 0; i < cachedSpriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = cachedSpriteRenderers[i];
                if (renderer != null)
                {
                    int baseOrder = (cachedSpriteBaseOrders != null && i < cachedSpriteBaseOrders.Length)
                        ? cachedSpriteBaseOrders[i]
                        : 0;
                    renderer.sortingOrder = baseOrder + order;
                }
            }
        }

        lastAppliedOrderOffset = order;
        hasLastAppliedOrder = true;
        lastWorldPosition = transform.position;
        hasLastWorldPosition = true;
    }

    private void LateUpdate()
    {
        Vector3 currentPosition = transform.position;
        bool positionChanged = !hasLastWorldPosition || (currentPosition - lastWorldPosition).sqrMagnitude > 0.0001f;

        if (transform.hasChanged || positionChanged || !hasLastAppliedOrder)
        {
            UpdateSpriteSortingOrder();
        }

        transform.hasChanged = false;
        // 讓意圖小圖跟著敵人位置
        if (intentIconRenderer != null)
        {
            intentIconRenderer.transform.position = transform.position + intentWorldOffset;
        }
        // ★ 攻擊數字也跟著圖示移動
        if (intentValueText != null)
        {
            intentValueText.transform.position =
                (intentIconRenderer != null ? intentIconRenderer.transform.position : transform.position)
                + intentValueOffset;
        }
    }

    protected virtual bool IsPlayerInRange(Player player)
    {
        foreach (var off in attackRangeOffsets)
        {
            if (gridPosition + off == player.position) return true;
        }
        return false;
    }

    protected virtual void MoveOneStepTowards(Player player)
    {
        Board board = FindObjectOfType<Board>();
        if (board == null) return;
        var adjs = board.GetAdjacentTiles(gridPosition);
        Vector2Int bestPos = gridPosition;
        float bestDist = Vector2Int.Distance(gridPosition, player.position);
        foreach (var t in adjs)
        {
            Vector2Int pos = t.gridPosition;
            if (board.IsTileOccupied(pos)) continue;
            if (player.position == pos) continue;
            float d = Vector2Int.Distance(pos, player.position);
            if (d < bestDist)
            {
                bestDist = d;
                bestPos = pos;
            }
        }
        if (bestPos != gridPosition)
            MoveToPosition(bestPos);
    }
    protected virtual void Awake()                  // Awake 在物件建立時呼叫
    {
        currentHP = maxHP;                         // 同步當前生命值為最大值

        CacheSortingComponents();                  // 緩存渲染相關組件
        UpdateSpriteSortingOrder();                // 依據位置更新排序
        EnsureAnimators();
    }

    private void OnEnable()
    {
        if (cachedSpriteRenderers == null || cachedSpriteRenderers.Length == 0)
        {
            CacheSortingComponents();
        }
        UpdateSpriteSortingOrder();
    }

    [SerializeField] private GameObject highlightFx;  // 高亮特效物件

    public void SetHighlight(bool on)               // 控制高亮顯示
    {
        if (highlightFx)
        {
            highlightFx.SetActive(on);
            if (on)
            {
                SyncHighlightAnimation();
            }
        }
        UpdateSpriteSortingOrder();
    }

    private void OnMouseDown()                     // 滑鼠點擊時呼叫
    {
        BattleManager bm = FindObjectOfType<BattleManager>();  // 找到 BattleManager
        bm.OnEnemyClicked(this);                   // 通知 BattleManager 有敵人被點擊
    }

    private Transform GetSpriteRoot()
    {
        return spriteRoot ? spriteRoot : transform;
    }

    private void CaptureSpriteDefaults()
    {
        
        Transform root = GetSpriteRoot();
        spriteDefaultLocalPosition = root.localPosition;
        spriteDefaultLocalScale = root.localScale;
        spriteDefaultsInitialized = true;
    }

    private void EnsureSpriteDefaults()
    {
        if (spriteDefaultsInitialized) return;
        CaptureSpriteDefaults();
    }

    private void ResetSpriteVisual()
    {
        EnsureSpriteDefaults();
        Transform root = GetSpriteRoot();
        root.localPosition = spriteDefaultLocalPosition;
        root.localScale = spriteDefaultLocalScale;
    }

    private IEnumerator HitShake()                 // 受擊抖動效果
    {
        EnsureSpriteDefaults();
        Transform root = GetSpriteRoot();
        Vector3 originalPos = spriteDefaultLocalPosition;
        Vector3 originalScale = spriteDefaultLocalScale;

        float elapsed = 0f;
        Vector3 targetScale = originalScale * scaleMultiplier;
        root.localScale = targetScale;
        while (elapsed < shakeDuration)
        {
            float t = elapsed / shakeDuration;
            float currentMag = Mathf.Lerp(shakeMagnitude, 0f, t); // 幅度隨時間衰減
            root.localPosition = originalPos + (Vector3)UnityEngine.Random.insideUnitCircle * currentMag;            root.localScale = Vector3.Lerp(targetScale, originalScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        ResetSpriteVisual();

    }

    public virtual void TakeDamage(int dmg)                // 受到傷害 (考慮格擋)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            ResetSpriteVisual();
        }
        else
        {
            EnsureSpriteDefaults();
        }
        shakeRoutine = StartCoroutine(HitShake());
        int remain = dmg - block;                 // 計算剩餘傷害
        if (remain > 0)
        {
            block = 0;                            // 格擋用完歸零
            currentHP -= remain;                  // 扣除剩餘傷害
            if (currentHP <= 0)
            {
                currentHP = 0;                   // 生命不低於 0
                Die();                           // 生命歸零觸發死亡
            }
        }
        else
        {
            block -= dmg;                         // 僅扣除格擋
        }
    }

    public virtual void TakeTrueDamage(int dmg)           // 真實傷害 (無視格擋)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            ResetSpriteVisual();
        }
        else
        {
            EnsureSpriteDefaults();
        }
        shakeRoutine = StartCoroutine(HitShake());
        currentHP -= dmg;                         // 直接扣除生命
        if (currentHP <= 0)
        {
            currentHP = 0;
            Die();                                // 扣到 0 則死亡
        }
    }

    public void AddBlock(int amount)              // 增加格擋值
    {
        block += amount;
    }

    public void ReduceBlock(int amount)           // 減少格擋值
    {
        block -= amount;
        if (block < 0) block = 0;                // 格擋不低於 0
    }

    public void DispelBuff(int count)             // 清除指定數量的 Buff
    {
        buffs.ClearSomeBuff(count);
    }

    public bool HasElement(ElementType e)          // 檢查是否有指定元素標籤
    {
        return elementTags.Contains(e);
    }

    public void AddElementTag(ElementType e)      // 添加元素標籤
    {
        if (elementTags.Add(e))
        {
            ElementTagsChanged?.Invoke(this);
        }
    }

    public void RemoveElementTag(ElementType e)   // 移除元素標籤
    {
        if (elementTags.Remove(e))
        {
            ElementTagsChanged?.Invoke(this);
        }
    }

    public IEnumerable<ElementType> GetElementTags()  // 取得目前所有元素標籤
    {
        return elementTags;
    }

    public int ApplyElementalAttack(ElementType e, int baseDamage, Player player)
    {
        var strat = ElementalStrategyProvider.Get(e);
        return strat.CalculateDamage(player, this, baseDamage);
    }

    public virtual void ProcessTurnStart()
    {
        var tagsCopy = new List<ElementType>(elementTags);
        foreach (var tag in tagsCopy)
        {
            var strat = ElementalStrategyProvider.Get(tag);

            // 如果該策略有持續效果的介面實作
            if (strat is IStartOfTurnEffect effect)
            {
                effect.OnStartOfTurn(this);  // 執行持續效果
            }
        }

        // 成長陷阱效果照舊
        Board board = FindObjectOfType<Board>();
        if (board != null)
        {
            var tile = board.GetTileAt(gridPosition);
            tile?.TriggerGrowthTrap(this);
        }
    }

    public int BaseAttackDamage
    {
        get => baseAttackDamage;
        set => baseAttackDamage = Mathf.Max(0, value);
    }

    protected virtual int GetBaseAttackDamage()
    {
        return Mathf.Max(0, baseAttackDamage);
    }

    protected virtual int CalculateAttackDamage()
    {
        int atkValue = GetBaseAttackDamage();
        if (hasBerserk) atkValue += 5;       // 狂暴狀態加攻擊
        return atkValue;
    }

        // 決定下一回合的意圖（只算邏輯＋更新小圖）
    public void DecideNextIntent(Player player)
    {
        if (player == null)
        {
            UpdateIntentIcon();
            return;
        }

        // 被控制（冰凍、暈眩） → Idle
        if (frozenTurns > 0 || buffs.stun > 0)
        {
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        // 如果在攻擊範圍 → 預告攻擊
        if (IsPlayerInRange(player))
        {
            nextIntent.type = EnemyIntentType.Attack;
            nextIntent.value = CalculateAttackDamage();
            UpdateIntentIcon();
            return;
        }

        // 不在攻擊範圍 → 可移動就 Move，不能移動就 Idle
        if (canMove)
        {
            nextIntent.type = EnemyIntentType.Move;
            nextIntent.value = 0;
        }
        else
        {
            nextIntent.type = EnemyIntentType.Idle;
            nextIntent.value = 0;
        }

        UpdateIntentIcon();
    }

    // 根據 nextIntent 更新上面的小圖 Sprite + 位置
    private void UpdateIntentIcon()
    {
        if (intentIconRenderer == null)
            return;

        // 選擇要用的圖
        Sprite icon = null;
        switch (nextIntent.type)
        {
            case EnemyIntentType.Attack:
                icon = intentAttackSprite;
                break;
            case EnemyIntentType.Move:
                icon = intentMoveSprite;
                break;
            case EnemyIntentType.Defend:
                icon = intentDefendSprite;
                break;
            default:
                icon = intentIdleSprite;
                break;
        }

        intentIconRenderer.sprite = icon;
        intentIconRenderer.enabled = (icon != null);
        // 位置跟著敵人 + 偏移
        intentIconRenderer.transform.position = transform.position + intentWorldOffset;

        // === 處理攻擊數字 ===
        if (intentValueText != null)
        {
            // 只有「攻擊」狀態而且傷害 > 0 才顯示數字
            if (nextIntent.type == EnemyIntentType.Attack && nextIntent.value > 0)
            {
                intentValueText.gameObject.SetActive(true);
                intentValueText.text = nextIntent.value.ToString();

                // 位置：跟圖示或跟敵人，再加偏移
                Vector3 basePos = transform.position;
                if (intentIconRenderer != null)
                    basePos = intentIconRenderer.transform.position;

                intentValueText.transform.position = basePos + intentValueOffset;
            }
            else
            {
                intentValueText.gameObject.SetActive(false);
            }
        }
    }


    public virtual void EnemyAction(Player player)        // 敵人執行動作
    {
        if (frozenTurns > 0)                     // 冰凍回合中不能行動
        {
            frozenTurns--;
            return;
        }
        if (buffs.stun > 0)                       // 暈眩回合中不能行動
        {
            buffs.stun--;
            return;
        }
        if (IsPlayerInRange(player))
        {
            int atkValue = CalculateAttackDamage();
            if (atkValue > 0)
            {
                player.TakeDamage(atkValue);         // 對玩家造成傷害
            }
        }
        else
        {
            MoveOneStepTowards(player);           // 移動一格接近玩家
        }
    }

    protected virtual void Die()                                    // 死亡處理
    {
        Debug.Log(enemyName + " died!");
         BattleManager bm = FindObjectOfType<BattleManager>();
        if (bm != null)
        {
            bm.OnEnemyDefeated(this);
        }
        Destroy(gameObject);                     // 刪除自身
    }
}

[System.Serializable]
public class EnemyBuffs                        // 敵人 Buff 結構
{
    public int stun = 0;                         // 暈眩回合數
    public void ClearSomeBuff(int count)         // 清除指定層數的 Buff
    {
        stun = Mathf.Max(0, stun - count);
    }
}