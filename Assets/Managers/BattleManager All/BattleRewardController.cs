using System.Collections.Generic;                 // 使用泛型集合（List<T> 等）
using UnityEngine;                                // 使用 Unity 引擎 API（Object、Transform 等）

public class BattleRewardController               // 戰鬥獎勵控制器：處理擊殺數、金幣、以及勝利獎勵 UI
{
    private readonly BattleManager battleManager;  // 戰鬥管理器，用於在獎勵 UI 中回呼或需要上下文
    private readonly Player player;                // 玩家物件（加金幣、取得獎勵卡）
    private readonly List<CardBase> allCardPool;   // 所有可用作獎勵的卡片池
    private readonly RewardUI rewardUIPrefab;      // 獎勵 UI 的 Prefab
    private readonly Transform handPanel;          // 手牌所在的 UI 節點，用來往上找 Canvas

    private int defeatedEnemyCount = 0;            // 本場戰鬥已擊敗敵人數量
    private int totalGoldReward = 0;               // 累積的金幣獎勵總額
    private RewardUI rewardUIInstance;             // 已經生成的獎勵 UI 實例（避免重複生成）

    public BattleRewardController(BattleManager battleManager, Player player, List<CardBase> allCardPool, RewardUI rewardUIPrefab, Transform handPanel)
    {
        this.battleManager = battleManager;        // 存下 BattleManager 引用
        this.player = player;                      // 存下 Player 引用
        this.allCardPool = allCardPool;            // 存下獎勵卡池列表
        this.rewardUIPrefab = rewardUIPrefab;      // 存下獎勵 UI Prefab
        this.handPanel = handPanel;                // 存下手牌 Panel，用來取得 Canvas
    }

    public void OnEnemyDefeated(Enemy e)
    {
        defeatedEnemyCount++;                      // 擊敗敵人數 +1
        totalGoldReward += Mathf.Max(0, e != null ? e.GoldReward : 0);
        // 累加金幣：若敵人不為 null，取其 GoldReward，至少為 0
    }

    public void ShowVictoryRewards()
    {
        int goldReward = totalGoldReward;          // 把累積金幣複製到局部變數（之後給 UI & Player）
        player.AddGold(goldReward);                // 直接將金幣加到玩家身上

        var cardChoices = GetRandomCards(allCardPool, 3);
        // 從卡池中隨機抽出 3 張卡片作為選項

        Canvas canvas = handPanel != null ? handPanel.GetComponentInParent<Canvas>() : Object.FindObjectOfType<Canvas>();
        // 優先從 handPanel 往上找 Canvas，如果沒有就隨機找場景中的第一個 Canvas

        if (rewardUIInstance == null)
            rewardUIInstance = Object.Instantiate(rewardUIPrefab, canvas.transform);
        // 如果還沒生成過 RewardUI，就在 Canvas 底下 Instantiate 一個

        rewardUIInstance.Show(battleManager, goldReward, cardChoices);
        // 顯示獎勵 UI，並把 BattleManager、金幣數與卡片選項傳進去
    }

    private List<CardBase> GetRandomCards(List<CardBase> pool, int count)
    {
        List<CardBase> result = new List<CardBase>();    // 用來回傳的卡片清單
        if (pool == null) return result;                 // 若池子是 null，直接回傳空清單

        List<CardBase> temp = new List<CardBase>(pool);  // 複製一份池子，避免修改原列表

        for (int i = 0; i < count && temp.Count > 0; i++)// 執行 count 次，或直到池子沒牌
        {
            int idx = UnityEngine.Random.Range(0, temp.Count); // 隨機抽一個索引
            result.Add(temp[idx]);                             // 把抽到的那張加入結果
            temp.RemoveAt(idx);                                // 從暫存池移除，避免重複
        }
        return result;                                         // 回傳抽到的卡片列表
    }
}
