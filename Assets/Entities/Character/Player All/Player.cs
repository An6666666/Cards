using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerDeckController))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerBuffController))]
public class Player : MonoBehaviour
{
    [Header("Base Setup")]
    public int baseHandCardCount = 5;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;

    public List<RelicBase> relics = new List<RelicBase>();

    private PlayerStats stats;
    private PlayerDeckController deckController;
    private PlayerMovement movement;
    public PlayerBuffController buffs { get; private set; }

    private PlayerAnimatorController animCtrl;
    private PlayerEffectController effectCtrl;
    private PlayerDamageFeedbackController damageFeedbackCtrl;
    private PlayerLowHpFeedbackController lowHpFeedbackCtrl;
    private SpriteRenderer visualSpriteRenderer;
    private bool hasPendingTeleport;
    private Vector2Int pendingTeleportTarget;
    private bool pendingTeleportAllowsOccupiedTileRelic;

    private PlayerStats Stats => stats ??= GetComponent<PlayerStats>();
    private PlayerDeckController DeckController => deckController ??= GetComponent<PlayerDeckController>();
    private PlayerMovement Movement => movement ??= GetComponent<PlayerMovement>();

    public void PlayAttackAnim() => animCtrl?.PlayAttack();
    public void PlayDefendAnim() => animCtrl?.PlayDefend();
    public void PlayUtilityAnim() => animCtrl?.PlayUtility();
    public void PlayMoveCardAnim() => animCtrl?.PlayMoveCard();
    public void PlayMoveStarAnim() => animCtrl?.PlayMoveStar();
    public void PlayShieldHitFX() => effectCtrl?.PlayShieldHitFX();
    public void PlayShieldBreakFX() => effectCtrl?.PlayShieldBreakFX();
    public void PlayMoveStarFX() => effectCtrl?.PlayMoveStarFX();
    public void PlayHuGuPoSkillFX() => effectCtrl?.PlayHuGuPoSkillFX();
    public void PlayTeleportDisappearAnim() => animCtrl?.PlayTeleportDisappear();
    public void PlayTeleportLeaveFX() => effectCtrl?.PlayTeleportLeaveFX();
    public void PlayTeleportAppearAnim() => animCtrl?.PlayTeleportAppear();
    public void PlayTeleportAppearFX() => effectCtrl?.PlayTeleportAppearFX();
    public void PlayWoundedAnim() => animCtrl?.PlayWounded();
    public void PlayHitFeedback() => damageFeedbackCtrl?.PlayHitFeedback();
    public void PlayAttackHitShake() => damageFeedbackCtrl?.PlayAttackHitShake();
    public void PlayHurtSFX() => AudioManager.Instance?.PlayPlayerHurtSFX();
    public void PlayShieldBlockNoDamageSFX() => AudioManager.Instance?.PlayPlayerBlockNoDamageSFX();
    public void RefreshLowHpFeedback() => lowHpFeedbackCtrl?.RefreshLowHpState(currentHP, maxHP);
    public void SetMovingAnim(bool moving) => animCtrl?.SetMoving(moving);
    public void SetDeadAnim(bool dead) => animCtrl?.SetDead(dead);
    public void SetShieldFXActive(bool active) => effectCtrl?.SetShieldActive(active);
    public void RefreshDebuffFX() => effectCtrl?.SetDebuffFxState(
        buffs != null && buffs.bleed > 0,
        buffs != null && buffs.weak > 0,
        buffs != null && buffs.imprison > 0);

    public void FaceTowards(Transform target, bool faceRightByDefault = true)
    {
        if (target == null)
        {
            return;
        }

        FaceTowards(target.position, faceRightByDefault);
    }

    public void FaceTowards(Vector3 targetPosition, bool faceRightByDefault = true)
    {
        SpriteRenderer renderer = ResolveVisualSpriteRenderer();
        Transform faceOrigin = visualRoot != null ? visualRoot : transform;
        if (renderer == null || faceOrigin == null)
        {
            return;
        }

        float dx = targetPosition.x - faceOrigin.position.x;
        if (Mathf.Approximately(dx, 0f))
        {
            return;
        }

        bool shouldFaceRight = dx > 0f;
        renderer.flipX = faceRightByDefault ? !shouldFaceRight : shouldFaceRight;
    }

    public int maxHP
    {
        get => Stats.maxHP;
        set
        {
            Stats.maxHP = value;
            RefreshLowHpFeedback();
        }
    }

