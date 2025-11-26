using System.Collections.Generic;                // 引用泛型集合，這裡要用 List<>
using UnityEngine;                               // 引用 UnityEngine，才能使用 ScriptableObject 等 Unity 類型

// 建立一個可在 Unity 建立資產的 ScriptableObject，放在「Run/Encounter Pool」選單底下，檔名預設為 EncounterPool
[CreateAssetMenu(menuName = "Run/Encounter Pool", fileName = "EncounterPool")]
public class EncounterPool : ScriptableObject    // 定義一個戰鬥組合池，用來裝很多場可用的戰鬥
{
    // 在 Inspector 中序列化一個清單，裡面放多個 RunEncounterDefinition（每一個就是一種戰鬥配置）
    [SerializeField] private List<RunEncounterDefinition> encounters = new List<RunEncounterDefinition>();

    // 對外只讀的屬性，回傳這個清單，讓別的系統可以讀取有哪些戰鬥組合
    public IReadOnlyList<RunEncounterDefinition> Encounters => encounters;

    // 從池子裡面隨機拿一個戰鬥組合

    public RunEncounterDefinition GetRandomEncounter()
    {
        // 如果清單是空的或根本沒設定，直接回傳 null，代表目前沒有戰鬥可用
        if (encounters == null || encounters.Count == 0)
            return null;

        // 用 Unity 的隨機數，在 0 ~ (清單長度-1) 之間取一個索引
        int index = UnityEngine.Random.Range(0, encounters.Count);
        // 回傳這個索引位置的戰鬥配置
        return encounters[index];
    }
}
