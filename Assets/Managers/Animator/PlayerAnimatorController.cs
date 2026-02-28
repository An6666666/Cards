using UnityEngine;

public class PlayerAnimatorController : MonoBehaviour
{
    private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashWounded = Animator.StringToHash("Wounded");
    private static readonly int HashDead = Animator.StringToHash("Dead");

    [Header("Animator (on body/visual)")]
    public Animator animator;

    private bool hasIsMovingParam;
    private bool hasAttackParam;
    private bool hasWoundedParam;
    private bool hasDeadParam;

    private void Reset()
    {
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        CacheAnimatorParameterFlags();
    }

    public void SetMoving(bool moving)
    {
        if (animator == null || !hasIsMovingParam)
        {
            return;
        }

        animator.SetBool(HashIsMoving, moving);
    }

    public void PlayAttack()
    {
        if (animator == null || !hasAttackParam)
        {
            return;
        }

        animator.SetTrigger(HashAttack);
    }

    public void PlayWounded()
    {
        if (animator == null || !hasWoundedParam)
        {
            return;
        }

        animator.SetTrigger(HashWounded);
    }

    public void SetDead(bool dead)
    {
        if (animator == null || !hasDeadParam)
        {
            return;
        }

        animator.SetBool(HashDead, dead);
    }

    private void CacheAnimatorParameterFlags()
    {
        hasIsMovingParam = HasParameter(HashIsMoving, AnimatorControllerParameterType.Bool);
        hasAttackParam = HasParameter(HashAttack, AnimatorControllerParameterType.Trigger);
        hasWoundedParam = HasParameter(HashWounded, AnimatorControllerParameterType.Trigger);
        hasDeadParam = HasParameter(HashDead, AnimatorControllerParameterType.Bool);
    }

    private bool HasParameter(int nameHash, AnimatorControllerParameterType expectedType)
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == nameHash && parameter.type == expectedType)
            {
                return true;
            }
        }

        return false;
    }
}
