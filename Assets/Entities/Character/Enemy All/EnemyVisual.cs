using System.Collections;
using System;
using UnityEngine;

public class EnemyVisual : MonoBehaviour
{
    private Enemy enemy;
    private Coroutine shakeRoutine;
    private Coroutine attackLungeRoutine;
    private Vector3 spriteDefaultLocalPosition;
    private Vector3 spriteDefaultLocalScale;
    private bool spriteDefaultsInitialized = false;
    private Animator spriteAnimator;
    private Animator highlightAnimator;
    private bool appearAnimationStarted;
    private float appearAnimationFallbackEndTime;

    private const float DefaultAppearFallbackDuration = 0.8f;
    private const float AttackLungeForwardDuration = 0.08f;
    private const float AttackLungeReturnDuration = 0.12f;
    private const float AttackLungeDistanceRatio = 0.42f;
    private const float AttackLungeMinDistance = 0.45f;
    private const float AttackLungeMaxDistance = 1.65f;
    private const float AttackLungeScaleMultiplier = 1.05f;

    private static readonly int HashAppear = Animator.StringToHash("Appear");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashHit = Animator.StringToHash("Hit");
    private static readonly int HashSkillStart = Animator.StringToHash("SkillStart");
    private static readonly int HashSkillEnd = Animator.StringToHash("SkillEnd");
    private static readonly int HashIsDead = Animator.StringToHash("IsDead");
    private static readonly int HashMove = Animator.StringToHash("Move");

    private bool hasAppeared = false;

    public void Init(Enemy owner)
    {
        enemy = owner;
    }

    public void HandleAwake()
    {
        EnsureAnimators();
        // Capture once on startup so later resets always return to a stable baseline.
        CaptureSpriteDefaults();
        enemy?.HideIdleOverlaysInternal();
        enemy?.RefreshIdleOverlaysInternal();
        PlayAppearAnimation();
    }

    public void HandleOnEnable()
    {
        SyncHighlightAnimation();
    }

    public void HandleOnDisable()
    {
        StopAttackLunge(resetVisual: false);

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        if (spriteDefaultsInitialized)
        {
            ResetSpriteVisual();
        }
    }

    public void HandleLateUpdate()
    {
        RefreshIdleOverlays();
    }

    public void CaptureSpriteDefaults()
    {
        Transform root = GetSpriteRoot();
        spriteDefaultLocalPosition = root.localPosition;
        spriteDefaultLocalScale = root.localScale;
        spriteDefaultsInitialized = true;
    }

    public void EnsureSpriteDefaults()
    {
        if (spriteDefaultsInitialized) return;
        CaptureSpriteDefaults();
    }

    public void ResetSpriteVisual()
    {
        EnsureSpriteDefaults();
        Transform root = GetSpriteRoot();
        root.localPosition = spriteDefaultLocalPosition;
        root.localScale = spriteDefaultLocalScale;
    }

    public void PlayAppearAnimation()
    {
        if (enemy == null || !enemy.useAppearAnimation || hasAppeared) return;
        EnsureAnimators();
        if (spriteAnimator == null) return;

        hasAppeared = true;
        appearAnimationStarted = false;
        appearAnimationFallbackEndTime = Time.time + ResolveAppearFallbackDuration();
        enemy.HideIdleOverlaysInternal();
        spriteAnimator.SetTrigger(HashAppear);
    }

    public bool IsAppearAnimationPlaying()
    {
        if (enemy == null || !enemy.useAppearAnimation || !hasAppeared)
            return false;

        EnsureAnimators();
        if (spriteAnimator == null)
            return false;

        if (IsAppearClipPlaying())
        {
            appearAnimationStarted = true;
            return true;
        }

        if (!appearAnimationStarted && Time.time < appearAnimationFallbackEndTime)
            return true;

        return false;
    }

    public void SetMoveBool(bool moving)
    {
        EnsureAnimators();
        if (spriteAnimator == null || enemy.IsDead) return;
        spriteAnimator.SetBool(HashMove, moving);
    }

    public void PlayAttackAnimation()
    {
        EnsureAnimators();
        if (spriteAnimator == null || enemy.IsDead) return;
        enemy.HideIdleOverlaysInternal();
        spriteAnimator.SetTrigger(HashAttack);
    }

    public void PlayContactAttackToPlayer(Player player)
    {
        PlayAttackAnimation();
        PlayAttackLunge(player);
    }

