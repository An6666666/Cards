using UnityEngine;

public class EnemyCooldownSpriteUI : MonoBehaviour
{
    [Header("目標")]
    public Enemy enemy;

    [Header("數字貼圖（可選）")]
    [Tooltip("若指定 Sprites 將用精靈數字顯示冷卻（可搭配 SpriteRenderer）。")]
    public Sprite[] digitSprites;

    [Tooltip("依序擺放的 SpriteRenderer（左到右）。")]
    public SpriteRenderer[] digitSpriteRenderers;

    [SerializeField] private bool hideWhenZero = true;

    private void Awake()
    {
        if (enemy == null)
        {
            enemy = GetComponentInParent<Enemy>();
        }
    }

    private void Update()
    {
        int cooldown = GetCooldownRemaining();
        if (cooldown <= 0 && hideWhenZero)
        {
            SetRenderersActive(false);
            return;
        }

        if (!HasDigitSprites())
        {
            SetRenderersActive(false);
            return;
        }

        string cooldownString = Mathf.Max(0, cooldown).ToString();
        bool updated = ApplySpritesToRenderers(cooldownString, digitSpriteRenderers);
        SetRenderersActive(updated);
    }

    private int GetCooldownRemaining()
    {
        if (enemy == null) return 0;

        IEnemyCooldownProvider provider = enemy as IEnemyCooldownProvider;
        if (provider == null)
        {
            provider = enemy.GetComponent<IEnemyCooldownProvider>();
        }

        return provider != null ? Mathf.Max(0, provider.GetCooldownTurnsRemaining()) : 0;
    }

    private bool HasDigitSprites()
    {
        return digitSprites != null && digitSprites.Length >= 10;
    }

    private bool ApplySpritesToRenderers(string valueString, SpriteRenderer[] targets)
    {
        if (targets == null || targets.Length == 0) return false;
        string truncated = TruncateToFit(valueString, targets.Length);
        int startIndex = targets.Length - truncated.Length;

        for (int i = 0; i < targets.Length; i++)
        {
            SpriteRenderer renderer = targets[i];
            if (renderer == null) continue;

            if (i >= startIndex && i - startIndex < truncated.Length && char.IsDigit(truncated[i - startIndex]))
            {
                int digit = truncated[i - startIndex] - '0';
                renderer.sprite = digitSprites[digit];
                renderer.enabled = renderer.sprite != null;
            }
            else
            {
                renderer.sprite = digitSprites[0];
                renderer.enabled = renderer.sprite != null;
            }
        }

        return true;
    }

    private void SetRenderersActive(bool isActive)
    {
        if (digitSpriteRenderers == null) return;

        foreach (SpriteRenderer renderer in digitSpriteRenderers)
        {
            if (renderer == null) continue;
            renderer.enabled = isActive && renderer.sprite != null;
        }
    }

    private string TruncateToFit(string valueString, int capacity)
    {
        if (valueString.Length > capacity)
        {
            return valueString.Substring(valueString.Length - capacity);
        }
        return valueString;
    }
}