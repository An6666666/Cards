using UnityEngine;                                               // 引用Unity引擎核心功能

[CreateAssetMenu(fileName = "Attack_JiJiRuLvLing_Water", menuName = "Cards/Attack/急急如律令(水)")]
public class Attack_JiJiRuLvLing_Water : Attack_JiJiRuLvLing       // 定義一張水屬性攻擊卡，繼承自Attack_JiJiRuLvLing
{
    [SerializeField] private GameObject waterEffectPrefab;

    protected override ElementType Element => ElementType.Water;

    protected override GameObject EffectPrefab => waterEffectPrefab;
}