    public void PlayHitAnimation()
    {
        EnsureAnimators();
        if (spriteAnimator == null || enemy.IsDead) return;
        enemy.HideIdleOverlaysInternal();
        spriteAnimator.SetTrigger(HashHit);
    }

    public void PlaySkillStart()
    {
        EnsureAnimators();
        if (spriteAnimator == null || enemy.IsDead) return;
        if (!HasParameter(HashSkillStart, AnimatorControllerParameterType.Trigger)) return;
        enemy.HideIdleOverlaysInternal();
        spriteAnimator.SetTrigger(HashSkillStart);
    }

    public void PlaySkillEnd()
    {
        EnsureAnimators();
        if (spriteAnimator == null || enemy.IsDead) return;
        if (!HasParameter(HashSkillEnd, AnimatorControllerParameterType.Trigger)) return;
        enemy.HideIdleOverlaysInternal();
        spriteAnimator.SetTrigger(HashSkillEnd);
    }

    public void PlayDeadAnimation()
    {
        EnsureAnimators();
        if (spriteAnimator == null) return;
        enemy.HideIdleOverlaysInternal();
        spriteAnimator.SetBool(HashIsDead, true);
    }

    public void PlayHitShake()
    {
        StopAttackLunge(resetVisual: true);

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
    }

    public void SetHighlight(bool on)
    {
        if (enemy.highlightFx)
        {
            enemy.highlightFx.SetActive(on);
            if (on)
            {
                SyncHighlightAnimation();
            }
        }
        enemy.Sorting.UpdateNow();
    }

    public void SyncHighlightAnimation()
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

    public void RefreshIdleOverlays()
    {
        enemy.RefreshIdleOverlaysInternal();
    }

    public void HideIdleOverlays()
    {
        enemy.HideIdleOverlaysInternal();
    }

    public bool IsBodyInIdle()
    {
        EnsureAnimators();
        if (spriteAnimator == null) return false;
        if (enemy.IsDead) return false;

        return spriteAnimator
            .GetCurrentAnimatorStateInfo(0)
            .IsTag("Idle");
    }

    private Transform GetSpriteRoot()
    {
        return enemy.spriteRoot ? enemy.spriteRoot : enemy.transform;
    }

    private Transform GetAttackMotionRoot()
    {
        Transform root = GetSpriteRoot();
        if (root != null && root != enemy.transform)
        {
            return root;
        }

        EnsureAnimators();
        if (spriteAnimator != null && spriteAnimator.transform != enemy.transform)
        {
            return spriteAnimator.transform;
        }

        return null;
    }

