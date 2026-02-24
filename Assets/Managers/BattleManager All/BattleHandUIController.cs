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
        UpdateDeckDiscardUI();                     // 更新牌庫/棄牌堆顯示
        SyncHandUI(playDrawAnimation);             // 用 diff 方式同步手牌 UI
    }

    public void UpdateEnergyUI()
    {
        if (energyText != null && player != null)                 // 確認文字與玩家都有設定
        {
            energyText.text = $"{player.energy}/{player.maxEnergy}";
            // 顯示目前能量/最大能量，例如「2/3」
        }
    }
    
     public void UpdateHandMetaUI()
    {
        UpdateEnergyUI();
        UpdateDeckDiscardUI();
    }

    public void HandleCardUsedUI(CardUI usedUI)
    {
        if (usedUI == null || handPanel == null) return;
        Object.Destroy(usedUI.gameObject);

        if (handPanel is RectTransform handRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(handRect);
    }

    public void SyncHandUI(bool playDrawAnimation = false)
    {
        if (handPanel == null || player == null) return;

        List<CardUI> cardsWithMissingData = new List<CardUI>();
        Dictionary<CardBase, Queue<CardUI>> existingByCard = new Dictionary<CardBase, Queue<CardUI>>();

        for (int i = 0; i < handPanel.childCount; i++)
        {
            var child = handPanel.GetChild(i);
            var cardUI = child.GetComponent<CardUI>();
            if (cardUI == null) continue;

            if (cardUI.cardData == null)
            {
                cardsWithMissingData.Add(cardUI);
                continue;
            }

            if (!existingByCard.TryGetValue(cardUI.cardData, out var queue))
            {
                queue = new Queue<CardUI>();
                existingByCard.Add(cardUI.cardData, queue);
            }

            queue.Enqueue(cardUI);
        }

        List<CardUI> createdCards = new List<CardUI>();

        foreach (var cardData in player.Hand)
        {
            if (existingByCard.TryGetValue(cardData, out var queue) && queue.Count > 0)
            {
                var existing = queue.Dequeue();
                existing.SetInteractable(!cardInteractionLocked); // 只更新互動狀態，不重設位置
                continue;
            }

            GameObject cardObj = Object.Instantiate(cardPrefab, handPanel);
            var cardUI = cardObj.GetComponent<CardUI>();
            if (cardUI == null)
            {
                Object.Destroy(cardObj);
                continue;
            }

            cardUI.SetupCard(cardData);
            cardUI.SetInteractable(!cardInteractionLocked);
            cardUI.ForceResetToHand(handPanel);                   // 僅新生成卡片重置一次
            createdCards.Add(cardUI);
        }
        foreach (var pair in existingByCard)
        {
            while (pair.Value.Count > 0)
            {
                var extraCardUI = pair.Value.Dequeue();
                DetachCardUI(extraCardUI);
                Object.Destroy(extraCardUI.gameObject);           // 刪除多餘卡牌 UI
            }
        }

        for (int i = 0; i < cardsWithMissingData.Count; i++)
        {
            var cardUI = cardsWithMissingData[i];
            DetachCardUI(cardUI);
            Object.Destroy(cardUI.gameObject);
        }
        if (handPanel is RectTransform handRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(handRect);

            for (int i = 0; i < handPanel.childCount; i++)
            {
                var cardUI = handPanel.GetChild(i).GetComponent<CardUI>();
                var rt = cardUI != null ? cardUI.VisualRect : null;
                if (cardUI == null || rt == null) continue;

                var animation = cardUI.GetComponent<CardAnimationController>();
                if (animation != null && animation.IsPlayingDrawAnimation) continue;

                var drag = cardUI.GetComponent<CardDragHandler>();
                if (drag != null && drag.IsDragging) continue;

                cardUI.OriginalAnchoredPosition = rt.anchoredPosition;
            }
        }

        if (playDrawAnimation)
        {
            RectTransform deckRect = deckPile as RectTransform;
            for (int i = 0; i < createdCards.Count; i++)
                createdCards[i].PlayDrawAnimation(deckRect);
        }
    }

    public void UpdateDeckDiscardUI()
    {
        if (deckPile)
        {
            var t = deckPile.GetComponentInChildren<Text>();
            if (t) t.text = $"{player.deck.Count}";
        }

        if (discardPile)
        {
            var t2 = discardPile.GetComponentInChildren<Text>();
            if (t2) t2.text = $"{player.discardPile.Count}";
        }
    }
    private void DetachCardUI(CardUI cardUI)
    {
        if (cardUI == null) return;
        if (cardUI.LayoutElement != null) cardUI.LayoutElement.ignoreLayout = true;
        cardUI.transform.SetParent(null, false);
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
