using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerEffectController : MonoBehaviour
{
    private static readonly int HashHasShield = Animator.StringToHash("HasShield");
    private static readonly int HashPlayShieldHitFX = Animator.StringToHash("PlayShieldHitFX");
    private static readonly int HashPlayShieldBreakFX = Animator.StringToHash("PlayShieldBreakFX");
    private static readonly int HashPlayMoveStarFX = Animator.StringToHash("PlayMoveStarFX");
    private static readonly int HashPlayTeleportLeaveFX = Animator.StringToHash("PlayTeleportLeaveFX");
    private static readonly int HashPlayTeleportAppearFX = Animator.StringToHash("PlayTeleportAppearFX");

    [Header("FX Animators")]
    [SerializeField] private Animator shieldFxAnimator;
    [SerializeField] private Animator actionFxAnimator;

    [Header("Debuff FX")]
    [SerializeField] private GameObject bleedFxRoot;
    [SerializeField] private GameObject weakFxRoot;
    [SerializeField] private GameObject imprisonEnterFxRoot;
    [SerializeField] private GameObject imprisonIdleFxRoot;
    [SerializeField] private GameObject huGuPoSkillFxRoot;

    private bool hasShieldParam;
    private bool hasShieldHitFxParam;
    private bool hasShieldBreakFxParam;
    private bool hasMoveStarFxParam;
    private bool hasTeleportLeaveFxParam;
    private bool hasTeleportAppearFxParam;
    private bool wasImprisonActive;

    private void Awake()
    {
        EnsureResolved();
    }

    public void SetShieldActive(bool active)
    {
        EnsureResolved();

        if (shieldFxAnimator == null || !hasShieldParam)
        {
            return;
        }

        shieldFxAnimator.SetBool(HashHasShield, active);
    }

    public void PlayShieldHitFX()
    {
        EnsureResolved();

        if (shieldFxAnimator == null || !hasShieldHitFxParam)
        {
            return;
        }

        shieldFxAnimator.SetTrigger(HashPlayShieldHitFX);
    }

    public void PlayShieldBreakFX()
    {
        EnsureResolved();

        if (shieldFxAnimator == null || !hasShieldBreakFxParam)
        {
            return;
        }

        shieldFxAnimator.SetTrigger(HashPlayShieldBreakFX);
    }

    public void PlayMoveStarFX()
    {
        EnsureResolved();
        TrySetActionTrigger(HashPlayMoveStarFX, hasMoveStarFxParam);
    }

    public void PlayTeleportLeaveFX()
    {
        EnsureResolved();
        TrySetActionTrigger(HashPlayTeleportLeaveFX, hasTeleportLeaveFxParam);
    }

    public void PlayTeleportAppearFX()
    {
        EnsureResolved();
        TrySetActionTrigger(HashPlayTeleportAppearFX, hasTeleportAppearFxParam);
    }

    public void SetDebuffFxState(bool hasBleed, bool hasWeak, bool hasImprison)
    {
        EnsureResolved();

        SetFxActive(bleedFxRoot, hasBleed);
        SetFxActive(weakFxRoot, hasWeak);
        SetFxActive(imprisonIdleFxRoot, hasImprison);

        if (hasImprison && !wasImprisonActive)
        {
            ReplayFx(imprisonEnterFxRoot);
        }
        else if (!hasImprison)
        {
            SetFxActive(imprisonEnterFxRoot, false);
        }

        wasImprisonActive = hasImprison;
    }

    public void PlayHuGuPoSkillFX()
    {
        EnsureResolved();
        ReplayFx(huGuPoSkillFxRoot);
    }

    private void EnsureResolved()
    {
        ResolveAnimators();
        CacheParameterFlags();
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

        bleedFxRoot = ResolveFxRoot(bleedFxRoot, "BleedFXRoot", "BleedEffectRoot", "DebuffBleedFXRoot");
        weakFxRoot = ResolveFxRoot(weakFxRoot, "WeakFXRoot", "WeakEffectRoot", "DebuffWeakFXRoot");
        huGuPoSkillFxRoot = ResolveFxRoot(huGuPoSkillFxRoot, "HuGuPoSkillFXRoot");

        Transform imprisonRoot = ResolveFxTransform(null, "ImprisonFXRoot", "DebuffImprisonFXRoot");
        imprisonEnterFxRoot = ResolveFxRoot(
            imprisonEnterFxRoot,
            "ImprisonEnterFXRoot",
            "ImprisonStartFXRoot",
            "DebuffImprisonEnterFXRoot",
            "Enter",
            "confine_start_00");
        imprisonIdleFxRoot = ResolveFxRoot(
            imprisonIdleFxRoot,
            "ImprisonIdleFXRoot",
            "DebuffImprisonIdleFXRoot",
            "Idle",
            "standby_00");

        if (imprisonRoot != null)
        {
            if (imprisonEnterFxRoot == null)
            {
                Transform enter = FindChildByName(imprisonRoot, "Enter") ?? FindChildByName(imprisonRoot, "confine_start_00");
                if (enter != null)
                {
                    imprisonEnterFxRoot = enter.gameObject;
                }
            }

            if (imprisonIdleFxRoot == null)
            {
                Transform idle = FindChildByName(imprisonRoot, "Idle") ?? FindChildByName(imprisonRoot, "standby_00");
                if (idle != null)
                {
                    imprisonIdleFxRoot = idle.gameObject;
                }
            }
        }
    }

    private void CacheParameterFlags()
    {
        hasShieldParam = HasParameter(shieldFxAnimator, HashHasShield, AnimatorControllerParameterType.Bool);
        hasShieldHitFxParam = HasParameter(shieldFxAnimator, HashPlayShieldHitFX, AnimatorControllerParameterType.Trigger);
        hasShieldBreakFxParam = HasParameter(shieldFxAnimator, HashPlayShieldBreakFX, AnimatorControllerParameterType.Trigger);
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

    private static void SetFxActive(GameObject fxRoot, bool active)
    {
        if (fxRoot == null)
        {
            return;
        }

        if (fxRoot.activeSelf != active)
        {
            fxRoot.SetActive(active);
        }
    }

    private static void ReplayFx(GameObject fxRoot)
    {
        if (fxRoot == null)
        {
            return;
        }

        fxRoot.SetActive(false);
        fxRoot.SetActive(true);

        Animator animator = fxRoot.GetComponent<Animator>();
        if (animator == null)
        {
            animator = fxRoot.GetComponentInChildren<Animator>(true);
        }

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }

    private GameObject ResolveFxRoot(GameObject current, params string[] names)
    {
        if (current != null)
        {
            return current;
        }

        Transform match = ResolveFxTransform(null, names);
        return match != null ? match.gameObject : null;
    }

    private Transform ResolveFxTransform(Transform current, params string[] names)
    {
        if (current != null)
        {
            return current;
        }

        for (int i = 0; i < names.Length; i++)
        {
            Transform child = FindChildByName(transform, names[i]);
            if (child != null)
            {
                return child;
            }
        }

        Scene activeScene = gameObject.scene;
        if (activeScene.IsValid())
        {
            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform root = roots[i].transform;
                for (int j = 0; j < names.Length; j++)
                {
                    Transform child = FindChildByName(root, names[j]);
                    if (child != null)
                    {
                        return child;
                    }
                }
            }
        }

        return null;
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
