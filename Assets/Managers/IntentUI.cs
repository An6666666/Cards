// IntentUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 負責把 EnemyIntent 的資料顯示在 UI 上（小圖示 + 數字）。
/// 掛在 IntentUI 預置物的根物件上。
/// </summary>
public class IntentUI : MonoBehaviour
{
    [Header("UI 元件")]
    public Image iconImage;              // 顯示意圖圖示
    public TextMeshProUGUI valueText;    // 顯示數值（例如攻擊傷害）

    private Enemy _enemy;                // 綁定的敵人（目前沒用到，只是保留參考）

    /// <summary>
    /// 外部呼叫，更新這個 UI 要顯示什麼。
    /// </summary>
    /// <param name="enemy">是哪一隻敵人的意圖</param>
    /// <param name="intent">敵人下一回合意圖資料</param>
    /// <param name="attackSprite">攻擊圖示</param>
    /// <param name="moveSprite">移動圖示</param>
    /// <param name="idleSprite">發呆/待機圖示</param>
    /// <param name="defendSprite">防禦圖示（有需要再用）</param>
    public void Bind(
        Enemy enemy,
        EnemyIntent intent,
        Sprite attackSprite,
        Sprite moveSprite,
        Sprite idleSprite,
        Sprite defendSprite = null
    )
    {
        _enemy = enemy;

        if (iconImage != null)
        {
            Sprite s = null;

            switch (intent.type)
            {
                case EnemyIntentType.Attack:
                    s = attackSprite;
                    break;
                case EnemyIntentType.Move:
                    s = moveSprite;
                    break;
                case EnemyIntentType.Defend:
                    s = defendSprite;
                    break;
                case EnemyIntentType.Idle:
                default:
                    s = idleSprite;
                    break;
            }

            iconImage.sprite = s;
            iconImage.enabled = (s != null);
        }

        if (valueText != null)
        {
            // 只有 Attack 才顯示數字，其它意圖就空白
            if (intent.type == EnemyIntentType.Attack && intent.value > 0)
                valueText.text = intent.value.ToString();
            else
                valueText.text = "";
        }
    }
}
