using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IElementalStrategy
{
    int CalculateDamage(Player attacker, Enemy defender, int baseDamage);
}

public class DefaultElementalStrategy : IElementalStrategy
{
    public int CalculateDamage(Player attacker, Enemy defender, int baseDamage)
    {
        return baseDamage;
    }
}

public class FireStrategy : IElementalStrategy
{
    public int CalculateDamage(Player attacker, Enemy defender, int baseDamage)
    {
        if (defender.HasElement(ElementType.Wood))
        {
            return baseDamage + 2;
        }
        return baseDamage;
    }
}

public static class ElementalStrategyProvider
{
    private static readonly System.Collections.Generic.Dictionary<ElementType, IElementalStrategy> map =
        new System.Collections.Generic.Dictionary<ElementType, IElementalStrategy>
        {
            { ElementType.Fire, new FireStrategy() },
            { ElementType.Water, new DefaultElementalStrategy() },
            { ElementType.Thunder, new DefaultElementalStrategy() },
            { ElementType.Ice, new DefaultElementalStrategy() },
            { ElementType.Wood, new DefaultElementalStrategy() }
        };

    public static IElementalStrategy Get(ElementType type)
    {
        if (map.TryGetValue(type, out var strat))
            return strat;
        return new DefaultElementalStrategy();
    }
}
