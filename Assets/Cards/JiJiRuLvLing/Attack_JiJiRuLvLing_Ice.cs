using UnityEngine;

[CreateAssetMenu(fileName = "Attack_JiJiRuLvLing_Ice", menuName = "Cards/Attack/急急如律令(冰)")]
public class Attack_JiJiRuLvLing_Ice : Attack_JiJiRuLvLing
{
    [SerializeField] private GameObject iceEffectPrefab;

    protected override ElementType Element => ElementType.Ice;

    protected override GameObject EffectPrefab => iceEffectPrefab;
}