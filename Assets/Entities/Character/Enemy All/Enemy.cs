using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;

/// <summary>
/// 敵人的核心共用類別。
/// 這支類別本身不把所有邏輯都塞在一起，而是扮演「總控管者」：
/// 1. 保存敵人的基礎屬性與狀態。
/// 2. 串接 Combat / Elements / Intent / Movement / Visual 等子模組。
/// 3. 提供敵人在戰鬥中最常用的共用入口。
/// </summary>
public class Enemy : MonoBehaviour
{
    // 目前場上所有啟用中的敵人，方便戰鬥系統一次收集。
    private static readonly HashSet<Enemy> ActiveEnemies = new HashSet<Enemy>();

    // 一般敵人行動完成後，輪到下一隻前的預設延遲。
    private const float DefaultSequentialActionDelay = 0.2f;
    // Idle 或因冰凍跳過行動時，使用較短的延遲。
    private const float IdleSequentialActionDelay = 0.08f;
    // 移動型行動完成後額外補的短延遲，讓節奏更順。
    private const float PostMoveSequentialActionDelay = 0.05f;

    // 敵人名稱。
    public string enemyName = "Slime";
    // 最大生命值。
    public int maxHP = 30;
    // 目前生命值。
    public int currentHP;

    // 與舊欄位名稱 "block" 保持相容，避免舊 prefab 遺失資料。
    [FormerlySerializedAs("block")]
    [SerializeField] private int currentBlock = 0;

    /// <summary>
    /// 對外公開的格擋值。
    /// 寫入時會自動夾到 0 以上，並在變化時通知 UI 更新。
    /// </summary>
    public int block
    {
        get => currentBlock;
        set
        {
            // 格擋不允許變成負數。
            int clamped = Mathf.Max(0, value);
            if (currentBlock == clamped)
            {
                // 沒變化就不做多餘刷新。
                return;
            }

            currentBlock = clamped;
            // 狀態值改變後，讓 HUD / Buff UI 等監聽者同步更新。
            RaiseStatusChanged();
        }
    }

    // 預設每回合都會清除格擋；特殊敵人可覆寫成 false。
    public virtual bool ShouldResetBlockEachTurn => true;

    [Header("掉落獎勵")]
    // 敵人死亡後掉落的金幣數。
    [SerializeField] private int goldReward = 3;
    // 對外唯讀的掉金幣屬性。
    public int GoldReward => goldReward;

    // 燃燒剩餘回合數。
    public int burningTurns = 0;
    // 冰凍剩餘回合數。
    public int frozenTurns = 0;
    public int immobilizedTurns = 0;
    // 是否處於超導狀態。
    public bool superconduct = false;
    // 蓄電次數。
    public int chargedCount = 0;
    // 結霜層數。
    public int frostStacks = 0;
    private int pendingFrostStacksForNextDamage = -1;
    // 目前在棋盤上的座標。
    public Vector2Int gridPosition;

    [Header("基礎攻擊")]
    // 基礎攻擊傷害。
    [SerializeField] private int baseAttackDamage = 10;

    [Header("預設攻擊範圍")]
    // 預設的攻擊偏移範圍，多數普通敵人可直接使用。
    public List<Vector2Int> attackRangeOffsets = new List<Vector2Int>
    {
        new Vector2Int(-2, 0), new Vector2Int(2, 0),
        new Vector2Int(-1, -2), new Vector2Int(1, -2),
        new Vector2Int(-1, 2), new Vector2Int(1, 2)
    };

    // 是否為 Boss。
    public bool isBoss = false;

    // 元素標籤變動時通知外部。
    public event Action<Enemy> ElementTagsChanged;
    // 狀態值改變時通知外部。
    public event System.Action OnStatusChanged;

    // 敵人主要的美術根節點。
    [SerializeField] internal Transform spriteRoot;

    [Header("登場與死亡")]
    // 是否使用登場動畫。
    [SerializeField] internal bool useAppearAnimation = true;
    // 死亡後延遲多久才真正刪除物件。
    [SerializeField] internal float deathDestroyDelay = 0.6f;

