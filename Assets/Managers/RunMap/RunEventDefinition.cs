using System.Collections.Generic;                                // 要用 List<>
using UnityEngine;                                               // Unity 基本命名空間

// 可以在 Unity 裡面建立的事件定義 ScriptableObject
[CreateAssetMenu(menuName = "Run/Event Definition", fileName = "RunEvent")]
public class RunEventDefinition : ScriptableObject               // 這個類別代表「跑團中的一個事件」
{
    [SerializeField] private string eventId;                      // 事件的識別碼，可用來存檔或比對
    [SerializeField] private string title;                        // 事件標題，UI 會顯示
    [TextArea] [SerializeField] private string description;       // 事件描述，支援多行文字
    [SerializeField] private List<RunEventOption> options = new List<RunEventOption>(); // 這個事件有哪些可選的選項

    // 對外給事件 ID，如果沒填就用資產名稱
    public string EventId => string.IsNullOrEmpty(eventId) ? name : eventId;
    // 對外給標題
    public string Title => title;
    // 對外給描述
    public string Description => description;
    // 對外給選項清單（唯讀）
    public IReadOnlyList<RunEventOption> Options => options;
}

// 事件底下的一個「選項」資料結構，要可序列化才能在 Inspector 裡編輯
[System.Serializable]
public class RunEventOption
{
    public string optionLabel;                                   // 按鈕上顯示的文字，例如「幫助他」「離開」
    public int goldDelta;                                        // 金錢變化，可以是 +（給錢）或 -（扣錢）
    public int hpDelta;                                          // 血量變化，可以是 +（回復）或 -（受傷）
    public List<CardBase> rewardCards = new List<CardBase>();    // 選了這個選項後送的卡片清單
    public List<CardBase> rewardRelics = new List<CardBase>();   // 選了這個選項後送的「類似遺物」的東西，目前用 CardBase 當容器
}
