using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Attack_JiJiRuLvLing_Ice", menuName = "Cards/Attack/急急如律令(冰)")]
public class Attack_JiJiRuLvLing_Ice : AttackCardBase
{
    public int baseDamage = 6;

    public GameObject iceEffectPrefab;

    private void OnEnable() { cardType = CardType.Attack; }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        int dmg = enemy.ApplyElementalAttack(ElementType.Ice, baseDamage, player);
        enemy.TakeDamage(dmg);
        Board board = GameObject.FindObjectOfType<Board>();
        if (board)
        {
            foreach (var adjE in GameObject.FindObjectsOfType<Enemy>())
            {
                if (adjE == enemy) continue;
                if (Vector2Int.Distance(adjE.gridPosition, enemy.gridPosition) <= 1.1f)
                {
                    adjE.AddElementTag(ElementType.Ice);
                }
            }
        }
        
         if (iceEffectPrefab != null)
            GameObject.Instantiate(iceEffectPrefab, enemy.transform.position, Quaternion.identity);

        AudioManager.Instance.PlayAttackSFX(ElementType.Ice);

    }
}