    [Header("Idle Overlays (W/H)")]
    // 待機時顯示的白色外框。
    [SerializeField] internal GameObject idleWhiteObj;
    // 被卡牌鎖定且待機時顯示的紅色外框。
    [SerializeField] internal GameObject idleRedObj;

    // 目前是否被卡牌選為目標。
    private bool isCardTargeted = false;

    [Header("排序設定")]
    // 排序基底值。
    [SerializeField] internal int sortingOrderBase = 0;
    // 根據座標換算排序時使用的倍率。
    [SerializeField] internal float sortingOrderMultiplier = 100f;

    [Header("受擊表現")]
    // 抖動持續時間。
    [SerializeField] internal float shakeDuration = 0.1f;
    // 抖動幅度。
    [SerializeField] internal float shakeMagnitude = 0.1f;
    // 放大倍率。
    [SerializeField] internal float scaleMultiplier = 1.1f;

    [Header("Intent (Sprite Icon)")]
    [Tooltip("此敵人是否允許移動；若為 false，無法執行 Move 意圖。")]
    // 是否允許移動。
    public bool canMove = true;

    [Tooltip("下一回合預計要顯示給玩家看的意圖資料。")]
    // 下一個意圖資料。
    public EnemyIntent nextIntent = new EnemyIntent();

    // 敵人底部 HUD。
    protected EnemyBottomHudFinal bottomHud;

    [Tooltip("顯示意圖圖示的 SpriteRenderer。")]
    // 世界座標上的意圖圖示。
    public SpriteRenderer intentIconRenderer;

    // 是否強制隱藏意圖圖示。
    private bool forceHideIntent = false;

    [Tooltip("意圖圖示在世界座標中的偏移。")]
    // 意圖圖示偏移位置。
    public Vector3 intentWorldOffset = new Vector3(0f, 2f, 0f);

    [Header("Intent Icon Sprites")]
    // 不同行為對應的意圖圖片。
    public Sprite intentAttackSprite;
    public Sprite intentMoveSprite;
    public Sprite intentIdleSprite;
    public Sprite intentDefendSprite;
    public Sprite intentSkillSprite;

    [Header("Intent Value (Attack Number)")]
    [Tooltip("顯示攻擊數字等資訊的 Text 元件。")]
    // 顯示意圖數值的文字元件。
    public Text intentValueText;

    [Tooltip("意圖數值的顯示偏移。")]
    // 意圖數值的偏移位置。
    public Vector3 intentValueOffset = new Vector3(0.8f, 0.1f, 0f);

    // 被 hover / 被選取時的強調特效。
    [SerializeField] internal GameObject highlightFx;

    [Header("技能說明")]
    [TextArea(2, 6)]
    // 技能型意圖時顯示的描述文字。
    public string skillDescription;

    // 是否已經死亡，用來避免重複進入死亡流程。
    private bool isDead = false;

    // 以下是 Enemy 所依賴的各個子模組。
    private EnemyCombat combat;
    private EnemyElements elements;
    private EnemyIntentController intent;
    private EnemyMovement movement;
    private EnemyVisual visual;
    private EnemySortingController sorting;
    private EnemyMouseInteractor mouseInteractor;
    private EnemyAreaDamagePreview areaDamagePreview;

    // 對外暴露的內部唯讀存取器。
    internal EnemyCombat Combat => combat;
    internal EnemyElements Elements => elements;
    internal EnemyIntentController Intent => intent;
    internal EnemyMovement Movement => movement;
    internal EnemyVisual Visual => visual;
    internal EnemySortingController Sorting => sorting;
    internal EnemyMouseInteractor MouseInteractor => mouseInteractor;

    internal bool IsDead => isDead;
    internal bool ForceHideIntent => forceHideIntent;
    internal float DeathDestroyDelay => deathDestroyDelay;

    /// <summary>
    /// Unity Start：
    /// 初始化底部 HUD，並註冊狀態更新事件。
    /// </summary>
    protected virtual void Start()
    {
        bottomHud = GetComponentInChildren<EnemyBottomHudFinal>(true);

        // 狀態變化時同步更新 HUD。
        OnStatusChanged += RefreshHud;

        // 啟動時先刷新一次，確保畫面顯示正確。
        RefreshHud();
    }

