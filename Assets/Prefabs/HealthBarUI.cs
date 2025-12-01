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
        if (hpText != null)
        {
            hpText.text = hpString;
        }
    }
}
