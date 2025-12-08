using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Attack_RanJinShu_Ice", menuName = "Cards/Attack/燃盡術(冰)")]
public class Attack_RanJinShu_Ice : Attack_RanJinShu
{
    protected override ElementType Element => ElementType.Ice;
}