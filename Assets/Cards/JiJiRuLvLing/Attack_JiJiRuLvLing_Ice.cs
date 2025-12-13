using UnityEngine;

[CreateAssetMenu(fileName = "Attack_JiJiRuLvLing_Ice", menuName = "Cards/Attack/急急如律令(冰)")]
public class Attack_JiJiRuLvLing_Ice : Attack_JiJiRuLvLing
{
    [SerializeField] private GameObject iceEffectPrefab;

    protected override ElementType Element => ElementType.Ice;

    protected override GameObject EffectPrefab => iceEffectPrefab;

    protected override void OnAfterDamage(Player player, Enemy enemy, ElementType element, int damage)
    {
        Board board = GameObject.FindObjectOfType<Board>();
        if (board == null)
        {
            return;
        }

        foreach (var adjE in GameObject.FindObjectsOfType<Enemy>())
        {
            if (adjE == enemy) continue;
            if (Vector2Int.Distance(adjE.gridPosition, enemy.gridPosition) <= 1.1f)
            {
                adjE.AddElementTag(ElementType.Ice);
            }
        }
    }
}
