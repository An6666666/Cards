using System.Collections;
using UnityEngine;

public class EnemyVisual : MonoBehaviour
{
    private Enemy enemy;
    private Coroutine shakeRoutine;
    private Vector3 spriteDefaultLocalPosition;
    private Vector3 spriteDefaultLocalScale;
    private bool spriteDefaultsInitialized = false;
    private Animator spriteAnimator;
    private Animator highlightAnimator;

    private static readonly int HashAppear = Animator.StringToHash("Appear");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashHit = Animator.StringToHash("Hit");
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
        enemy?.HideIdleOverlaysInternal();
        enemy?.RefreshIdleOverlaysInternal();
        PlayAppearAnimation();
    }

    public void HandleOnEnable()
    {
        SyncHighlightAnimation();
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
        enemy.HideIdleOverlaysInternal();
        spriteAnimator.SetTrigger(HashAppear);
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

    public void PlayHitAnimation()
    {
        EnsureAnimators();
        if (spriteAnimator == null || enemy.IsDead) return;
        enemy.HideIdleOverlaysInternal();
        spriteAnimator.SetTrigger(HashHit);
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
}
