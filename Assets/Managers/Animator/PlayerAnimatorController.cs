using UnityEngine;

public class PlayerAnimatorController : MonoBehaviour
{
    [Header("Animator (掛在 body/visual 上)")]
    public Animator animator;

    private void Reset()
    {
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
    }

    public void SetMoving(bool moving)
    {
        if (animator == null) return;
        animator.SetBool("IsMoving", moving);
    }

    public void PlayAttack()
    {
        if (animator == null) return;
        animator.SetTrigger("Attack");
    }

    public void PlayWounded()
    {
        if (animator == null) return;
        animator.SetTrigger("Wounded");
    }

    public void SetDead(bool dead)
    {
        if (animator == null) return;
        animator.SetBool("Dead", dead);
    }
}
