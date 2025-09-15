using System.Collections;                                       // 引用集合命名空間
using System.Collections.Generic;                                // 引用泛型集合命名空間
using UnityEngine;                                               // 引用Unity引擎核心功能

[CreateAssetMenu(fileName = "Attack_JiJiRuLvLing_Water", menuName = "Cards/Attack/急急如律令(水)")]
public class Attack_JiJiRuLvLing_Water : AttackCardBase       // 定義一張水屬性攻擊卡，繼承自AttackCardBase
{
    public int baseDamage = 6;                                 // 基礎傷害值

    public GameObject waterEffectPrefab;

    private void OnEnable() { cardType = CardType.Attack; }    // 腳本啟用時，設定卡牌類型為攻擊

    public override void ExecuteEffect(Player player, Enemy enemy)  // 覆寫執行卡牌效果方法
    {
        int dmg = enemy.ApplyElementalAttack(ElementType.Water, baseDamage, player);  // 計算玩家對敵人的水屬性攻擊傷害
        enemy.TakeDamage(dmg);                                        // 令敵人承受傷害
        Board board = GameObject.FindObjectOfType<Board>();          // 在場景中尋找Board物件
        if (board)                                                  // 若找到Board
        {
            BoardTile t = board.GetTileAt(enemy.gridPosition);    // 獲取敵人所在的格子
            if (t != null) t.AddElement(ElementType.Water);        // 在該格子添加水元素標籤
            foreach (var adj in board.GetAdjacentTiles(enemy.gridPosition))  // 遍歷相鄰格子
            {
                adj.AddElement(ElementType.Water);                 // 在相鄰格子也添加水元素標籤
            }
        }

        if (waterEffectPrefab != null)
            GameObject.Instantiate(waterEffectPrefab, enemy.transform.position, Quaternion.identity);

        AudioManager.Instance.PlayAttackSFX(ElementType.Water);

    }
}