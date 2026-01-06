using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    public string enemyName = "Slime";
    public int maxHP = 30;
    public int currentHP;
    public int block = 0;
    public virtual bool ShouldResetBlockEachTurn => true;

    [Header("擊敗獎勵")]
    [SerializeField] private int goldReward = 3;
    public int GoldReward => goldReward;

    public int burningTurns = 0;
    public int frozenTurns = 0;
    public bool thunderstrike = false;
    public bool superconduct = false;

    public bool hasBerserk = false;
    public EnemyBuffs buffs = new EnemyBuffs();
    public Vector2Int gridPosition;

    [Header("攻擊設定")]
    [SerializeField] private int baseAttackDamage = 10;

    [Header("攻擊範圍偏移")]
    public List<Vector2Int> attackRangeOffsets = new List<Vector2Int>
    {
        new Vector2Int(-2,0), new Vector2Int(2,0),
        new Vector2Int(-1,-2), new Vector2Int(1,-2),
        new Vector2Int(-1,2), new Vector2Int(1,2)
    };

    public bool isBoss = false;

    public event Action<Enemy> ElementTagsChanged;

    [SerializeField] internal Transform spriteRoot;

    [Header("動畫設定")]
    [SerializeField] internal bool useAppearAnimation = true;
    [SerializeField] internal float deathDestroyDelay = 0.6f;
    [Header("Idle Overlays (W/H)")]
    [SerializeField] internal GameObject idleWhiteObj;
    [SerializeField] internal GameObject idleRedObj;

    private bool isCardTargeted = false;

    [Header("圖層排序設定")]
    [SerializeField] internal int sortingOrderBase = 0;
    [SerializeField] internal float sortingOrderMultiplier = 100f;

    [Header("受擊抖動設定")]
    [SerializeField] internal float shakeDuration = 0.1f;
    [SerializeField] internal float shakeMagnitude = 0.1f;
    [SerializeField] internal float scaleMultiplier = 1.1f;

    [Header("Intent (Sprite Icon)")]
    [Tooltip("這隻敵人是否可以移動，用來決定意圖要顯示 Move 還是 Idle")]
    public bool canMove = true;

    [Tooltip("下一回合預計要做的行動（邏輯用）")]
    public EnemyIntent nextIntent = new EnemyIntent();

    [Tooltip("掛在敵人身上的 SpriteRenderer，用來顯示意圖小圖")]
    public SpriteRenderer intentIconRenderer;
    private bool forceHideIntent = false;

    [Tooltip("意圖圖示相對於敵人中心的位置偏移")]
    public Vector3 intentWorldOffset = new Vector3(0f, 2f, 0f);

    [Header("Intent Icon Sprites")]
    public Sprite intentAttackSprite;
    public Sprite intentMoveSprite;
    public Sprite intentIdleSprite;
    public Sprite intentDefendSprite;
    public Sprite intentSkillSprite;

    [Header("Intent Value (Attack Number)")]
    [Tooltip("顯示攻擊數值用的 TextMeshPro (世界空間文字)")]
    public Text intentValueText;

    [Tooltip("數字文字相對於圖示的位置偏移")]
    public Vector3 intentValueOffset = new Vector3(0.8f, 0.1f, 0f);

    [SerializeField] internal GameObject highlightFx;
    [Header("滑鼠懸停提示")]
    [SerializeField] internal GameObject hoverIndicator2D;
    [SerializeField] internal float hoverIndicatorDelaySeconds = 0f;

    private bool isDead = false;

    private EnemyCombat combat;
    private EnemyElements elements;
    private EnemyIntentController intent;
    private EnemyMovement movement;
    private EnemyVisual visual;
    private EnemySortingController sorting;
    private EnemyMouseInteractor mouseInteractor;

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

    public void SetCardTargeted(bool on)
    {
        isCardTargeted = on;
        RefreshIdleOverlaysInternal();
    }

    private bool IsBodyInIdle()
    {
        return visual != null && visual.IsBodyInIdle();
    }

    internal void RefreshIdleOverlaysInternal()
    {
        bool inIdle = IsBodyInIdle();

        if (idleWhiteObj != null)
            idleWhiteObj.SetActive(inIdle);

        if (idleRedObj != null)
            idleRedObj.SetActive(inIdle && isCardTargeted);
    }

    internal void HideIdleOverlaysInternal()
    {
        if (idleWhiteObj != null) idleWhiteObj.SetActive(false);
        if (idleRedObj != null) idleRedObj.SetActive(false);
    }

    public void SetForceHideIntent(bool hide)
    {
        forceHideIntent = hide;
        UpdateIntentIcon();
    }

    public void MoveToPosition(Vector2Int targetGridPos)
    {
        movement?.MoveToPosition(targetGridPos);
    }

    protected internal virtual void MoveOneStepTowards(Player player)
    {
        movement?.MoveOneStepTowards(player);
    }

    public virtual bool IsPlayerInRange(Player player)
    {
        return movement != null && movement.IsPlayerInRange(player);
    }

    public virtual void TakeDamage(int dmg)
    {
        combat?.TakeDamage(dmg);
    }

    public virtual void TakeTrueDamage(int dmg)
    {
        combat?.TakeTrueDamage(dmg);
    }

    public void AddBlock(int amount)
    {
        combat?.AddBlock(amount);
    }

    public void ReduceBlock(int amount)
    {
        combat?.ReduceBlock(amount);
    }

    public void DispelBuff(int count)
    {
        buffs.ClearSomeBuff(count);
    }

    public bool HasElement(ElementType e)
    {
        return elements != null && elements.HasElement(e);
    }

    public void AddElementTag(ElementType e)
    {
        elements?.AddElementTag(e);
    }

    public void RemoveElementTag(ElementType e)
    {
        elements?.RemoveElementTag(e);
    }

    public IEnumerable<ElementType> GetElementTags()
    {
        return elements != null ? elements.GetElementTags() : Array.Empty<ElementType>();
    }

    public IEnumerable<ElementType> GetElementTagsByRecentOrder()
    {
        return elements != null ? elements.GetElementTagsByRecentOrder() : Array.Empty<ElementType>();
    }
    
    public int ApplyElementalAttack(ElementType e, int baseDamage, Player player)
    {
        return elements != null ? elements.ApplyElementalAttack(e, baseDamage, player) : baseDamage;
    }

    public virtual void ProcessTurnStart()
    {
        elements?.ProcessTurnStart();
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

    protected internal virtual int CalculateAttackDamage()
    {
        int atkValue = GetBaseAttackDamage();
        if (hasBerserk) atkValue += 5;
        return atkValue;
    }

    public virtual void DecideNextIntent(Player player)
    {
        intent?.DecideNextIntent(player);
    }

    public void UpdateIntentIcon()
    {
        intent?.UpdateIntentIcon();
    }

    public void SetHighlight(bool on)
    {
        visual?.SetHighlight(on);
    }

    public void SyncHighlightAnimation()
    {
        visual?.SyncHighlightAnimation();
    }

    public void HitShake()
    {
        visual?.PlayHitShake();
    }

    public void UpdateIdleOverlay()
    {
        visual?.RefreshIdleOverlays();
    }

    public void UpdateSpriteSortingOrder()
    {
        sorting?.UpdateNow();
    }

    public virtual void EnemyAction(Player player)
    {
        if (frozenTurns > 0)
        {
            frozenTurns--;
            return;
        }
        if (buffs.stun > 0)
        {
            buffs.stun--;
            return;
        }
        if (IsPlayerInRange(player))
        {
            int atkValue = CalculateAttackDamage();
            if (atkValue > 0)
            {
                visual?.PlayAttackAnimation();
                player.TakeDamage(atkValue);
            }
        }
        else
        {
            MoveOneStepTowards(player);
        }
    }

    protected internal virtual void Die()
    {
        combat?.Die();
    }

    protected virtual void Awake()
    {
        EnsureComponents();

        combat?.HandleAwake();
        sorting?.HandleAwake();
        intent?.HandleAwake();
        visual?.HandleAwake();
    }

    protected virtual void OnValidate()
    {
        EnsureComponents();
    }

    private void OnEnable()
    {
        sorting?.HandleOnEnable();
        visual?.HandleOnEnable();
        mouseInteractor?.HandleOnEnable();
    }

    private void OnDisable()
    {
        mouseInteractor?.HandleOnDisable();
    }

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

    private void OnMouseDown()
    {
        mouseInteractor?.HandleMouseDown();
    }

    private void OnMouseEnter()
    {
        mouseInteractor?.HandleMouseEnter();
    }

    private void OnMouseExit()
    {
        mouseInteractor?.HandleMouseExit();
    }

    internal void MarkDead()
    {
        if (isDead) return;
        isDead = true;
    }

    internal void RaiseElementTagsChanged()
    {
        ElementTagsChanged?.Invoke(this);
    }

    internal Transform GetSpriteRoot()
    {
        return spriteRoot ? spriteRoot : transform;
    }

    internal T GetOrAdd<T>() where T : Component
    {
        var comp = GetComponent<T>();
        if (comp == null)
        {
            comp = gameObject.AddComponent<T>();
        }
        return comp;
    }

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

    public void SetMoveBool(bool moving)
    {
        visual?.SetMoveBool(moving);
    }
}

[System.Serializable]
public class EnemyBuffs
{
    public int stun = 0;
    public void ClearSomeBuff(int count)
    {
        stun = Mathf.Max(0, stun - count);
    }
}