    public int currentHP
    {
        get => Stats.currentHP;
        set
        {
            Stats.currentHP = value;
            RefreshLowHpFeedback();
        }
    }

    public int maxEnergy { get => Stats.maxEnergy; set => Stats.maxEnergy = value; }
    public int energy { get => Stats.energy; set => Stats.energy = value; }
    public int block { get => Stats.block; set => Stats.SetBlock(value); }
    public int gold { get => Stats.gold; set => Stats.gold = value; }

    public List<CardBase> deck
    {
        get => DeckController.Deck;
        set => DeckController.Deck = value ?? new List<CardBase>();
    }

    public List<CardBase> Hand => DeckController.Hand;
    public List<CardBase> discardPile => DeckController.DiscardPile;
    public List<CardBase> exhaustPile => DeckController.ExhaustPile;

    public int exhaustCountThisTurn
    {
        get => DeckController.exhaustCountThisTurn;
        set => DeckController.exhaustCountThisTurn = value;
    }

    public Vector2Int position
    {
        get => Movement.position;
        set => Movement.position = value;
    }

    private void Awake()
    {
        relics = InstantiateRelicCopies(relics);

        stats = GetComponent<PlayerStats>();
        deckController = GetComponent<PlayerDeckController>();
        movement = GetComponent<PlayerMovement>();
        buffs = GetComponent<PlayerBuffController>();

        animCtrl = GetComponentInChildren<PlayerAnimatorController>(true);
        effectCtrl = GetComponentInChildren<PlayerEffectController>(true);
        damageFeedbackCtrl = GetComponentInChildren<PlayerDamageFeedbackController>(true);
        lowHpFeedbackCtrl = GetComponentInChildren<PlayerLowHpFeedbackController>(true);
        ResolveVisualRoot();

        stats.Initialize(this, buffs);
        deckController.Initialize(this, buffs);

        if (RunManager.Instance != null)
        {
            RunManager.Instance.RegisterPlayer(this);
        }

        RefreshLowHpFeedback();
        RefreshDebuffFX();
    }

    private static List<RelicBase> InstantiateRelicCopies(List<RelicBase> sourceRelics)
    {
        if (sourceRelics == null || sourceRelics.Count == 0)
        {
            return new List<RelicBase>();
        }

        List<RelicBase> instantiatedRelics = new List<RelicBase>(sourceRelics.Count);
        for (int i = 0; i < sourceRelics.Count; i++)
        {
            RelicBase relic = sourceRelics[i];
            if (relic == null)
            {
                continue;
            }

            instantiatedRelics.Add(UnityEngine.Object.Instantiate(relic));
        }

        return instantiatedRelics;
    }

    public void BeginTeleportSequence(Vector2Int targetGridPos, bool allowOccupiedTileRelic = false)
    {
        pendingTeleportTarget = targetGridPos;
        hasPendingTeleport = true;
        pendingTeleportAllowsOccupiedTileRelic = allowOccupiedTileRelic;
        PlayTeleportDisappearAnim();
    }

    public void OnTeleportDisappearEvent()
    {
        if (!hasPendingTeleport)
        {
            return;
        }

        PlayTeleportLeaveFX();
        SetVisualRootVisible(false);
        movement.TeleportToPosition(pendingTeleportTarget, pendingTeleportAllowsOccupiedTileRelic);
    }

    public void FinishTeleportVisual()
    {
        SetVisualRootVisible(true);
        PlayTeleportAppearFX();
        PlayTeleportAppearAnim();
        hasPendingTeleport = false;
        pendingTeleportAllowsOccupiedTileRelic = false;
    }

    public void StartTurn()
    {
        stats.StartTurn();
        deckController.StartTurn(baseHandCardCount);
        buffs.OnTurnStartReset();

        foreach (RelicBase relic in relics)
        {
            relic?.OnTurnStart(this);
        }
    }

    public void EndTurn()
    {
        if (buffs.needRandomDiscardAtEnd > 0)
        {
            int n = buffs.needRandomDiscardAtEnd;
            buffs.needRandomDiscardAtEnd = 0;
            for (int i = 0; i < n; i++)
            {
                if (!deckController.DiscardRandomCard(relics, false))
                {
                    break;
                }
            }
        }

        foreach (RelicBase relic in relics)
        {
            relic?.OnTurnEnd(this);
        }

        buffs.OnTurnEndReset(this);
    }

