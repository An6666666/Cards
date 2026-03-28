using UnityEngine;
using UnityEngine.UI;

public class StatusSlotView : MonoBehaviour
{
    [Header("Icon (choose one)")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private Image iconImage;

    [Header("Turns Number")]
    [SerializeField] private SpriteNumber turnNumber;

    private Sprite defaultRendererSprite;
    private bool defaultRendererEnabled;
    private Sprite defaultImageSprite;
    private bool defaultImageEnabled;

    private void Awake()
    {
        if (iconRenderer != null)
        {
            defaultRendererSprite = iconRenderer.sprite;
            defaultRendererEnabled = iconRenderer.enabled;
        }

        if (iconImage != null)
        {
            defaultImageSprite = iconImage.sprite;
            defaultImageEnabled = iconImage.enabled;
        }
    }

    public void Bind(Sprite icon, int turns)
    {
        if (icon != null)
            SetIcon(icon);
        else
            RestoreDefaultIcon();

        if (turnNumber != null)
            turnNumber.SetValue(Mathf.Max(0, turns));
    }

    private void SetIcon(Sprite icon)
    {
        if (iconRenderer != null)
        {
            iconRenderer.sprite = icon;
            iconRenderer.enabled = true;
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = true;
        }
    }

    private void RestoreDefaultIcon()
    {
        if (iconRenderer != null)
        {
            iconRenderer.sprite = defaultRendererSprite;
            iconRenderer.enabled = defaultRendererEnabled;
        }

        if (iconImage != null)
        {
            iconImage.sprite = defaultImageSprite;
            iconImage.enabled = defaultImageEnabled;
        }
    }
}
