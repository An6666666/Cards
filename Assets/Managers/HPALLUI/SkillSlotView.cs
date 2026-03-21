using UnityEngine;
using UnityEngine.UI;

public class SkillSlotView : MonoBehaviour
{
    [Header("Icon (choose one)")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private Image iconImage;

    [Header("Cooldown Number")]
    [SerializeField] private SpriteNumber cdNumber;

    public void Bind(Sprite icon, int cd, bool showCooldown)
    {
        SetIcon(icon);

        if (cdNumber != null)
        {
            cdNumber.gameObject.SetActive(showCooldown);
            if (showCooldown)
                cdNumber.SetValue(Mathf.Max(0, cd));
        }
    }

    private void SetIcon(Sprite icon)
    {
        bool hasIcon = icon != null;

        if (iconRenderer != null)
        {
            iconRenderer.sprite = icon;
            iconRenderer.enabled = hasIcon;
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = hasIcon;
        }
    }
}
