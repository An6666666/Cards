using UnityEngine;
[CreateAssetMenu(fileName = "Attack_JiJiRuLvLing_Wood", menuName = "Cards/Attack/急急如律令(木)")]
public class Attack_JiJiRuLvLing_Wood : Attack_JiJiRuLvLing        // 木屬性版本
{
    [SerializeField] private GameObject woodEffectPrefab;

    protected override ElementType Element => ElementType.Wood;

    protected override GameObject EffectPrefab => woodEffectPrefab;
}