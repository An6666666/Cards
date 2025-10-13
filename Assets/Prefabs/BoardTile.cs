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
    [SerializeField] private GameObject growthTrapIcon;  // 指向荊棘圖示的子物件

    // 元素標籤紀錄
    private HashSet<ElementType> elements = new HashSet<ElementType>();
    public bool growthTrap = false; // 水+木產生的陷阱
    // 讓 BattleManager 呼叫

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

    /// 新增：元素處理
    public void AddElement(ElementType e)
    {
        elements.Add(e);
        if (elements.Contains(ElementType.Water) && elements.Contains(ElementType.Wood))
        {
            growthTrap = true;
        }

        UpdateGrowthTrapVisual();
    }

    public void RemoveElement(ElementType e)
    {
        elements.Remove(e);
        if (!(elements.Contains(ElementType.Water) && elements.Contains(ElementType.Wood)))
        {
            growthTrap = false;
        }

        UpdateGrowthTrapVisual();
    }

    public bool HasElement(ElementType e)
    {
        return elements.Contains(e);
    }

     private void OnEnable()
    {
        UpdateGrowthTrapVisual();
    }

    private void UpdateGrowthTrapVisual()
    {
        if (growthTrapIcon)
        {
            growthTrapIcon.SetActive(growthTrap);
        }
    }
}
