using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Attack_JiJiRuLvLing_Water", menuName = "Cards/Attack/���p�ߥO(��)")]
public class Attack_JiJiRuLvLing_Water : AttackCardBase
{
    public int baseDamage = 6;

    private void OnEnable() { cardType = CardType.Attack; }

    public override void ExecuteEffect(Player player, Enemy enemy)
    {
        int dmg = enemy.ApplyElementalAttack(ElementType.Water, baseDamage, player);
        enemy.TakeDamage(dmg);
        Board board = GameObject.FindObjectOfType<Board>();
        if (board)
        {
            BoardTile t = board.GetTileAt(enemy.gridPosition);
            if (t != null) t.AddElement(ElementType.Water);
            foreach (var adj in board.GetAdjacentTiles(enemy.gridPosition))
            {
                adj.AddElement(ElementType.Water);
            }
        }
        
    }
}