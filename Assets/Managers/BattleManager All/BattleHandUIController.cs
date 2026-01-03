using System.Collections;                     // 引用協程相關的命名空間（提供 IEnumerator 等）
using System.Collections.Generic;             // 引用泛型集合命名空間（提供 List<T> 等）
using UnityEngine;                            // 引用 Unity 引擎核心 API
using UnityEngine.UI;                         // 引用 UI 相關元件（Text、Button、LayoutRebuilder 等）

public class BattleHandUIController           // 戰鬥場景中「手牌 UI」的控制類（純 C# 類，非 MonoBehaviour）
{
    private readonly BattleManager battleManager;  // 持有 BattleManager 參考，用於呼叫戰鬥流程
    private readonly Player player;                // 玩家資料與狀態
    private readonly GameObject cardPrefab;        // 用於生成卡片 UI 的預製物 (Prefab)
    private readonly Transform handPanel;          // 放置手牌卡片的 UI 容器（通常是 Horizontal/Vertical Layout）
    private readonly Transform deckPile;           // 牌庫 UI（顯示剩餘牌數的地方）
    private readonly Transform discardPile;        // 棄牌堆 UI（顯示棄牌數量的地方）
    private readonly Text energyText;              // 能量顯示文字
    private readonly Button endTurnButton;         // 結束回合按鈕
    private readonly float cardUseDelay;           // 卡片使用後再解鎖互動的延遲時間（秒）

    private bool cardInteractionLocked = false;    // 是否鎖住卡片互動（例如回合開始動畫期間不能點卡）

    public bool IsCardInteractionLocked => cardInteractionLocked;
    // 對外提供只讀屬性，讓外部知道目前卡片是否被鎖住（無法互動）

    public BattleHandUIController(
        BattleManager battleManager,               // 傳入戰鬥管理器參考
        Player player,                             // 傳入玩家物件
        GameObject cardPrefab,                     // 傳入卡片 UI 的 Prefab
        Transform handPanel,                       // 傳入手牌容器
        Transform deckPile,                        // 傳入牌庫 UI 容器
        Transform discardPile,                     // 傳入棄牌堆 UI 容器
        Text energyText,                           // 傳入顯示能量的 Text
        Button endTurnButton,                      // 傳入結束回合 Button
        float cardUseDelay)                        // 傳入卡片使用後的延遲秒數
    {
        this.battleManager = battleManager;        // 儲存 battleManager 參考
        this.player = player;                      // 儲存 player 參考
        this.cardPrefab = cardPrefab;              // 儲存卡片 Prefab
        this.handPanel = handPanel;                // 儲存手牌容器 Transform
        this.deckPile = deckPile;                  // 儲存牌庫 UI Transform
        this.discardPile = discardPile;            // 儲存棄牌堆 UI Transform
        this.energyText = energyText;              // 儲存能量文字
        this.endTurnButton = endTurnButton;        // 儲存結束回合按鈕
        this.cardUseDelay = cardUseDelay;          // 儲存卡片使用延遲秒數
    }

    public void RefreshHandUI(bool playDrawAnimation = false)
    {
        UpdateEnergyUI();                          // 先更新能量顯示（例如 3/3）

        if (deckPile)                              // 如果有指定牌庫 UI 容器
        {
            var t = deckPile.GetComponentInChildren<Text>(); // 往下找一個子物件的 Text（顯示牌庫剩餘張數）
            if (t) t.text = $"{player.deck.Count}";          // 將文字顯示為玩家牌庫的卡片數量
        }
        if (discardPile)                           // 如果有指定棄牌堆 UI 容器
        {
            var t2 = discardPile.GetComponentInChildren<Text>(); // 和牌庫一樣，抓棄牌堆上的 Text
            if (t2) t2.text = $"{player.discardPile.Count}";     // 顯示玩家棄牌堆卡牌數量
        }

        // 先把手牌區域底下原本的所有卡片 UI 清空
        for (int i = handPanel.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(handPanel.GetChild(i).gameObject);     // 將每一個子物件（卡片） Destroy 掉
        }

        List<CardUI> createdCards = new List<CardUI>();           // 用來記錄這次建立出來的 CardUI，之後可能播放抽牌動畫

        foreach (var cardData in player.Hand)                     // 走訪玩家手牌資料列表（CardBase）
        {
            GameObject cardObj = Object.Instantiate(cardPrefab, handPanel);
            // 生成一張新的卡片 UI，作為 handPanel 的子物件

            var cardUI = cardObj.GetComponent<CardUI>();          // 從生成的物件上取得 CardUI 元件
            if (cardUI == null) continue;                         // 若意外沒掛 CardUI，就跳過這張

            cardUI.SetupCard(cardData);                           // 把 CardBase 資料塞進 CardUI，更新圖、名稱、描述等
            cardUI.SetInteractable(!cardInteractionLocked);       // 根據是否鎖互動，決定卡片是否可被點擊/拖曳
            cardUI.ForceResetToHand(handPanel);                   // 強制把卡片重置回手牌位置（避免拉出來後位置錯亂）
            createdCards.Add(cardUI);                             // 記錄這張 CardUI，以便後面播放動畫
        }

        if (handPanel is RectTransform handRect)                  // 若 handPanel 是 RectTransform（UI 容器）
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(handRect);
            // 強制立即重建 Layout，讓卡片依 Layout Group 正確排列

            for (int i = 0; i < createdCards.Count; i++)          // 重新記錄每張卡片的基準座標
            {
                var rt = createdCards[i]?.RectTransform;
                if (rt != null)
                    createdCards[i].OriginalAnchoredPosition = rt.anchoredPosition;
            }
        }

        if (playDrawAnimation)                                    // 如果這次需要播放「抽牌」動畫
        {
            RectTransform deckRect = deckPile as RectTransform;   // 把牌庫 Transform 轉為 RectTransform（動畫起點）
            for (int i = 0; i < createdCards.Count; i++)
                createdCards[i].PlayDrawAnimation(deckRect);      // 叫每一張新卡片從牌庫位置飛入手牌位置
        }
    }

    public void UpdateEnergyUI()
    {
        if (energyText != null && player != null)                 // 確認文字與玩家都有設定
        {
            energyText.text = $"{player.energy}/{player.maxEnergy}";
            // 顯示目前能量/最大能量，例如「2/3」
        }
    }

    public void ApplyInteractableToAllCards(bool value)
    {
        var cards = Object.FindObjectsOfType<CardUI>();           // 在場景中找到所有 CardUI（不只手牌，也可能包含獎勵卡 UI）
        for (int i = 0; i < cards.Length; i++)
            cards[i].SetInteractable(value);                      // 統一設定每張卡是否可互動
    }

    public IEnumerator EnableCardsAfterDelay()
    {
        yield return new WaitForSeconds(cardUseDelay);            // 等待指定秒數（卡片使用後的延遲時間）
        cardInteractionLocked = false;                            // 解鎖卡片互動
        ApplyInteractableToAllCards(true);                        // 讓所有卡片重新可以點擊/拖曳
    }

    public void SetEndTurnButtonInteractable(bool value)
    {
        if (endTurnButton != null)
            endTurnButton.interactable = value;                   // 控制「結束回合」按鈕是否可按
    }

    public void LockCardInteraction()
    {
        cardInteractionLocked = true;                             // 將卡片互動鎖住（配合 IsCardInteractionLocked）
    }
}
