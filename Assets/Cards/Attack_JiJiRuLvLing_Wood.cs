using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "Attack_JiJiRuLvLing_Wood", menuName = "Cards/Attack/急急如律令(木)")]
public class Attack_JiJiRuLvLing_Wood : AttackCardBase        // 木屬性版本
{
    public int baseDamage = 6;                                 // 基礎傷害值

    public GameObject woodEffectPrefab;

    private void OnEnable() { cardType = CardType.Attack; }    // 設定卡牌為攻擊類型

    public override void ExecuteEffect(Player player, Enemy enemy)  // 執行效果
    {
        int dmg = enemy.ApplyElementalAttack(ElementType.Wood, baseDamage, player);  // 計算木屬性傷害
        enemy.TakeDamage(dmg);                                        // 使敵人承受計算後傷害
        Board board = GameObject.FindObjectOfType<Board>();          // 尋找Board物件
        if (board)                                                  // 若存在Board
        {
            BoardTile t = board.GetTileAt(enemy.gridPosition);    // 取得敵人格子
            if (t != null) t.AddElement(ElementType.Wood);         // 添加木元素
            foreach (var adj in board.GetAdjacentTiles(enemy.gridPosition))  // 遍歷相鄰格
            {
                adj.AddElement(ElementType.Wood);                  // 添加木元素
            }
        }

        if (woodEffectPrefab != null)
            GameObject.Instantiate(woodEffectPrefab, enemy.transform.position, Quaternion.identity);

        AudioManager.Instance.PlayAttackSFX(ElementType.Wood);
    }
}
