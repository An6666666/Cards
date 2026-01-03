using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("目標 (二選一)")]
    public Player player;   // 拖 Player 進來
    public Enemy enemy;     // 或拖 Enemy 進來

    [Header("UI")]
    public GameObject hp_fill;      // 指向 Fill
    public Text hpText;             // 顯示血量文字（可選）

    [Header("數字貼圖（可選）")]
    [Tooltip("若指定 Sprites 將用精靈數字顯示血量(優先於文字）。")]
    public Sprite[] digitSprites;   // 0~9 的數字圖

    [Tooltip("依序擺放的 Image 元件（左到右）。")]
    public Image[] digitImages;     // 用來呈現每一位數

    [Tooltip("若在世界空間用 SpriteRenderer（例如 2D Square）顯示血量，依序放置這些 Renderer（左到右）。")]
    public SpriteRenderer[] digitSpriteRenderers;

    void Update()
    {
        float percent = 1f;
        int current = 0;
        int max = 0;
        // 1) 跟著角色位置
        //Transform t = null;
        //if (player != null) t = player.transform;
        //else if (enemy != null) t = enemy.transform;

        // if (t != null)
        // {
        //     transform.position = t.position + worldOffset;
        //     if (alwaysFaceCamera && _cam != null)
        //         transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up); // 2D 正面
        // }

        // 2) 更新血量比例
        // 只處理 enemy
        if (enemy != null)
        {
            bool shouldDestroy = enemy.currentHP <= 0;
            // YingGe 假死等待復活時不要銷毀血條，避免復活後消失
            if (enemy is YingGe yingGe && yingGe.IsAwaitingRespawn)
            {
                shouldDestroy = false;
            }

            if (shouldDestroy)
            {
                Destroy(gameObject);
                return;
            }
            max = Mathf.Max(0, enemy.maxHP);
            current = Mathf.Max(0, enemy.currentHP);
            float safeMax = Mathf.Max(1, max);
            percent = Mathf.Clamp01((float)current / safeMax);
        }
        // 或只處理 player
        else if (player != null)
        {
            max = Mathf.Max(0, player.maxHP);
            current = Mathf.Max(0, player.currentHP);
            float safeMax = Mathf.Max(1, max);
            percent = Mathf.Clamp01((float)current / safeMax);
        }
        else
        {
            return;
        }

        // 更新血量縮放
        if (hp_fill != null)
        {
            Vector3 s = hp_fill.transform.localScale;
            hp_fill.transform.localScale = new Vector3(percent, s.y, s.z);
        }
        
        string hpString = $"{current}";
        if (TryUpdateDigitSprites(hpString)) return;

        if (hpText != null)
        {
            hpText.text = hpString;
        }
    }

    private bool TryUpdateDigitSprites(string hpString)
    {
        if (!HasDigitSprites()) return false;

        bool usedImage = ApplySpritesToImages(hpString, digitImages);
        bool usedRenderers = ApplySpritesToRenderers(hpString, digitSpriteRenderers);

        return usedImage || usedRenderers;
    }

    private bool HasDigitSprites()
    {
        return digitSprites != null && digitSprites.Length >= 10;
    }

    private bool ApplySpritesToImages(string hpString, Image[] targets)
    {
        if (targets == null || targets.Length == 0) return false;
        string truncated = TruncateToFit(hpString, targets.Length);
        int startIndex = targets.Length - truncated.Length; // 右對齊，讓個位數永遠落在最後一格

        for (int i = 0; i < targets.Length; i++)
        {
            Image img = targets[i];
            if (img == null) continue;

            if (i >= startIndex && i - startIndex < truncated.Length && char.IsDigit(truncated[i - startIndex]))
            {
                int digit = truncated[i - startIndex] - '0';
                img.sprite = digitSprites[digit];
                img.enabled = img.sprite != null;
            }
            else
            {
                img.sprite = digitSprites[0]; // 無該位數時顯示 0
                img.enabled = img.sprite != null;
            }
        }

        return true;
    }

    private bool ApplySpritesToRenderers(string hpString, SpriteRenderer[] targets)
    {
        if (targets == null || targets.Length == 0) return false;
        string truncated = TruncateToFit(hpString, targets.Length);
        int startIndex = targets.Length - truncated.Length; // 右對齊

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
                renderer.sprite = digitSprites[0]; // 無該位數時顯示 0
                renderer.enabled = renderer.sprite != null;
            }
        }

        return true;
    }

    private string TruncateToFit(string hpString, int capacity)
    {
        if (hpString.Length > capacity)
        {
            return hpString.Substring(hpString.Length - capacity);
        }
        return hpString;
    }
}