    /// <summary>
    /// 刷新底部 HUD 顯示。
    /// </summary>
    protected void RefreshHud()
    {
        if (bottomHud == null) return;
        bottomHud.Refresh(nextIntent.type, nextIntent.value);
    }

    /// <summary>
    /// 設定此敵人是否正被卡牌鎖定。
    /// </summary>
    public void SetCardTargeted(bool on)
    {
        isCardTargeted = on;
        RefreshIdleOverlaysInternal();
    }

    /// <summary>
    /// 顯示或隱藏範圍傷害預覽。
    /// </summary>
    public void SetAreaDamagePreview(bool on)
    {
        // 如果只是要關閉，而且目前根本沒有預覽物件，就直接跳出。
        if (!on && areaDamagePreview == null)
        {
            return;
        }

        // 第一次使用時才建立 / 取得預覽元件。
        if (areaDamagePreview == null)
        {
            areaDamagePreview = GetComponentInChildren<EnemyAreaDamagePreview>(true);
            if (areaDamagePreview == null)
            {
                areaDamagePreview = gameObject.AddComponent<EnemyAreaDamagePreview>();
            }

            areaDamagePreview.Init(this);
        }

        areaDamagePreview.SetVisible(on);
    }

    /// <summary>
    /// 檢查敵人本體動畫目前是否處於 Idle。
    /// </summary>
    private bool IsBodyInIdle()
    {
        return visual != null && visual.IsBodyInIdle();
    }

    /// <summary>
    /// 依目前狀態刷新待機外框。
    /// </summary>
    internal void RefreshIdleOverlaysInternal()
    {
        bool inIdle = IsBodyInIdle();

        if (idleWhiteObj != null)
            idleWhiteObj.SetActive(inIdle);

        if (idleRedObj != null)
            idleRedObj.SetActive(inIdle && isCardTargeted);
    }

    /// <summary>
    /// 強制關閉待機外框。
    /// </summary>
    internal void HideIdleOverlaysInternal()
    {
        if (idleWhiteObj != null) idleWhiteObj.SetActive(false);
        if (idleRedObj != null) idleRedObj.SetActive(false);
    }

    /// <summary>
    /// 強制顯示 / 隱藏意圖圖示。
    /// </summary>
    public void SetForceHideIntent(bool hide)
    {
        forceHideIntent = hide;
        UpdateIntentIcon();
    }

    /// <summary>
    /// 直接移動到指定格子。
    /// </summary>
    public void MoveToPosition(Vector2Int targetGridPos)
    {
        movement?.MoveToPosition(targetGridPos);
    }

    /// <summary>
    /// 朝玩家方向移動一步。
    /// </summary>
    protected internal virtual void MoveOneStepTowards(Player player)
    {
        movement?.MoveOneStepTowards(player);
    }

    /// <summary>
    /// 檢查玩家是否在攻擊範圍內。
    /// </summary>
    public virtual bool IsPlayerInRange(Player player)
    {
        return movement != null && movement.IsPlayerInRange(player);
    }

    /// <summary>
    /// 受到一般傷害。
    /// </summary>
    public virtual void TakeDamage(int dmg)
    {
        // 先讓蓄電影響這次傷害，再交給 Combat 處理扣血 / 格擋 / 死亡。
        ApplyChargedEffect(ref dmg);
        combat?.TakeDamage(dmg);
    }

    /// <summary>
    /// 受到真傷。
    /// </summary>
    public virtual void TakeTrueDamage(int dmg)
    {
        // 真傷同樣會先受蓄電影響，再交給 Combat 處理。
        ApplyChargedEffect(ref dmg);
        combat?.TakeTrueDamage(dmg);
    }

    /// <summary>
    /// 增加格擋。
    /// </summary>
    public void AddBlock(int amount)
    {
        combat?.AddBlock(amount);
    }

    /// <summary>
    /// 減少格擋。
    /// </summary>
    public void ReduceBlock(int amount)
    {
        combat?.ReduceBlock(amount);
    }

    /// <summary>
    /// 檢查是否具有指定元素標籤。
    /// </summary>
    public bool HasElement(ElementType e)
    {
        return elements != null && elements.HasElement(e);
    }

    /// <summary>
    /// 加入元素標籤。
    /// </summary>
    public void AddElementTag(ElementType e)
    {
        elements?.AddElementTag(e);
    }