    public void RemoveAllNegativeEffects()
    {
        buffs.RemoveAllNegativeEffects(this);
    }

    public void RemoveEnemyNegativeEffects()
    {
        buffs.RemoveEnemyNegativeEffects(this);
    }

    public bool TryRemoveRandomNegativeEffect()
    {
        return buffs != null && buffs.TryRemoveRandomNegativeEffect(this);
    }

    public bool HasExhaustableCardInHand(CardBase excludedCard = null)
        => deckController.HasExhaustableCardInHand(excludedCard);

    public void DrawNewHand(int count) => deckController.DrawNewHand(count);
    public void ReshuffleDiscardIntoDeck() => deckController.ReshuffleDiscardIntoDeck();
    public void ShuffleDeck() => deckController.ShuffleDeck();

    public void DiscardCards(int n) => deckController.DiscardCards(n, relics);
    public bool DiscardOneCard() => deckController.DiscardOneCard(relics);
    public bool DiscardRandomCard() => deckController.DiscardRandomCard(relics);

    public bool ExhaustRandomCardFromHand(CardBase excludedCard = null)
        => deckController.ExhaustRandomCardFromHand(excludedCard);

    public bool CheckExhaustPlan() => deckController.CheckExhaustPlan();

    public void UseEnergy(int cost) => stats.UseEnergy(cost);
    public void GainEnergy(int amount)
    {
        stats.GainEnergy(amount);

        BattleManager manager = BattleRuntimeContext.Active != null
            ? BattleRuntimeContext.Active.Manager
            : null;

        manager?.RefreshHandMetaUI();
    }

    public int CalculateAttackDamage(int baseDamage) => stats.CalculateAttackDamage(baseDamage);
    public void AddBlock(int amount) => stats.AddBlock(amount, relics);
    public void TakeDamage(int dmg) => stats.TakeDamage(dmg);
    public void TakeDamageDirect(int dmg) => stats.TakeDamageDirect(dmg);
    public void TakeStatusDamage(int dmg) => stats.TakeStatusDamage(dmg);
    public void AddGold(int amount) => stats.AddGold(amount);

    public void DrawCards(int n) => deckController.DrawCards(n);
    public int GetCardCostModifier(CardBase card) => deckController.GetCardCostModifier(card);
    public void AddCardCostModifier(CardBase card, int modifier) => deckController.AddCardCostModifier(card, modifier);
    public void ClearCardCostModifier(CardBase card) => deckController.ClearCardCostModifier(card);
    public void ExhaustCard(CardBase card) => deckController.ExhaustCard(card);
    public RelicBase AcquireRelic(RelicBase relicTemplate)
    {
        if (relicTemplate == null)
        {
            return null;
        }

        RelicBase relicInstance = Instantiate(relicTemplate);
        relics.Add(relicInstance);
        relicInstance.OnAcquire(this);
        return relicInstance;
    }

    public void NotifyBattleStarted()
    {
        foreach (RelicBase relic in relics)
        {
            relic?.OnBattleStart(this);
        }
    }

    public void NotifyTurnStarted()
    {
        foreach (RelicBase relic in relics)
        {
            relic?.OnTurnStart(this);
        }
    }

    public void NotifyCardPlayed(CardBase card)
    {
        if (card == null)
        {
            return;
        }

        foreach (RelicBase relic in relics)
        {
            relic?.OnCardPlayed(this, card);
        }
    }

    public void NotifyCardPlayStarted(CardBase card)
    {
        if (card == null)
        {
            return;
        }

        foreach (RelicBase relic in relics)
        {
            relic?.OnCardPlayStarted(this, card);
        }
    }

    public void NotifyCardExhausted(CardBase card)
    {
        if (card == null)
        {
            return;
        }

        foreach (RelicBase relic in relics)
        {
            relic?.OnCardExhausted(this, card);
        }
    }

    public void NotifyEnemyDefeated(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        foreach (RelicBase relic in relics)
        {
            relic?.OnEnemyDefeated(this, enemy);
        }
    }

    public void NotifyAttackCardDamagedEnemy(AttackCardBase card, Enemy target, int hpDamage)
    {
        if (card == null || target == null || hpDamage <= 0)
        {
            return;
        }

        foreach (RelicBase relic in relics)
        {
            relic?.OnAttackCardDamagedEnemy(this, card, target, hpDamage);
        }
    }

