using UnityEngine;
using UnityEngine.UI;

public class StatusSlotView : MonoBehaviour
{
    [Header("Icon (choose one)")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private Image iconImage;

    [Header("Turns Number")]
    [SerializeField] private SpriteNumber turnNumber;

    public void Bind(Sprite icon, int turns)
    {
        // ✅ icon 為 null：保留 prefab 原本的圖，不要關掉
        if (icon != null)
            SetIcon(icon);

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
}