    private void PlayAttackLunge(Player player)
    {
        if (enemy == null || player == null || enemy.IsDead)
        {
            return;
        }

        Transform root = GetAttackMotionRoot();
        if (root == null)
        {
            return;
        }

        StopAttackLunge(resetVisual: true);

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        EnsureSpriteDefaults();

        Vector3 localOffset = ResolveAttackLungeLocalOffset(root, player.transform.position);
        if (localOffset.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        attackLungeRoutine = StartCoroutine(AttackLungeRoutine(root, localOffset));
    }

    private Vector3 ResolveAttackLungeLocalOffset(Transform root, Vector3 playerWorldPosition)
    {
        Vector3 attackOrigin = root.position;
        Vector3 worldDirection = playerWorldPosition - attackOrigin;
        worldDirection.z = 0f;

        float worldDistance = worldDirection.magnitude;
        if (worldDistance <= 0.001f)
        {
            return Vector3.zero;
        }

        float lungeDistance = Mathf.Clamp(
            worldDistance * AttackLungeDistanceRatio,
            AttackLungeMinDistance,
            AttackLungeMaxDistance);

        Vector3 worldOffset = worldDirection.normalized * lungeDistance;
        Transform parent = root.parent;
        return parent != null ? parent.InverseTransformVector(worldOffset) : worldOffset;
    }

    private IEnumerator AttackLungeRoutine(Transform root, Vector3 localOffset)
    {
        Vector3 startPosition = spriteDefaultLocalPosition;
        Vector3 impactPosition = startPosition + localOffset;
        Vector3 startScale = spriteDefaultLocalScale;
        Vector3 impactScale = startScale * AttackLungeScaleMultiplier;

        yield return AnimateLungeStep(root, startPosition, impactPosition, startScale, impactScale, AttackLungeForwardDuration, EaseOutCubic);
        yield return AnimateLungeStep(root, impactPosition, startPosition, impactScale, startScale, AttackLungeReturnDuration, EaseInCubic);

        attackLungeRoutine = null;
        ResetSpriteVisual();
    }

    private IEnumerator AnimateLungeStep(
        Transform root,
        Vector3 fromPosition,
        Vector3 toPosition,
        Vector3 fromScale,
        Vector3 toScale,
        float duration,
        Func<float, float> easing)
    {
        if (duration <= 0f)
        {
            root.localPosition = toPosition;
            root.localScale = toScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = easing != null ? easing(t) : t;
            root.localPosition = Vector3.LerpUnclamped(fromPosition, toPosition, eased);
            root.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
            elapsed += Time.deltaTime;
            yield return null;
        }

        root.localPosition = toPosition;
        root.localScale = toScale;
    }

    private void StopAttackLunge(bool resetVisual)
    {
        if (attackLungeRoutine != null)
        {
            StopCoroutine(attackLungeRoutine);
            attackLungeRoutine = null;
        }

        if (resetVisual && spriteDefaultsInitialized)
        {
            ResetSpriteVisual();
        }
    }

    private static float EaseOutCubic(float t)
    {
        float inv = 1f - Mathf.Clamp01(t);
        return 1f - inv * inv * inv;
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    private IEnumerator HitShake()
    {
        EnsureSpriteDefaults();
        Transform root = GetSpriteRoot();
        Vector3 originalPos = spriteDefaultLocalPosition;
        Vector3 originalScale = spriteDefaultLocalScale;

        float elapsed = 0f;
        Vector3 targetScale = originalScale * enemy.scaleMultiplier;
        root.localScale = targetScale;
        while (elapsed < enemy.shakeDuration)
        {
            float t = elapsed / enemy.shakeDuration;
            float currentMag = Mathf.Lerp(enemy.shakeMagnitude, 0f, t);
            root.localPosition = originalPos + (Vector3)UnityEngine.Random.insideUnitCircle * currentMag;
            root.localScale = Vector3.Lerp(targetScale, originalScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        ResetSpriteVisual();
    }

    private void EnsureAnimators()
    {
        if (spriteAnimator == null)
        {
            Transform root = GetSpriteRoot();
            spriteAnimator = root.GetComponent<Animator>();
            if (spriteAnimator == null)
            {
                spriteAnimator = root.GetComponentInChildren<Animator>(true);
            }
        }

        if (highlightAnimator == null && enemy.highlightFx != null)
        {
            highlightAnimator = enemy.highlightFx.GetComponent<Animator>();
            if (highlightAnimator == null)
            {
                highlightAnimator = enemy.highlightFx.GetComponentInChildren<Animator>(true);
            }
        }
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        if (spriteAnimator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = spriteAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == hash && parameter.type == type)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAppearClipPlaying()
    {
        if (spriteAnimator == null)
            return false;

        if (ContainsAppearClip(spriteAnimator.GetCurrentAnimatorClipInfo(0)))
            return true;

        if (spriteAnimator.IsInTransition(0) && ContainsAppearClip(spriteAnimator.GetNextAnimatorClipInfo(0)))
            return true;

        return false;
    }

    private float ResolveAppearFallbackDuration()
    {
        if (spriteAnimator == null || spriteAnimator.runtimeAnimatorController == null)
            return DefaultAppearFallbackDuration;

        float longestAppearClip = 0f;
        AnimationClip[] clips = spriteAnimator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null || !IsAppearName(clip.name))
                continue;

            longestAppearClip = Mathf.Max(longestAppearClip, clip.length);
        }

        if (longestAppearClip <= 0f)
            return DefaultAppearFallbackDuration;

        return longestAppearClip + 0.35f;
    }

    private static bool ContainsAppearClip(AnimatorClipInfo[] clipInfos)
    {
        if (clipInfos == null || clipInfos.Length == 0)
            return false;

        for (int i = 0; i < clipInfos.Length; i++)
        {
            AnimationClip clip = clipInfos[i].clip;
            if (clip != null && IsAppearName(clip.name))
                return true;
        }

        return false;
    }

    private static bool IsAppearName(string clipName)
    {
        return !string.IsNullOrWhiteSpace(clipName) &&
               clipName.IndexOf("Appear", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
