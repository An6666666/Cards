using UnityEngine;

public class PlayerAnimatorController : MonoBehaviour
{
    private static readonly int HashMove = Animator.StringToHash("Move");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashDefend = Animator.StringToHash("Defend");
    private static readonly int HashUtility = Animator.StringToHash("Utility");
    private static readonly int HashMoveCard = Animator.StringToHash("MoveCard");
    private static readonly int HashMoveStar = Animator.StringToHash("MoveStar");
    private static readonly int HashTeleportDisappear = Animator.StringToHash("TeleportDisappear");
    private static readonly int HashTeleportAppear = Animator.StringToHash("TeleportAppear");
    private static readonly int HashWounded = Animator.StringToHash("Wounded");
    private static readonly int HashDead = Animator.StringToHash("Dead");

    [Header("Animator (on body/visual)")]
    public Animator animator;

    private bool hasMoveParam;
    private bool hasAttackParam;
    private bool hasDefendParam;
    private bool hasUtilityParam;
    private bool hasMoveCardParam;
    private bool hasMoveStarParam;
    private bool hasTeleportDisappearParam;
    private bool hasTeleportAppearParam;
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
        if (animator == null || !hasMoveParam)
        {
            return;
        }

        animator.SetBool(HashMove, moving);
    }

    public void PlayAttack()
    {
        TrySetTrigger(HashAttack, hasAttackParam);
    }

    public void PlayDefend()
    {
        TrySetTrigger(HashDefend, hasDefendParam);
    }

    public void PlayUtility()
    {
        TrySetTrigger(HashUtility, hasUtilityParam);
    }

    public void PlayMoveCard()
    {
        TrySetTrigger(HashMoveCard, hasMoveCardParam);
    }

    public void PlayMoveStar()
    {
        TrySetTrigger(HashMoveStar, hasMoveStarParam);
    }

    public void PlayTeleportDisappear()
    {
        TrySetTrigger(HashTeleportDisappear, hasTeleportDisappearParam);
    }

    public void PlayTeleportAppear()
    {
        TrySetTrigger(HashTeleportAppear, hasTeleportAppearParam);
    }

    public void PlayWounded()
    {
        TrySetTrigger(HashWounded, hasWoundedParam);
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
        hasMoveParam = HasParameter(HashMove, AnimatorControllerParameterType.Bool);
        hasAttackParam = HasParameter(HashAttack, AnimatorControllerParameterType.Trigger);
        hasDefendParam = HasParameter(HashDefend, AnimatorControllerParameterType.Trigger);
        hasUtilityParam = HasParameter(HashUtility, AnimatorControllerParameterType.Trigger);
        hasMoveCardParam = HasParameter(HashMoveCard, AnimatorControllerParameterType.Trigger);
        hasMoveStarParam = HasParameter(HashMoveStar, AnimatorControllerParameterType.Trigger);
        hasTeleportDisappearParam = HasParameter(HashTeleportDisappear, AnimatorControllerParameterType.Trigger);
        hasTeleportAppearParam = HasParameter(HashTeleportAppear, AnimatorControllerParameterType.Trigger);
        hasWoundedParam = HasParameter(HashWounded, AnimatorControllerParameterType.Trigger);
        hasDeadParam = HasParameter(HashDead, AnimatorControllerParameterType.Bool);
    }

    private void TrySetTrigger(int hash, bool hasParameter)
    {
        if (animator == null || !hasParameter)
        {
            return;
        }

        animator.SetTrigger(hash);
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
