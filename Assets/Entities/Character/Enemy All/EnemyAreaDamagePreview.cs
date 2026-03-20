using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class EnemyAreaDamagePreview : MonoBehaviour
{
    [Header("Preview Icon")]
    [Tooltip("Optional override sprite. If empty, the preview reuses the enemy intent sprite.")]
    [SerializeField] private Sprite previewSprite;
    [SerializeField] private Vector3 fallbackLocalOffset = new Vector3(-0.85f, 4.25f, 0f);
    [FormerlySerializedAs("offsetFromIntentIcon")]
    [Tooltip("Offset relative to the enemy transform center.")]
    [SerializeField] private Vector3 offsetFromEnemyCenter = new Vector3(-0.8f, 4.05f, 0f);
    [SerializeField] private float iconScale = 0.65f;
    [SerializeField] private Color iconColor = new Color(1f, 0.45f, 0.2f, 0.95f);
    [SerializeField] private int sortingOrderOffset = 3;

    private Enemy enemy;
    private SpriteRenderer previewRenderer;
    private Transform previewTransform;
    private bool isVisible;

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
        if (!isVisible || previewRenderer == null || !previewRenderer.enabled)
        {
            return;
        }

        SyncVisualState();
    }

    private void EnsurePreviewRenderer()
    {
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
    }

    private void SyncVisualState()
    {
        if (previewRenderer == null)
        {
            return;
        }

        Sprite iconSprite = ResolvePreviewSprite();
        bool shouldShow = isVisible && enemy != null && iconSprite != null;
        if (!shouldShow)
        {
            previewRenderer.enabled = false;
            return;
        }

        previewRenderer.sprite = iconSprite;
        previewRenderer.color = iconColor;
        previewRenderer.enabled = true;

        if (previewTransform != null)
        {
            previewTransform.localPosition = ResolveLocalPosition();
            previewTransform.localScale = Vector3.one * iconScale;
        }

        ApplySorting();
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
}
