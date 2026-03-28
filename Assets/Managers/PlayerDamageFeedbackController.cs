using System.Collections;
using UnityEngine;

public class PlayerDamageFeedbackController : MonoBehaviour
{
    private static readonly int HitTriggerHash = Animator.StringToHash("Hit");
    private const float DefaultAttackHitShakeScale = 2f / 3f;

    [Header("Overlay References")]
    [SerializeField] private GameObject hitOverlayRoot;
    [SerializeField] private CanvasGroup hitOverlayCanvasGroup;
    [SerializeField] private Animator hitOverlayAnimator;

    [Header("Shake")]
    [SerializeField] private ScreenShakeController screenShakeController;
    [SerializeField] private float shakeDuration = 0.18f;
    [SerializeField] private float shakeStrength = 0.18f;

    [Header("CanvasGroup Fade")]
    [SerializeField] private float overlayPeakAlpha = 0.45f;
    [SerializeField] private float fadeInDuration = 0.05f;
    [SerializeField] private float fadeOutDuration = 0.16f;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (hitOverlayCanvasGroup != null)
        {
            hitOverlayCanvasGroup.alpha = 0f;
        }

        if (hitOverlayRoot != null)
        {
            hitOverlayRoot.SetActive(false);
        }
    }

    public void PlayHitFeedback()
    {
        PlayOverlay();
        PlayShake();
    }

    public void PlayAttackHitShake()
    {
        PlayShake(DefaultAttackHitShakeScale);
    }

    private void PlayOverlay()
    {
        if (hitOverlayAnimator != null)
        {
            if (hitOverlayRoot != null && !hitOverlayRoot.activeSelf)
            {
                hitOverlayRoot.SetActive(true);
            }

            hitOverlayAnimator.ResetTrigger(HitTriggerHash);
            hitOverlayAnimator.SetTrigger(HitTriggerHash);
            return;
        }

        if (hitOverlayCanvasGroup == null)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(PlayCanvasGroupFade());
    }

    private void PlayShake()
    {
        PlayShake(1f);
    }

    private void PlayShake(float strengthScale)
    {
        if (screenShakeController == null)
        {
            return;
        }

        screenShakeController.PlayShake(shakeDuration, shakeStrength * Mathf.Max(0f, strengthScale));
    }

    private IEnumerator PlayCanvasGroupFade()
    {
        if (hitOverlayRoot != null && !hitOverlayRoot.activeSelf)
        {
            hitOverlayRoot.SetActive(true);
        }

        hitOverlayCanvasGroup.alpha = 0f;

        yield return FadeCanvasGroup(0f, overlayPeakAlpha, fadeInDuration);
        yield return FadeCanvasGroup(hitOverlayCanvasGroup.alpha, 0f, fadeOutDuration);

        hitOverlayCanvasGroup.alpha = 0f;

        if (hitOverlayRoot != null)
        {
            hitOverlayRoot.SetActive(false);
        }

        fadeRoutine = null;
    }

    private IEnumerator FadeCanvasGroup(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            hitOverlayCanvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            hitOverlayCanvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        hitOverlayCanvasGroup.alpha = to;
    }

    private void OnDisable()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (hitOverlayCanvasGroup != null)
        {
            hitOverlayCanvasGroup.alpha = 0f;
        }

        if (hitOverlayRoot != null)
        {
            hitOverlayRoot.SetActive(false);
        }
    }
}
