using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///地圖單一格，可被高亮與點擊
/// </summary>
public class BoardTile : MonoBehaviour
{
    public Vector2Int gridPosition;              // 必填：該格在地圖的座標
    [SerializeField] private GameObject highlightObject; // 指向高亮用的子物件
    [SerializeField] private GameObject attackPreviewHighlightObject; // 指向敵人攻擊預告高亮
    [SerializeField] private GameObject growthTrapIcon;  // 指向荊棘圖示的子物件
    [SerializeField] private GameObject waterElementIcon; // 水屬性圖示
    [SerializeField] private GameObject woodElementIcon;  // 木屬性圖示
    [SerializeField] private GameObject miasmaEffectObject; // 瘴氣效果顯示

    // 元素標籤紀錄
    private HashSet<ElementType> elements = new HashSet<ElementType>();
    public bool growthTrap = false; // 水+木產生的陷阱
    private bool hasMiasma = false;                     // 是否佈滿瘴氣
    private int miasmaDamage = 0;                       // 瘴氣造成的傷害
    // 讓 BattleManager 呼叫

     private void Awake()
    {
        UpdateElementIcons();
    }


    public void TriggerGrowthTrap(Enemy enemy)
    {
        if (growthTrap)
        {
            enemy.TakeDamage(3);  // 例如扣3血
            Debug.Log("Growth trap triggered! Enemy took damage.");
        }
    }
    public void SetSelectable(bool canSelect)
    {
        // 顯示/隱藏高亮
        if (highlightObject) highlightObject.SetActive(canSelect);

        // 動態加/移除點擊偵測組件
        var selectable = GetComponent<BoardTileSelectable>();
        if (canSelect && selectable == null)
        {
            gameObject.AddComponent<BoardTileSelectable>();
        }
        else if (!canSelect && selectable != null)
        {
            Destroy(selectable);
        }
    }

    /// <summary>
    /// 單純控制高亮顯示，不影響可選狀態
    /// </summary>
    /// <param name="show">是否顯示高亮</param>
    public void SetHighlight(bool show)
    {
        if (highlightObject) highlightObject.SetActive(show);
    }

    /// <summary>
    /// 單獨控制敵人攻擊範圍的高亮顯示
    /// </summary>
    public void SetAttackHighlight(bool show)
    {
        if (attackPreviewHighlightObject)
        {
            attackPreviewHighlightObject.SetActive(show);
        }
    }

    /// <summary>
    /// 查詢目前攻擊預覽高亮是否啟用中
    /// </summary>
    public bool IsAttackHighlightActive()
    {
        return attackPreviewHighlightObject && attackPreviewHighlightObject.activeSelf;
    }
    
    /// 新增：元素處理
    public void AddElement(ElementType e)
    {
        elements.Add(e);
        if (elements.Contains(ElementType.Water) && elements.Contains(ElementType.Wood))
        {
            growthTrap = true;
        }

        UpdateGrowthTrapVisual();
        UpdateElementIcons();
    }

    public void RemoveElement(ElementType e)
    {
        elements.Remove(e);
        if (!(elements.Contains(ElementType.Water) && elements.Contains(ElementType.Wood)))
        {
            growthTrap = false;
        }

        UpdateGrowthTrapVisual();
        UpdateElementIcons();
    }

    public bool HasElement(ElementType e)
    {
        return elements.Contains(e);
    }

     /// <summary>
    /// 檢查是否佈滿瘴氣
    /// </summary>
    public bool HasMiasma => hasMiasma;

    /// <summary>
    /// 獲取該格瘴氣造成的傷害值（若沒有瘴氣則為 0）
    /// </summary>
    public int MiasmaDamage => hasMiasma ? Mathf.Max(0, miasmaDamage) : 0;

    /// <summary>
    /// 設定瘴氣狀態
    /// </summary>
    public void SetMiasma(bool active, int damage)
    {
        hasMiasma = active;
        miasmaDamage = active ? Mathf.Max(0, damage) : 0;

        if (miasmaEffectObject)
        {
            miasmaEffectObject.SetActive(active);
        }
    }

    /// <summary>
    /// 當玩家進入此格時的觸發處理
    /// </summary>
    public void HandlePlayerEntered(Player player)
    {
        if (player == null)
        {
            return;
        }
    }
    
    private void UpdateElementIcons()
    {
        bool hasWater = HasElement(ElementType.Water);
        bool hasWood = HasElement(ElementType.Wood);

        if (waterElementIcon)
        {
            // 水圖示完全依照棋盤格是否含有水屬性標籤
            waterElementIcon.SetActive(hasWater);
        }

        if (woodElementIcon)
        {
            bool showWood = hasWood && !growthTrap;
            woodElementIcon.SetActive(showWood);
        }
    }

    private void OnEnable()
    {
        UpdateGrowthTrapVisual();
        UpdateElementIcons();
    }

    private void UpdateGrowthTrapVisual()
    {
        if (growthTrapIcon)
        {
            growthTrapIcon.SetActive(growthTrap);
        }
    }
}
