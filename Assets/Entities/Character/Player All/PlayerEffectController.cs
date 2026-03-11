using UnityEngine;

public class PlayerEffectController : MonoBehaviour
{
    private static readonly int HashHasShield = Animator.StringToHash("HasShield");
    private static readonly int HashPlayMoveStarFX = Animator.StringToHash("PlayMoveStarFX");
    private static readonly int HashPlayTeleportLeaveFX = Animator.StringToHash("PlayTeleportLeaveFX");
    private static readonly int HashPlayTeleportAppearFX = Animator.StringToHash("PlayTeleportAppearFX");

    [Header("FX Animators")]
    [SerializeField] private Animator shieldFxAnimator;
    [SerializeField] private Animator actionFxAnimator;

    private bool hasShieldParam;
    private bool hasMoveStarFxParam;
    private bool hasTeleportLeaveFxParam;
    private bool hasTeleportAppearFxParam;

    private void Awake()
    {
        ResolveAnimators();
        CacheParameterFlags();
    }

    public void SetShieldActive(bool active)
    {
        if (shieldFxAnimator == null || !hasShieldParam)
        {
            return;
        }

        shieldFxAnimator.SetBool(HashHasShield, active);
    }

    public void PlayMoveStarFX()
    {
        TrySetActionTrigger(HashPlayMoveStarFX, hasMoveStarFxParam);
    }

    public void PlayTeleportLeaveFX()
    {
        TrySetActionTrigger(HashPlayTeleportLeaveFX, hasTeleportLeaveFxParam);
    }

    public void PlayTeleportAppearFX()
    {
        TrySetActionTrigger(HashPlayTeleportAppearFX, hasTeleportAppearFxParam);
    }

    private void ResolveAnimators()
    {
        if (shieldFxAnimator == null)
        {
            Transform shieldRoot = FindChildByName(transform, "ShieldFXRoot");
            if (shieldRoot != null)
            {
                shieldFxAnimator = shieldRoot.GetComponent<Animator>();
            }
        }

        if (actionFxAnimator == null)
        {
            Transform actionRoot = FindChildByName(transform, "ActionFXRoot");
            if (actionRoot != null)
            {
                actionFxAnimator = actionRoot.GetComponent<Animator>();
            }
        }
    }

    private void CacheParameterFlags()
    {
        hasShieldParam = HasParameter(shieldFxAnimator, HashHasShield, AnimatorControllerParameterType.Bool);
        hasMoveStarFxParam = HasParameter(actionFxAnimator, HashPlayMoveStarFX, AnimatorControllerParameterType.Trigger);
        hasTeleportLeaveFxParam = HasParameter(actionFxAnimator, HashPlayTeleportLeaveFX, AnimatorControllerParameterType.Trigger);
        hasTeleportAppearFxParam = HasParameter(actionFxAnimator, HashPlayTeleportAppearFX, AnimatorControllerParameterType.Trigger);
    }

    private void TrySetActionTrigger(int hash, bool hasParameter)
    {
        if (actionFxAnimator == null || !hasParameter)
        {
            return;
        }

        actionFxAnimator.SetTrigger(hash);
    }

    private static bool HasParameter(Animator animator, int hash, AnimatorControllerParameterType expectedType)
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
