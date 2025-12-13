using UnityEngine;

[CreateAssetMenu(fileName = "Attack_JiJiRuLvLing_Thunder", menuName = "Cards/Attack/急急如律令(雷)")]
public class Attack_JiJiRuLvLing_Thunder : Attack_JiJiRuLvLing
{
    [SerializeField] private GameObject thunderEffectPrefab;

    protected override ElementType Element => ElementType.Thunder;

    protected override GameObject EffectPrefab => thunderEffectPrefab;
}