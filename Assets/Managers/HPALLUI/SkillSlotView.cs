using UnityEngine;
using UnityEngine.UI;

public class SkillSlotView : MonoBehaviour
{
    [Header("Icon (choose one)")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private Image iconImage;

    [Header("Cooldown Number")]
    [SerializeField] private SpriteNumber cdNumber;

    public void Bind(Sprite icon, int cd)
    {
        // ✅ icon 為 null：保留 prefab 原本的圖，不要關掉
        if (icon != null)
            SetIcon(icon);

        if (cdNumber != null)
            cdNumber.SetValue(Mathf.Max(0, cd));
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