    /// <summary>
    /// 移除元素標籤。
    /// </summary>
    public void RemoveElementTag(ElementType e)
    {
        elements?.RemoveElementTag(e);
    }

    /// <summary>
    /// 取得目前所有元素標籤。
    /// </summary>
    public IEnumerable<ElementType> GetElementTags()
    {
        return elements != null ? elements.GetElementTags() : Array.Empty<ElementType>();
    }

    /// <summary>
    /// 依最近附著順序取得元素標籤。
    /// </summary>
    public IEnumerable<ElementType> GetElementTagsByRecentOrder()
    {
        return elements != null ? elements.GetElementTagsByRecentOrder() : Array.Empty<ElementType>();
    }

    /// <summary>
    /// 套用蓄電影響。
    /// 當 chargedCount 下降到 0 的那次受傷，傷害會翻倍。
    /// </summary>
    private void ApplyChargedEffect(ref int dmg)
    {
        if (chargedCount <= 0) return;

        chargedCount--;
        RaiseStatusChanged();

        // 只有最後一次蓄電被消耗掉時，才把這次傷害翻倍。
        if (chargedCount == 0 && dmg > 0)
        {
            dmg = Mathf.CeilToInt(dmg * 2f);
        }
    }

    /// <summary>
    /// 設定蓄電次數。
    /// </summary>
    public void SetChargedCount(int count)
    {
        if (chargedCount == count) return;
        chargedCount = count;
        RaiseStatusChanged();
    }

    /// <summary>
    /// 增加結霜層數，並限制最大值為 6。
    /// </summary>
    public void AddFrostStacks(int amount)
    {
        if (amount <= 0) return;
        SetFrostStacks(Mathf.Min(6, frostStacks + amount));
    }

    /// <summary>
    /// 直接設定結霜層數，範圍限制在 0~6。
    /// </summary>
    public void SetFrostStacks(int count)
    {
        int clamped = Mathf.Clamp(count, 0, 6);
        if (frostStacks == clamped) return;
        frostStacks = clamped;
        RaiseStatusChanged();
    }

    /// <summary>
    /// 對此敵人施加元素攻擊。
    /// 真正的元素反應邏輯交給 EnemyElements 處理。
    /// </summary>
    public int ApplyElementalAttack(ElementType e, int baseDamage, Player player)
    {
        return elements != null ? elements.ApplyElementalAttack(e, baseDamage, player) : baseDamage;
    }

    public void SnapshotFrostStacksForNextDamage()
    {
        pendingFrostStacksForNextDamage = Mathf.Max(0, frostStacks);
    }

    public int ConsumeFrostStacksForDamage()
    {
        if (pendingFrostStacksForNextDamage < 0)
        {
            return frostStacks;
        }

        int snapshot = pendingFrostStacksForNextDamage;
        pendingFrostStacksForNextDamage = -1;
        return snapshot;
    }

    /// <summary>
    /// 敵人回合開始時的通用處理。
    /// </summary>
    public virtual void ProcessTurnStart()
    {
        elements?.ProcessTurnStart();
    }

    /// <summary>
    /// 玩家回合結束時的通用處理。
    /// </summary>
    public virtual void ProcessPlayerTurnEnd()
    {
        ApplyBurningTurnEnd();
        elements?.ProcessPlayerTurnEnd();
    }

    /// <summary>
    /// 處理燃燒在玩家回合結束時的結算。
    /// </summary>
    private void ApplyBurningTurnEnd()
    {
        if (burningTurns <= 0) return;

        // 燃燒目前每次固定造成 3 點一般傷害。
        TakeDamage(3);
        burningTurns--;
        RaiseStatusChanged();

        // 燃燒結束後，順便把木元素標籤移除。
        if (burningTurns == 0)
        {
            RemoveElementTag(ElementType.Wood);
        }
    }

    /// <summary>
    /// 敵人回合結束時的通用處理。
    /// </summary>
    public virtual void ProcessEnemyTurnEnd()
    {
        combat?.HandleEnemyTurnEnd();
    }

    /// <summary>
    /// 基礎攻擊值對外屬性。
    /// </summary>
    public int BaseAttackDamage
    {
        get => baseAttackDamage;
        set => baseAttackDamage = Mathf.Max(0, value);
    }

