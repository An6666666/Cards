using System.Collections.Generic;
using UnityEngine;

public class EnemyElements : MonoBehaviour
{
    private Enemy enemy;
    private HashSet<ElementType> elementTags = new HashSet<ElementType>();

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
        if (elementTags.Add(e))
        {
            enemy?.RaiseElementTagsChanged();
        }
    }

    public void RemoveElementTag(ElementType e)
    {
        if (elementTags.Remove(e))
        {
            enemy?.RaiseElementTagsChanged();
        }
    }

    public IEnumerable<ElementType> GetElementTags()
    {
        return elementTags;
    }

    public int ApplyElementalAttack(ElementType e, int baseDamage, Player player)
    {
        var strat = ElementalStrategyProvider.Get(e);
        return strat.CalculateDamage(player, enemy, baseDamage);
    }

    public void ProcessTurnStart()
    {
        var tagsCopy = new List<ElementType>(elementTags);
        foreach (var tag in tagsCopy)
        {
            var strat = ElementalStrategyProvider.Get(tag);

            if (strat is IStartOfTurnEffect effect)
            {
                effect.OnStartOfTurn(enemy);
            }
        }
    }

    public void SetInitialTags(IEnumerable<ElementType> existing)
    {
        elementTags.Clear();
        if (existing == null) return;
        foreach (var tag in existing)
        {
            elementTags.Add(tag);
        }
    }
}
