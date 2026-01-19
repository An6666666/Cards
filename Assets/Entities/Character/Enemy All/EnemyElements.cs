using System.Collections.Generic;
using UnityEngine;

public class EnemyElements : MonoBehaviour
{
    private Enemy enemy;
    private HashSet<ElementType> elementTags = new HashSet<ElementType>();
    private readonly List<ElementType> elementOrder = new List<ElementType>(); // 紀錄元素附著的先後順序（後加入的在尾端）

    public void Init(Enemy owner)
    {
        enemy = owner;
    }

    public bool HasElement(ElementType e)
    {
        return elementTags.Contains(e);
    }

    public void AddElementTag(ElementType e)
    {
        bool addedNew = elementTags.Add(e);

        // 重新調整加入順序，確保最新附著的元素在列表尾端
        elementOrder.Remove(e);
        elementOrder.Add(e);

        ResolveWaterWoodConflict();

        if (addedNew)
        {
            enemy?.RaiseElementTagsChanged();
        }
        else
        {
            // 即便元素已存在，重設附著時間也可能影響反應優先度，通知 UI 更新
            enemy?.RaiseElementTagsChanged();
        }
    }

    private void ResolveWaterWoodConflict()
    {
        if (!elementTags.Contains(ElementType.Water) || !elementTags.Contains(ElementType.Wood))
        {
            return;
        }

        int waterIndex = elementOrder.IndexOf(ElementType.Water);
        int woodIndex = elementOrder.IndexOf(ElementType.Wood);

        if (waterIndex < woodIndex)
        {
            RemoveElementTag(ElementType.Water);
        }
        else if (woodIndex < waterIndex)
        {
            RemoveElementTag(ElementType.Wood);
        }
    }
    
    public void RemoveElementTag(ElementType e)
    {
        if (elementTags.Remove(e))
        {
            elementOrder.Remove(e);
            enemy?.RaiseElementTagsChanged();
        }
    }

    public IEnumerable<ElementType> GetElementTags()
    {
        return elementTags;
    }

    /// <summary>
    /// 依照附著順序（最新在前）回傳元素列表，供反應判斷使用。
    /// </summary>
    public IEnumerable<ElementType> GetElementTagsByRecentOrder()
    {
        for (int i = elementOrder.Count - 1; i >= 0; i--)
        {
            yield return elementOrder[i];
        }
    }

    public int ApplyElementalAttack(ElementType e, int baseDamage, Player player)
    {
        var strat = ElementalStrategyProvider.Get(e);
        return strat.CalculateDamage(player, enemy, baseDamage);
    }

    public void ProcessTurnStart()
    {
        // 目前元素效果沒有在回合開始觸發，保留此方法供未來擴充。
    }

    public void ProcessPlayerTurnEnd()
    {
        var tagsCopy = new List<ElementType>(elementTags);
        foreach (var tag in tagsCopy)
        {
            var strat = ElementalStrategyProvider.Get(tag);

            if (strat is IPlayerEndTurnEffect effect)
            {
                effect.OnPlayerEndTurn(enemy);
            }
        }
    }

    public void SetInitialTags(IEnumerable<ElementType> existing)
    {
        elementTags.Clear();
        elementOrder.Clear();
        if (existing == null) return;
        foreach (var tag in existing)
        {
            if (elementTags.Add(tag))
            {
                elementOrder.Add(tag);
            }
        }
    }
}
