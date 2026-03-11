using System.Collections;
using UnityEngine;

public class ScreenShakeController : MonoBehaviour
{
    [SerializeField] private bool useUnscaledTime = true;

    private Coroutine shakeRoutine;
    private Vector3 initialLocalPosition;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        initialLocalPosition = transform.localPosition;
    }

    public void PlayShake(float duration, float strength)
    {
        if (duration <= 0f || strength <= 0f)
        {
            return;
        }

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
        }

        transform.localPosition = initialLocalPosition;
        shakeRoutine = StartCoroutine(ShakeRoutine(duration, strength));
    }

    private IEnumerator ShakeRoutine(float duration, float strength)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += deltaTime;

            float damper = 1f - Mathf.Clamp01(elapsed / duration);
            Vector2 offset2D = Random.insideUnitCircle * strength * damper;
            transform.localPosition = initialLocalPosition + new Vector3(offset2D.x, offset2D.y, 0f);

            yield return null;
        }

        transform.localPosition = initialLocalPosition;
        shakeRoutine = null;
    }

    private void OnDisable()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        transform.localPosition = initialLocalPosition;
    }
}
