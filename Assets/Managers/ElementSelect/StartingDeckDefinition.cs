using System;                                // 使用 System 命名空間（提供 Serializable 等）
using System.Collections.Generic;            // 使用泛型集合（List、Dictionary 等）
using System.Linq;                           // 使用 LINQ（Where、GroupBy、ToDictionary 等）
using UnityEngine;                           // 使用 Unity 引擎核心 API（ScriptableObject、Debug 等）

[Serializable]                               // 讓這個類別可被 Unity 序列化（Inspector 可顯示）
public class ElementalStarterCard            // 用來描述「元素 → 對應的起始卡」的資料結構
{
    public ElementType element;              // 元素類型（火、水、冰、雷、木等）
    public Attack_JiJiRuLvLing card;         // 該元素對應的「急急如律令」攻擊卡
}

[CreateAssetMenu(fileName = "StartingDeckDefinition", menuName = "Run/Starting Deck Definition")]
// 讓你能在 Unity 右鍵 Create 選單中建立這個 ScriptableObject 資產
public class StartingDeckDefinition : ScriptableObject
// 起始卡組定義：依照玩家選擇的元素，組出進入 Run 的起始牌組
{
    [SerializeField] private int copiesPerElement = 2;
    // 每個被選到的元素，要放幾張「急急如律令」（預設 2 張）

    [SerializeField] private int huWoZhenShenCopies = 4;
    // 「護我真身」要放幾張（預設 4 張）

    [SerializeField] private Skill_HuWoZhenShen huWoZhenShenCard;
    // 「護我真身」這張技能卡的引用（ScriptableObject 卡資料）

    [SerializeField] private List<ElementalStarterCard> elementalCards = new List<ElementalStarterCard>();
    // 元素對應卡表（每個元素對應一張 急急如律令 卡）

    public List<CardBase> BuildDeck(IEnumerable<ElementType> selectedElements)
    // 依照選擇的元素清單，建立起始牌組並回傳
    {
        List<CardBase> result = new List<CardBase>();
        // 最終回傳的牌組列表（以 CardBase 型別統一存放）

        if (selectedElements == null)
        {
            return result;
            // 若沒有選擇元素（傳入為 null），直接回傳空牌組
        }

        Dictionary<ElementType, Attack_JiJiRuLvLing> map = elementalCards
            .Where(entry => entry != null)
            // 過濾掉清單中為 null 的項目
            .GroupBy(entry => entry.element)
            // 依照 element 將資料分組（避免同一元素重複）
            .ToDictionary(group => group.Key, group => group.Last().card);
            // 建立字典：key=元素，value=該元素最後一筆設定的 card（若同元素有多筆，取最後一筆）

        foreach (ElementType element in selectedElements)
        // 逐一處理玩家選擇的每個元素
        {
            if (!map.TryGetValue(element, out Attack_JiJiRuLvLing card) || card == null)
            // 若該元素沒有配置對應卡，或取到的卡是 null
            {
                Debug.LogWarning($"StartingDeckDefinition: No 急急如律令 card configured for {element}.");
                // 顯示警告：該元素沒有配置急急如律令卡
                continue;
                // 跳過這個元素，繼續下一個
            }

            for (int i = 0; i < copiesPerElement; i++)
            // 依 copiesPerElement 的數量複製加入卡牌
            {
                result.Add(card);
                // 把該元素對應的「急急如律令」加入結果牌組
            }
        }

        if (huWoZhenShenCard == null)
        // 若沒有配置護我真身卡
        {
            Debug.LogWarning("StartingDeckDefinition: 護我真身 card is not configured.");
            // 顯示警告：護我真身卡未配置
        }
        else
        {
            for (int i = 0; i < huWoZhenShenCopies; i++)
            // 依 huWoZhenShenCopies 的數量加入護我真身
            {
                result.Add(huWoZhenShenCard);
                // 把「護我真身」加入結果牌組
            }
        }

        return result;
        // 回傳建立完成的起始牌組
    }
}