    // 是否支援小隊共用戰術。
    public virtual bool SupportsSharedSquadTactics => true;

    // 是否真的可以參與共用戰術。
    // 某些特殊敵人不適用統一協作規則，所以這裡排除。
    public bool CanUseSharedSquadTactics =>
        SupportsSharedSquadTactics &&
        this is not StoneToad &&
        this is not GouShe &&
        this is not HuGuPo &&
        this is not DiNiu &&
        this is not FeiLuYao &&
        this is not YingGe &&
        this is not YingGeStone;

    /// <summary>
    /// 取得基礎攻擊值。
    /// </summary>
    protected virtual int GetBaseAttackDamage()
    {
        return Mathf.Max(0, baseAttackDamage);
    }

    /// <summary>
    /// 計算最終攻擊傷害。
    /// </summary>
    protected internal virtual int CalculateAttackDamage()
    {
        return GetBaseAttackDamage();
    }

    /// <summary>
    /// 決定下一個要顯示的意圖。
    /// </summary>
    public virtual void DecideNextIntent(Player player)
    {
        EnemyActionPlan plan = BuildIntentPlan(player);
        nextIntent.type = plan.IntentType;
        nextIntent.value = plan.IntentValue;
        intent?.UpdateIntentIcon();
        // 更新 HUD，讓數字與 icon 同步。
        RefreshHud();
    }

    /// <summary>
    /// 單純刷新目前的意圖 icon。
    /// </summary>
    public void UpdateIntentIcon()
    {
        intent?.UpdateIntentIcon();
        // 更新 HUD，避免顯示舊資料。
        RefreshHud();
    }

    /// <summary>
    /// 開關高亮。
    /// </summary>
    public void SetHighlight(bool on)
    {
        visual?.SetHighlight(on);
    }

    /// <summary>
    /// 同步高亮動畫狀態。
    /// </summary>
    public void SyncHighlightAnimation()
    {
        visual?.SyncHighlightAnimation();
    }

    /// <summary>
    /// 播放受擊抖動。
    /// </summary>
    public void HitShake()
    {
        visual?.PlayHitShake();
    }

    /// <summary>
    /// 刷新 Idle 外框。
    /// </summary>
    public void UpdateIdleOverlay()
    {
        visual?.RefreshIdleOverlays();
    }

    /// <summary>
    /// 立即刷新角色排序。
    /// </summary>
    public void UpdateSpriteSortingOrder()
    {
        sorting?.UpdateNow();
    }

    /// <summary>
    /// 主動通知外部：狀態改變了。
    /// </summary>
    public void RaiseStatusChanged()
    {
        OnStatusChanged?.Invoke();
    }

    /// <summary>
    /// 設定燃燒回合數。
    /// </summary>
    public void SetBurningTurns(int turns)
    {
        if (burningTurns == turns) return;
        burningTurns = turns;
        RaiseStatusChanged();
    }

    /// <summary>
    /// 設定冰凍回合數。
    /// </summary>
    public void SetFrozenTurns(int turns)
    {
        if (frozenTurns == turns) return;
        frozenTurns = turns;
        RaiseStatusChanged();
    }

    public void SetImmobilizedTurns(int turns)
    {
        int clamped = Mathf.Max(0, turns);
        if (immobilizedTurns == clamped) return;
        immobilizedTurns = clamped;
        RaiseStatusChanged();
    }

    public bool CanMoveThisTurn()
    {
        return canMove && immobilizedTurns <= 0;
    }

    /// <summary>
    /// 判斷敵人目前是否帶有會顯示在 UI 上的負面效果。
    /// 目前和狀態面板一致，包含燃燒、冰凍、雷擊、結霜。
    /// </summary>
    public bool HasNegativeStatusEffect()
    {
        return burningTurns > 0 ||
               frozenTurns > 0 ||
               immobilizedTurns > 0 ||
               chargedCount > 0 ||
               frostStacks > 0;
    }

