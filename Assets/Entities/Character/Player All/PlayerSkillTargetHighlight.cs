using UnityEngine;

public class PlayerSkillTargetHighlight : MonoBehaviour
{
    [SerializeField] private Animator sourceAnimator;
    [SerializeField] private SpriteRenderer sourceRenderer;
    [SerializeField] private GameObject highlightObject;
    [SerializeField] private RuntimeAnimatorController highlightController;
    [SerializeField] private Color highlightColor = Color.white;

    private SpriteRenderer highlightRenderer;
    private Animator highlightAnimator;
    private bool highlighted;

    private void Awake()
    {
        ResolveSourceReferences();
        ResolveHighlightObject();
        if (highlightObject != null)
        {
            highlightObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        SetHighlighted(false);
    }

    public void SetHighlighted(bool value)
    {
        if (highlighted == value)
        {
            return;
        }

        highlighted = value;

        if (value)
        {
            ResolveHighlightObject();
            if (highlightObject == null)
            {
                return;
            }

            SyncRendererState();
            highlightObject.SetActive(true);
            RestartHighlightAnimator();
            return;
        }

        if (highlightObject != null)
        {
            highlightObject.SetActive(false);
        }
    }

    private void ResolveHighlightObject()
    {
        ResolveSourceReferences();

        if (highlightObject == null)
        {
            highlightObject = FindChildByName(transform, "SkillTargetHighlight");
        }

        if (highlightObject == null && sourceAnimator != null)
        {
            highlightObject = FindChildByName(sourceAnimator.transform, "SkillTargetHighlight");
        }

        if (highlightObject == null)
        {
            return;
        }

        if (highlightRenderer == null)
        {
            highlightRenderer = highlightObject.GetComponent<SpriteRenderer>();
        }

        if (highlightAnimator == null)
        {
            highlightAnimator = highlightObject.GetComponent<Animator>();
        }

        if (highlightAnimator != null && highlightController != null)
        {
            highlightAnimator.runtimeAnimatorController = highlightController;
        }

        SyncRendererState();
    }

    private void ResolveSourceReferences()
    {
        if (sourceAnimator == null)
        {
            PlayerAnimatorController animatorController = GetComponentInChildren<PlayerAnimatorController>(true);
            if (animatorController != null)
            {
                sourceAnimator = animatorController.animator;
            }
        }

        if (sourceAnimator == null)
        {
            sourceAnimator = GetComponentInChildren<Animator>(true);
        }

        if (sourceRenderer == null && sourceAnimator != null)
        {
            sourceRenderer = sourceAnimator.GetComponent<SpriteRenderer>();
        }

        if (sourceRenderer == null)
        {
            sourceRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private void SyncRendererState()
    {
        if (highlightRenderer == null)
        {
            return;
        }

        highlightRenderer.color = highlightColor;

        if (sourceRenderer == null)
        {
            return;
        }

        highlightRenderer.flipX = sourceRenderer.flipX;
        highlightRenderer.flipY = sourceRenderer.flipY;
    }

    private void RestartHighlightAnimator()
    {
        if (highlightAnimator == null)
        {
            return;
        }

        if (highlightController != null)
        {
            highlightAnimator.runtimeAnimatorController = highlightController;
        }

        if (highlightAnimator.runtimeAnimatorController == null)
        {
            return;
        }

        highlightAnimator.Rebind();
        highlightAnimator.Update(0f);
        if (highlightAnimator.layerCount > 0)
        {
            highlightAnimator.Play(0, 0, 0f);
        }
    }

    private static GameObject FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child != root && child.name == childName)
            {
                return child.gameObject;
            }
        }

        return null;
    }
}
