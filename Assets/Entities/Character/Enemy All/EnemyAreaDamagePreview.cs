using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class EnemyAreaDamagePreview : MonoBehaviour
{
    [Header("Preview Icon")]
    [Tooltip("Optional override sprite. If empty, the preview reuses the enemy intent sprite.")]
    [SerializeField] private Sprite previewSprite;
    [Tooltip("If assigned, this placed renderer is used first so you can adjust the icon directly in Bottom.")]
    [SerializeField] private SpriteRenderer placedPreviewRenderer;
    [SerializeField] private Vector3 fallbackLocalOffset = new Vector3(-0.85f, 4.25f, 0f);
    [FormerlySerializedAs("offsetFromIntentIcon")]
    [Tooltip("Offset relative to the enemy transform center.")]
    [SerializeField] private Vector3 offsetFromEnemyCenter = new Vector3(-0.8f, 4.05f, 0f);
    [SerializeField] private float iconScale = 0.65f;
    [SerializeField] private Color iconColor = new Color(1f, 0.45f, 0.2f, 0.95f);
    [SerializeField] private int sortingOrderOffset = 3;
    [Tooltip("When using a placed object inside Bottom, keep its local position/scale instead of overriding it from code.")]
    [SerializeField] private bool preservePlacedObjectTransform = true;
    [Tooltip("When using a placed object inside Bottom, keep its sorting setup from the prefab.")]
    [SerializeField] private bool preservePlacedObjectSorting = true;

    [Header("Debug")]
    [Tooltip("Only for size comparison in Inspector. Forces the preview icon to stay visible without changing battle logic.")]
    [SerializeField] private bool forceShowForSizeCheck;

    private Enemy enemy;
    private SpriteRenderer previewRenderer;
    private Transform previewTransform;
    private bool isVisible;
    private bool isUsingPlacedRenderer;

    public void Init(Enemy owner)
    {
        enemy = owner;

        if (previewRenderer != null)
        {
            SyncVisualState();
        }
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        EnsurePreviewRenderer();
        SyncVisualState();
    }

    private void LateUpdate()
    {
        if (previewRenderer == null)
        {
            return;
        }

        if (!ShouldShowPreview())
        {
            if (previewRenderer.enabled)
            {
                previewRenderer.enabled = false;
            }

            return;
        }

        SyncVisualState();
    }

    private void OnValidate()
    {
        if (previewRenderer != null)
        {
            SyncVisualState();
        }
    }

    private void EnsurePreviewRenderer()
    {
        if (placedPreviewRenderer == null)
        {
            placedPreviewRenderer = ResolvePlacedPreviewRenderer();
        }

        if (placedPreviewRenderer != null)
        {
            previewRenderer = placedPreviewRenderer;
            previewTransform = placedPreviewRenderer.transform;
            previewRenderer.enabled = false;
            isUsingPlacedRenderer = true;
            return;
        }

        if (previewRenderer != null)
        {
            return;
        }

        Transform existing = transform.Find("AreaDamagePreviewIcon");
        if (existing != null)
        {
            previewTransform = existing;
            previewRenderer = existing.GetComponent<SpriteRenderer>();
        }

        if (previewRenderer != null)
        {
            previewRenderer.enabled = false;
            return;
        }

        GameObject iconObject = new GameObject("AreaDamagePreviewIcon");
        previewTransform = iconObject.transform;
        previewTransform.SetParent(transform, false);

        previewRenderer = iconObject.AddComponent<SpriteRenderer>();
        previewRenderer.enabled = false;
        isUsingPlacedRenderer = false;
    }

    private void SyncVisualState()
    {
        if (previewRenderer == null)
        {
            return;
        }

        Sprite iconSprite = ResolvePreviewSprite();
        bool shouldShow = ShouldShowPreview() && enemy != null && iconSprite != null;
        if (!shouldShow)
        {
            previewRenderer.enabled = false;
            return;
        }

        previewRenderer.sprite = iconSprite;
        previewRenderer.color = iconColor;
        previewRenderer.enabled = true;

        if (previewTransform != null && ShouldApplyRuntimeTransform())
        {
            previewTransform.localPosition = ResolveLocalPosition();
            previewTransform.localScale = Vector3.one * iconScale;
        }

        if (ShouldApplyRuntimeSorting())
        {
            ApplySorting();
        }
    }

    [ContextMenu("Toggle Force Show For Size Check")]
    private void ToggleForceShowForSizeCheck()
    {
        forceShowForSizeCheck = !forceShowForSizeCheck;
        EnsurePreviewRenderer();
        SyncVisualState();
    }

    [ContextMenu("Enable Force Show For Size Check")]
    private void EnableForceShowForSizeCheck()
    {
        forceShowForSizeCheck = true;
        EnsurePreviewRenderer();
        SyncVisualState();
    }

    [ContextMenu("Disable Force Show For Size Check")]
    private void DisableForceShowForSizeCheck()
    {
        forceShowForSizeCheck = false;
        EnsurePreviewRenderer();
        SyncVisualState();
    }

    private Vector3 ResolveLocalPosition()
    {
        if (enemy != null)
        {
            return offsetFromEnemyCenter;
        }

        return fallbackLocalOffset;
    }

    private void ApplySorting()
    {
        if (previewRenderer == null)
        {
            return;
        }

        if (enemy != null && enemy.intentIconRenderer != null)
        {
            previewRenderer.sortingLayerID = enemy.intentIconRenderer.sortingLayerID;
            previewRenderer.sortingOrder = enemy.intentIconRenderer.sortingOrder + sortingOrderOffset;
            return;
        }

        if (enemy == null)
        {
            return;
        }

        SpriteRenderer fallbackRenderer = ResolveFallbackRenderer();
        if (fallbackRenderer != null)
        {
            previewRenderer.sortingLayerID = fallbackRenderer.sortingLayerID;
        }

        int dynamicOrder = enemy.sortingOrderBase + Mathf.RoundToInt(-enemy.transform.position.y * enemy.sortingOrderMultiplier);
        previewRenderer.sortingOrder = dynamicOrder + sortingOrderOffset;
    }

    private Sprite ResolvePreviewSprite()
    {
        if (previewSprite != null)
        {
            return previewSprite;
        }

        if (enemy == null)
        {
            return null;
        }

        if (enemy.intentAttackSprite != null)
        {
            return enemy.intentAttackSprite;
        }

        if (enemy.intentSkillSprite != null)
        {
            return enemy.intentSkillSprite;
        }

        if (enemy.intentMoveSprite != null)
        {
            return enemy.intentMoveSprite;
        }

        return enemy.intentIdleSprite;
    }

    private SpriteRenderer ResolveFallbackRenderer()
    {
        if (enemy == null)
        {
            return null;
        }

        SpriteRenderer[] renderers = enemy.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];
            if (candidate != null && candidate != previewRenderer)
            {
                return candidate;
            }
        }

        return null;
    }

    private bool ShouldShowPreview()
    {
        return isVisible || forceShowForSizeCheck;
    }

    private bool ShouldApplyRuntimeTransform()
    {
        return !isUsingPlacedRenderer || !preservePlacedObjectTransform;
    }

    private bool ShouldApplyRuntimeSorting()
    {
        return !isUsingPlacedRenderer || !preservePlacedObjectSorting;
    }

    private SpriteRenderer ResolvePlacedPreviewRenderer()
    {
        Transform found = FindChildRecursive(transform, "AreaDamagePreviewIcon");
        if (found == null)
        {
            return null;
        }

        return found.GetComponent<SpriteRenderer>();
    }

    private Transform FindChildRecursive(Transform root, string childName)
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

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