    /// <summary>
    /// 讓敵人執行一次實際行動。
    /// </summary>
    public virtual void EnemyAction(Player player)
    {
        // 冰凍時本回合直接跳過行動，並消耗 1 回合冰凍。
        if (frozenTurns > 0)
        {
            SetFrozenTurns(Mathf.Max(0, frozenTurns - 1));
            return;
        }

        EnemyActionPlan plan = BuildExecutionPlan(player);
        switch (plan.IntentType)
        {
            case EnemyIntentType.Attack:
                if (player != null && plan.IntentValue > 0)
                {
                    visual?.PlayAttackAnimation();
                    player.TakeDamage(plan.IntentValue);
                }
                break;

            case EnemyIntentType.Move:
                // 若有小隊協調給出的指定目標位置，就照協調結果走。
                if (plan.HasTargetPosition && CanUseSharedSquadTactics)
                {
                    MoveToPosition(plan.TargetPosition);
                }
                // 否則就走原本的單步追擊邏輯。
                else if (CanMoveThisTurn())
                {
                    MoveOneStepTowards(player);
                }
                break;
        }
    }

    /// <summary>
    /// 敵人行動的 coroutine 版本。
    /// 用於戰鬥流程中串接多隻敵人的演出。
    /// </summary>
    public virtual IEnumerator EnemyActionRoutine(Player player)
    {
        bool wasFrozenBeforeAction = frozenTurns > 0;
        EnemyIntentType plannedIntent = nextIntent.type;

        EnemyAction(player);
        ConsumeImmobilizedTurn();

        // 如果本次行動包含位移，就等位移完成後再結束。
        if (movement != null && movement.IsMoving)
        {
            yield return new WaitUntil(() => movement == null || !movement.IsMoving);

            if (PostMoveSequentialActionDelay > 0f)
            {
                yield return new WaitForSeconds(PostMoveSequentialActionDelay);
            }

            yield break;
        }

        float delay = GetSequentialActionDelay(plannedIntent, wasFrozenBeforeAction);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
    }

    /// <summary>
    /// 建立「顯示給玩家看的」意圖計畫。
    /// </summary>
    protected virtual EnemyActionPlan BuildIntentPlan(Player player)
    {
        Vector2Int? previousGridPos = movement != null ? movement.PreviousGridPosition : null;
        return EnemyActionScoringSystem.Evaluate(this, player, previousGridPos);
    }

    /// <summary>
    /// 根據本次預計行為，決定行動後需要等待多久。
    /// </summary>
    protected virtual float GetSequentialActionDelay(EnemyIntentType plannedIntent, bool wasFrozenBeforeAction)
    {
        if (wasFrozenBeforeAction || plannedIntent == EnemyIntentType.Idle)
        {
            return IdleSequentialActionDelay;
        }

        return DefaultSequentialActionDelay;
    }

    /// <summary>
    /// 建立真正執行時要用的行動計畫。
    /// 某些情況會優先吃小隊協調器提供的結果。
    /// </summary>
    protected virtual EnemyActionPlan BuildExecutionPlan(Player player)
    {
        if (CanUseSharedSquadTactics)
        {
            BattleRuntimeContext context = BattleRuntimeContext.Active;
            if (context != null &&
                context.SquadCoordinator != null &&
                context.SquadCoordinator.TryGetExecutionPlan(this, out EnemyActionPlan coordinatedPlan))
            {
                return coordinatedPlan;
            }
        }

        return BuildIntentPlan(player);
    }

    /// <summary>
    /// 死亡入口。
    /// 實際動畫與銷毀流程交給 Combat 模組處理。
    /// </summary>
    protected internal virtual void Die()
    {
        combat?.Die();
    }

    /// <summary>
    /// Unity Awake：
    /// 確保子模組存在，並讓各模組完成自己的 Awake 初始化。
    /// </summary>
    protected virtual void Awake()
    {
        EnsureComponents();

        combat?.HandleAwake();
        sorting?.HandleAwake();
        intent?.HandleAwake();
        visual?.HandleAwake();
    }

    /// <summary>
    /// 編輯器數值變動時也確保必要模組存在。
    /// </summary>
    protected virtual void OnValidate()
    {
        EnsureComponents();
    }

