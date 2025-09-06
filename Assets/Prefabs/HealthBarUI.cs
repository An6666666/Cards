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

    void Update()
    {
        float percent = 1f;
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
            if (enemy.currentHP <= 0)
            {
                Destroy(gameObject);
                return;
            }
            percent = (float)enemy.currentHP / enemy.maxHP;
        }
        // 或只處理 player
        else if (player != null)
        {
            if (player.currentHP <= 0)
            {
                Destroy(gameObject);
                return;
            }
            percent = (float)player.currentHP / player.maxHP;
        }

        // 更新血量縮放
        if (hp_fill != null)
        {
            Vector3 s = hp_fill.transform.localScale;
            hp_fill.transform.localScale = new Vector3(percent, s.y, s.z);
        }
    }
}
