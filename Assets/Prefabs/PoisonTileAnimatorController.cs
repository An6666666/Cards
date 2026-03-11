using System.Collections;
using UnityEngine;

public class PoisonTileAnimatorController : MonoBehaviour
{
    private static readonly int HashIsActive = Animator.StringToHash("IsActive");
    private static readonly int HashPlayEnd = Animator.StringToHash("PlayEnd");

    [SerializeField] private Animator animator;

    private bool hasIsActiveParam;
    private bool hasPlayEndParam;
    private Coroutine hideRoutine;

    private void Awake()
    {
        ResolveAnimator();
        CacheAnimatorParameterFlags();
    }

    public void Show()
    {
        ResolveAnimator();
        CacheAnimatorParameterFlags();

        gameObject.SetActive(true);

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (animator == null || !hasIsActiveParam)
        {
            return;
        }

        if (hasPlayEndParam)
        {
            animator.ResetTrigger(HashPlayEnd);
        }

        animator.SetBool(HashIsActive, true);
    }

    public void Hide()
    {
        ResolveAnimator();
        CacheAnimatorParameterFlags();

        if (!gameObject.activeSelf)
        {
            return;
        }

        if (animator == null || !hasIsActiveParam || !hasPlayEndParam)
        {
            gameObject.SetActive(false);
            return;
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        animator.SetBool(HashIsActive, false);
        animator.ResetTrigger(HashPlayEnd);
        animator.SetTrigger(HashPlayEnd);
        hideRoutine = StartCoroutine(WaitForOffState());
    }

    private IEnumerator WaitForOffState()
    {
        const float timeoutSeconds = 5f;
        float elapsed = 0f;

        while (elapsed < timeoutSeconds)
        {
            if (animator == null)
            {
                hideRoutine = null;
                yield break;
            }

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!animator.IsInTransition(0) && state.IsName("Off"))
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        hideRoutine = null;
        gameObject.SetActive(false);
    }

    private void ResolveAnimator()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void CacheAnimatorParameterFlags()
    {
        hasIsActiveParam = HasParameter(HashIsActive, AnimatorControllerParameterType.Bool);
        hasPlayEndParam = HasParameter(HashPlayEnd, AnimatorControllerParameterType.Trigger);
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType expectedType)
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == hash && parameter.type == expectedType)
            {
                return true;
            }
        }

        return false;
    }
}
