using UnityEngine;

[CreateAssetMenu(fileName = "Attack_JiJiRuLvLing_Fire", menuName = "Cards/Attack/急急如律令(火)")]
public class Attack_JiJiRuLvLing_Fire : Attack_JiJiRuLvLing
{
    [SerializeField] private GameObject fireEffectPrefab;

    protected override ElementType Element => ElementType.Fire;

    protected override GameObject EffectPrefab => fireEffectPrefab;
}