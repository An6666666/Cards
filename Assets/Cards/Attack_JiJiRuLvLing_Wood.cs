using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "Attack_JiJiRuLvLing_Wood", menuName = "Cards/Attack/���p�ߥO(��)")]
public class Attack_JiJiRuLvLing_Wood : AttackCardBase
{
    public int baseDamage = 6;

    private void OnEnable() { cardType = CardType.Attack; }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        int dmg = enemy.ApplyElementalAttack(ElementType.Wood, baseDamage, player);
        enemy.TakeDamage(dmg);
        Board board = GameObject.FindObjectOfType<Board>();
        if (board)
        {
            BoardTile t = board.GetTileAt(enemy.gridPosition);
            if (t != null) t.AddElement(ElementType.Wood);
            foreach (var adj in board.GetAdjacentTiles(enemy.gridPosition))
            {
                adj.AddElement(ElementType.Wood);
            }
        }
        
    }
}
