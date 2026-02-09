using UnityEngine;
using UnityEngine.UI;

public class SpriteNumber : MonoBehaviour
{
    [Header("Digit Sprites (0~9)")]
    public Sprite[] digitSprites;

    [Header("Render Targets (choose one)")]
    public SpriteRenderer[] digitSpriteRenderers;
    public Image[] digitImages;

    /// <summary>
    /// 設定要顯示的數字
    /// </summary>
    public void SetValue(int value)
    {
        if (digitSprites == null || digitSprites.Length < 10)
            return;

        string valueString = Mathf.Max(0, value).ToString();

        if (digitSpriteRenderers != null && digitSpriteRenderers.Length > 0)
        {
            ApplyToRenderers(valueString, digitSpriteRenderers);
        }
        else if (digitImages != null && digitImages.Length > 0)
        {
            ApplyToImages(valueString, digitImages);
        }
    }

    private void ApplyToRenderers(string valueString, SpriteRenderer[] targets)
    {
        string truncated = TruncateToFit(valueString, targets.Length);
        int startIndex = targets.Length - truncated.Length;

        for (int i = 0; i < targets.Length; i++)
        {
            var r = targets[i];
            if (r == null) continue;

            if (i >= startIndex && i - startIndex < truncated.Length)
            {
                int digit = truncated[i - startIndex] - '0';
                r.sprite = digitSprites[digit];
                r.enabled = true;
            }
            else
            {
                r.enabled = false;
            }
        }
    }

    private void ApplyToImages(string valueString, Image[] targets)
    {
        string truncated = TruncateToFit(valueString, targets.Length);
        int startIndex = targets.Length - truncated.Length;

        for (int i = 0; i < targets.Length; i++)
        {
            var img = targets[i];
            if (img == null) continue;

            if (i >= startIndex && i - startIndex < truncated.Length)
            {
                int digit = truncated[i - startIndex] - '0';
                img.sprite = digitSprites[digit];
                img.enabled = true;
            }
            else
            {
                img.enabled = false;
            }
        }
    }

    private string TruncateToFit(string s, int capacity)
    {
        if (s.Length > capacity)
            return s.Substring(s.Length - capacity);
        return s;
    }
}