    /// <summary>
    /// Unity OnEnable：
    /// 註冊到 ActiveEnemies，並讓相關模組執行啟用流程。
    /// </summary>
    private void OnEnable()
    {
        ActiveEnemies.Add(this);
        sorting?.HandleOnEnable();
        visual?.HandleOnEnable();
        mouseInteractor?.HandleOnEnable();
    }

    /// <summary>
    /// Unity OnDisable：
    /// 從 ActiveEnemies 移除，並做互動模組收尾。
    /// </summary>
    private void OnDisable()
    {
        ActiveEnemies.Remove(this);
        mouseInteractor?.HandleOnDisable();
    }

    /// <summary>
    /// Unity LateUpdate：
    /// 排序、意圖、視覺更新，以及滑鼠 hover 刷新都放這裡。
    /// </summary>
    private void LateUpdate()
    {
        sorting?.HandleLateUpdate();
        intent?.HandleLateUpdate();
        visual?.HandleLateUpdate();

        bool shouldRefreshHover = mouseInteractor != null && mouseInteractor.ShouldRefreshHover();
        if (shouldRefreshHover)
        {
            mouseInteractor.RefreshHoverIndicator();
        }
    }

    /// <summary>
    /// Unity 滑鼠按下事件，轉交給互動模組。
    /// </summary>
    private void OnMouseDown()
    {
        mouseInteractor?.HandleMouseDown();
    }

    /// <summary>
    /// Unity 滑鼠移入事件，轉交給互動模組。
    /// </summary>
    private void OnMouseEnter()
    {
        mouseInteractor?.HandleMouseEnter();
    }

    /// <summary>
    /// Unity 滑鼠移出事件，轉交給互動模組。
    /// </summary>
    private void OnMouseExit()
    {
        mouseInteractor?.HandleMouseExit();
    }

    /// <summary>
    /// 標記為死亡。
    /// 只負責狀態，不處理動畫與刪除。
    /// </summary>
    internal void MarkDead()
    {
        if (isDead) return;
        isDead = true;
    }

    /// <summary>
    /// 通知外部：元素標籤改變了。
    /// </summary>
    internal void RaiseElementTagsChanged()
    {
        ElementTagsChanged?.Invoke(this);
    }

    /// <summary>
    /// 取得主要 sprite 根節點；若未指定就退回自身 transform。
    /// </summary>
    internal Transform GetSpriteRoot()
    {
        return spriteRoot ? spriteRoot : transform;
    }

    /// <summary>
    /// 取得指定元件；若不存在就自動補上一個。
    /// </summary>
    internal T GetOrAdd<T>() where T : Component
    {
        var comp = GetComponent<T>();
        if (comp == null)
        {
            comp = gameObject.AddComponent<T>();
        }
        return comp;
    }

    private void ConsumeImmobilizedTurn()
    {
        if (immobilizedTurns <= 0)
        {
            return;
        }

        SetImmobilizedTurns(immobilizedTurns - 1);
    }

    /// <summary>
    /// 確保 Enemy 需要的各個子模組都存在，並完成初始化。
    /// </summary>
    private void EnsureComponents()
    {
        combat = GetOrAdd<EnemyCombat>();
        combat.Init(this);

        elements = GetOrAdd<EnemyElements>();
        elements.Init(this);

        intent = GetOrAdd<EnemyIntentController>();
        intent.Init(this);

        movement = GetOrAdd<EnemyMovement>();
        movement.Init(this);

        visual = GetOrAdd<EnemyVisual>();
        visual.Init(this);

        sorting = GetOrAdd<EnemySortingController>();
        sorting.Init(this);

        mouseInteractor = GetComponent<EnemyMouseInteractor>();
        if (mouseInteractor == null)
        {
            mouseInteractor = gameObject.AddComponent<EnemyMouseInteractor>();
        }
        mouseInteractor.Init(this);
    }

    /// <summary>
    /// 設定移動動畫的 bool。
    /// </summary>
    public void SetMoveBool(bool moving)
    {
        visual?.SetMoveBool(moving);
    }

    /// <summary>
    /// 將目前所有啟用中的敵人填入外部提供的 List。
    /// </summary>
    public static void FillActiveEnemies(List<Enemy> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        foreach (Enemy enemy in ActiveEnemies)
        {
            if (enemy != null)
            {
                results.Add(enemy);
            }
        }
    }
}
