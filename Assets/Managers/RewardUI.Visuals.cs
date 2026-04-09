using UnityEngine;
using UnityEngine.UI;

public partial class RewardUI
{
    private void PrepareRelicStage()
    {
        packOpenState = PackOpenState.Idle;

        if (packButton != null)
        {
            packButton.onClick.RemoveAllListeners();
            packButton.gameObject.SetActive(false);
        }

        if (cardParent != null)
        {
            cardParent.gameObject.SetActive(true);
        }

        ResetRectVisual(cardbagBag, false, bagDefaultScale, 0f);
        ResetRectVisual(cardbagLight, false, lightDefaultScale, 0f);
        ResetRectVisual(effectRoot, false, effectDefaultScale, 0f);
        ResetRectVisual(bottomRoot, true, bottomDefaultScale, 1f);
        ResetRectVisual(cardGlowRoot, false, glowDefaultScale, 0f);

        RestartVisualAnimator(bottomRoot);
        SetGoldTextAlignment(TextAnchor.MiddleCenter);
        SetGoldText(relicRewardTitle);
    }

    private void PrepareIdleStage()
    {
        packOpenState = PackOpenState.Idle;

        if (packButton != null)
        {
            packButton.gameObject.SetActive(true);
            packButton.interactable = true;
        }

        if (cardParent != null)
        {
            cardParent.gameObject.SetActive(false);
        }

        ResetRectVisual(cardbagBag, true, bagDefaultScale, 1f);
        ResetRectVisual(cardbagLight, true, lightDefaultScale, 1f);
        ResetRectVisual(effectRoot, false, effectDefaultScale, 1f);
        ResetRectVisual(bottomRoot, false, bottomDefaultScale, 1f);
        ResetRectVisual(cardGlowRoot, false, glowDefaultScale, 1f);

        RestartVisualAnimator(cardbagBag);
        RestartVisualAnimator(cardbagLight);
    }

    private void ClearRewardEntries()
    {
        rewardButtons.Clear();
        rewardCardUIs.Clear();
        relicChoiceViews.Clear();
        selectedRelicChoiceView = null;

        if (cardParent == null)
        {
            return;
        }

        while (cardParent.childCount > 0)
        {
            Transform child = cardParent.GetChild(0);
            child.gameObject.SetActive(false);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        RefreshRewardLayout();
    }

    private void ResolvePackVisualReferences()
    {
        if (cardbagBag == null)
        {
            cardbagBag = FindNamedRect("packImage");
            if (cardbagBag == null)
            {
                cardbagBag = FindNamedRect("cardbag_bag", packButton != null ? packButton.transform : null);
            }
        }

        if (cardbagLight == null)
        {
            cardbagLight = FindNamedRect("cardbag_light");
        }

        if (effectRoot == null)
        {
            effectRoot = FindNamedRect("effect");
        }

        if (bottomRoot == null)
        {
            bottomRoot = FindNamedRect("bottom", cardParent);
        }

        if (cardGlowRoot == null)
        {
            cardGlowRoot = FindNamedRect("card");
        }
    }

    private void CacheDefaultVisualState()
    {
        bagDefaultScale = cardbagBag != null ? cardbagBag.localScale : Vector3.one;
        lightDefaultScale = cardbagLight != null ? cardbagLight.localScale : Vector3.one;
        effectDefaultScale = effectRoot != null ? effectRoot.localScale : Vector3.one;
        bottomDefaultScale = bottomRoot != null ? bottomRoot.localScale : Vector3.one;
        glowDefaultScale = cardGlowRoot != null ? cardGlowRoot.localScale : Vector3.one;
    }

    private void CacheGoldTextStyle()
    {
        if (hasCachedGoldTextAlignment || goldText == null)
        {
            return;
        }

        defaultGoldTextAlignment = goldText.alignment;
        hasCachedGoldTextAlignment = true;
    }

    private void RestoreGoldTextStyle()
    {
        CacheGoldTextStyle();
        SetGoldTextAlignment(defaultGoldTextAlignment);
    }

    private void SetGoldText(string message)
    {
        if (goldText != null)
        {
            goldText.text = message;
        }
    }

    private void SetGoldTextAlignment(TextAnchor alignment)
    {
        if (goldText != null)
        {
            goldText.alignment = alignment;
        }
    }

    private void RefreshRewardLayout()
    {
        if (!(cardParent is RectTransform cardParentRect))
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(cardParentRect);
        Canvas.ForceUpdateCanvases();
    }

    private RectTransform FindNamedRect(string childName, Transform fallback = null)
    {
        Transform target = transform.Find(childName);
        if (target == null)
        {
            target = FindDeepChild(transform, childName);
        }

        if (target == null)
        {
            target = fallback;
        }

        return target as RectTransform;
    }

    private Transform FindDeepChild(Transform root, string childName)
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

            Transform result = FindDeepChild(child, childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void ResetRectVisual(RectTransform rectTransform, bool active, Vector3 scale, float alpha)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.gameObject.SetActive(active);
        rectTransform.localScale = scale;
        rectTransform.localRotation = Quaternion.identity;

        CanvasGroup canvasGroup = GetOrAddCanvasGroup(rectTransform.gameObject);
        canvasGroup.alpha = alpha;

        bool isPackButtonRoot = packButton != null && rectTransform == packButton.transform;
        canvasGroup.blocksRaycasts = isPackButtonRoot;
        canvasGroup.interactable = isPackButtonRoot;
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = target.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private void StopVisualAnimator(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        Animator animator = rectTransform.GetComponent<Animator>();
        if (animator == null)
        {
            animator = rectTransform.GetComponentInChildren<Animator>(true);
        }

        if (animator == null)
        {
            return;
        }

        animator.speed = 1f;
        animator.enabled = false;
    }

    private void RestartVisualAnimator(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        Animator animator = rectTransform.GetComponent<Animator>();
        if (animator == null)
        {
            animator = rectTransform.GetComponentInChildren<Animator>(true);
        }

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        animator.enabled = true;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.speed = 1f;
        animator.Rebind();
        animator.Update(0f);
        animator.Play(0, 0, 0f);
        animator.Update(0.0001f);
    }
}