    public void NotifyAttackCardHitEnemy(AttackCardBase card, Enemy target, int attemptedDamage, int hpDamage)
    {
        if (card == null || target == null || attemptedDamage <= 0)
        {
            target?.ClearAttackHitSnapshots();
            return;
        }

        AttackCardHitContext context = new AttackCardHitContext(target.ConsumeBurningTurnsForAttackHit());

        foreach (RelicBase relic in relics)
        {
            relic?.OnAttackCardHitEnemy(this, card, target, attemptedDamage, hpDamage, context);
        }
    }

    public int GetAdditionalCardDamage(CardBase card, Enemy target = null)
    {
        if (card == null)
        {
            return 0;
        }

        int bonusDamage = 0;
        foreach (RelicBase relic in relics)
        {
            if (relic == null)
            {
                continue;
            }

            bonusDamage += relic.GetAdditionalDamage(this, card, target);
        }

        return bonusDamage;
    }

    public float GetConductiveDamageMultiplier()
    {
        float multiplier = 0.5f;
        foreach (RelicBase relic in relics)
        {
            if (relic == null)
            {
                continue;
            }

            if (relic.TryGetConductiveDamageMultiplier(this, out float relicMultiplier))
            {
                multiplier = Mathf.Max(multiplier, relicMultiplier);
            }
        }

        return multiplier;
    }

    public bool TryGetGrowthTrapBonusElement(out ElementType element)
    {
        foreach (RelicBase relic in relics)
        {
            if (relic == null)
            {
                continue;
            }

            if (relic.TryGetGrowthTrapBonusElement(this, out element))
            {
                return true;
            }
        }

        element = default;
        return false;
    }

    public void NotifyFreezeReactionResolved(Enemy target, bool freezeApplied)
    {
        if (target == null)
        {
            return;
        }

        foreach (RelicBase relic in relics)
        {
            relic?.OnFreezeReactionResolved(this, target, freezeApplied);
        }
    }

    public bool HasRelic<T>() where T : RelicBase
    {
        for (int i = 0; i < relics.Count; i++)
        {
            if (relics[i] is T)
            {
                return true;
            }
        }

        return false;
    }

    public int GetRelicCount<T>() where T : RelicBase
    {
        int count = 0;
        for (int i = 0; i < relics.Count; i++)
        {
            if (relics[i] is T)
            {
                count++;
            }
        }

        return count;
    }

    public bool CanMoveToPosition(Vector2Int targetGridPos, bool allowOccupiedTileRelic = false)
        => Movement != null && Movement.CanMoveToPosition(targetGridPos, allowOccupiedTileRelic);

    public void MoveToPosition(Vector2Int targetGridPos, bool allowOccupiedTileRelic = false)
        => Movement.MoveToPosition(targetGridPos, allowOccupiedTileRelic);

    public void TeleportToPosition(Vector2Int targetPos, bool allowOccupiedTileRelic = false)
        => Movement.TeleportToPosition(targetPos, allowOccupiedTileRelic);

    private void ResolveVisualRoot()
    {
        if (visualRoot != null)
        {
            return;
        }

        Transform directChild = transform.Find("player");
        if (directChild != null)
        {
            visualRoot = directChild;
            return;
        }

        visualRoot = FindChildByName(transform, "player");
        if (visualRoot != null)
        {
            return;
        }

        if (animCtrl != null && animCtrl.transform != transform)
        {
            visualRoot = animCtrl.transform;
        }
    }

    private SpriteRenderer ResolveVisualSpriteRenderer()
    {
        if (visualSpriteRenderer != null)
        {
            return visualSpriteRenderer;
        }

        ResolveVisualRoot();

        if (animCtrl != null && animCtrl.animator != null)
        {
            visualSpriteRenderer = animCtrl.animator.GetComponent<SpriteRenderer>();
            if (visualSpriteRenderer != null)
            {
                return visualSpriteRenderer;
            }
        }

        if (visualRoot != null)
        {
            visualSpriteRenderer = visualRoot.GetComponent<SpriteRenderer>();
            if (visualSpriteRenderer == null)
            {
                visualSpriteRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);
            }

            if (visualSpriteRenderer != null)
            {
                return visualSpriteRenderer;
            }
        }

        visualSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        return visualSpriteRenderer;
    }

    private void SetVisualRootVisible(bool visible)
    {
        if (visualRoot == null)
        {
            ResolveVisualRoot();
        }

        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(visible);
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform match = FindChildByName(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
