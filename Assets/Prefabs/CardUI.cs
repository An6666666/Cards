using UnityEngine;                       // 使用 Unity 的核心 API
using UnityEngine.UI;                    // 使用 UI 相關 API（Image、Text 等）
using UnityEngine.EventSystems;          // 使用事件系統（滑鼠、拖曳等介面事件）
using DG.Tweening;                       // 使用 DOTween 做補間動畫
using System.Collections;               // 使用 IEnumerator、Coroutine 等集合與協程

[RequireComponent(typeof(CanvasGroup))]  // 要求此物件一定會有 CanvasGroup 元件，沒有就自動加上
public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
     public enum DisplayContext          // 顯示情境的列舉，用來分辨是手牌 UI 還是獎勵 UI
    {
        Hand,                            // 在手牌區顯示
        Reward                           // 在獎勵選牌介面顯示
    }


    [Header("UI 參考")]
    public Image cardImage;              // 卡片顯示的圖片（貼圖）

    [Header("資料參考")]
    public CardBase cardData; // 對應卡片資料的 ScriptableObject  // 這張 UI 卡對應的卡片資料（ScriptableObject）
    public Transform originalParent;     // 卡片最初的父物件（通常是手牌容器）
    private Canvas canvas;     // 用於計算拖曳位移（避免受 Canvas 縮放影響） // 拖曳時使用的 Canvas，避免 scaleFactor 造成位移錯亂
    private RectTransform rectTransform; // 這張卡片的 RectTransform
    private CanvasGroup canvasGroup;     // 控制透明度與 Raycast 判定用的 CanvasGroup
    private Transform canvasRoot;        // Canvas 的根節點，用來在拖曳中暫時把卡片移上來
    private BattleManager battleManager; // 取得戰鬥流程管理器
    private Camera mainCamera;           // 主攝影機參考

    [Header("拖曳外觀")]
    [SerializeField, Range(0f, 1f)]
    private float draggingAlpha = 0.5f;  // 拖曳中卡片的透明度

    private float originalAlpha = 1f;    // 記錄原始透明度

    [Header("滑鼠懸停效果")]
    [SerializeField, Tooltip("滑鼠懸停時卡片向上移動的距離（UI 座標單位）")]
    private float hoverMoveDistance = 20f; // 滑鼠移到卡片上時向上抬起的距離

    [SerializeField, Tooltip("滑鼠懸停時用於顯示的發光圖層（可為額外的 Image 或特效物件）")]
    private Image hoverGlowImage;        // 懸停時的發光圖片（額外圖層）

    [SerializeField, Tooltip("滑鼠懸停時的發光顏色")]
    private Color hoverGlowColor = Color.green; // 發光顏色（只是在懸停時使用）

    private Vector2 originalAnchoredPosition;   // 原始錨點座標（UI 位置）
    private Vector3 originalLocalScale;         // 原始縮放大小

    private bool isDragging;        // 是否正在拖曳
    private bool isHovering;        // 是否滑鼠正在懸停
    private int originalSiblingIndex; // 原始的兄弟排序 index（手牌中的位置）
    private Transform placeholder;  // 手牌中的占位物件，用來保持 layout 位置

    [Header("DOTween 設定")]
    [SerializeField]
    private float hoverMoveDuration = 0.2f; // 懸停位移動畫時間

    [Header("抽牌動畫")]
    [SerializeField, Tooltip("抽牌時從牌庫移動到手牌所花費的時間")]
    private float drawAnimationDuration = 0.35f; // 抽牌從牌庫飛到手牌的動畫時間

    [SerializeField, Tooltip("抽牌時卡片的起始縮放倍數（相對於原始大小）")]
    private float drawStartScale = 0.5f;        // 抽牌時起始縮放比例

    [SerializeField, Tooltip("抽牌移動與縮放所使用的緩動曲線")]
    private Ease drawAnimationEase = Ease.OutCubic; // 抽牌動畫使用的 Ease 曲線

    [SerializeField]
    private float returnMoveDuration = 0.2f;    // 放開滑鼠卡片回到原位的動畫時間

    [SerializeField]
    private Ease hoverMoveEase = Ease.OutQuad;  // 懸停位移用的補間曲線

    [SerializeField]
    private Ease returnMoveEase = Ease.InOutQuad; // 回到手牌位置時的補間曲線

    [SerializeField]
    private float fadeDuration = 0.15f;         // 淡入淡出的動畫時間

    [SerializeField]
    private float hoverGlowFadeDuration = 0.2f; // 懸停發光淡入淡出時間

    [Header("獎勵介面懸停效果")]
    [SerializeField]
    private float rewardHoverScale = 1.05f;     // 在獎勵介面懸停時放大的倍率

    [SerializeField]
    private float rewardHoverDuration = 0.15f;  // 獎勵介面懸停放大動畫時間

    [SerializeField]
    private float rewardReturnDuration = 0.15f; // 獎勵介面縮回原始大小的時間

    [SerializeField]
    private Ease rewardHoverEase = Ease.OutQuad;    // 獎勵介面懸停放大時的 Ease

    [SerializeField]
    private Ease rewardReturnEase = Ease.InOutQuad; // 獎勵介面縮回時的 Ease

    [Header("Layout可選")]
    [SerializeField] private LayoutElement layoutElement; // 供 LayoutGroup 使用，控制排版大小

    [Header("互動權限")]
    [SerializeField] private bool interactable = true; // 是否允許互動（可否被拖曳、點擊等）
    private bool allowDragging = true;                // 是否允許拖曳（可根據顯示情境變動）
    private DisplayContext displayContext = DisplayContext.Hand; // 當前顯示情境，預設在手牌中
    private Tweener positionTween;    // 位置補間動畫的 Tweener
    private Tweener alphaTween;       // 透明度補間動畫的 Tweener
    private Tweener hoverGlowTween;   // 發光淡入淡出的 Tweener
    private Tweener scaleTween;       // 縮放補間動畫的 Tweener
    private bool suppressNextHover;   // 是否忽略下一次 hover（避免剛啟用卡片立即放大）
    private bool isPlayingDrawAnimation; // 是否正在播放抽牌動畫
    private int drawAnimationTweenCount; // 抽牌動畫中的 tween 數量，用來判斷是否全部完成
    private bool allowDraggingBeforeDraw; // 抽牌動畫開始前的拖曳狀態
    private bool blocksRaycastsBeforeDraw; // 抽牌動畫前的 blocksRaycasts 狀態
    private bool interactableBeforeDraw = true; // 抽牌動畫前的互動狀態


    private void OnDisable()
    {
        if (positionTween != null) { positionTween.Kill(); positionTween = null; }   // 停掉位置補間
        if (hoverGlowTween != null) { hoverGlowTween.Kill(); hoverGlowTween = null; } // 停掉發光補間
        if (alphaTween != null) { alphaTween.Kill(); alphaTween = null; }             // 停掉透明度補間
        if (scaleTween != null) { scaleTween.Kill(); scaleTween = null; }             // 停掉縮放補間
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();       // 取得 RectTransform
        canvas = GetComponentInParent<Canvas>();             // 找最近的 Canvas
        if (canvas == null) canvas = FindObjectOfType<Canvas>(); // 若找不到，尋找場景中的任一 Canvas
        canvasRoot = canvas != null ? canvas.transform : null;   // Canvas 根節點
        canvasGroup = GetComponent<CanvasGroup>();           // 取得 CanvasGroup（控制透明與 Raycast）
        battleManager = FindObjectOfType<BattleManager>();   // 尋找戰鬥管理器
        mainCamera = Camera.main;                            // 取得主攝影機
        originalParent = transform.parent;                   // 記錄初始父物件
        originalAnchoredPosition = rectTransform.anchoredPosition; // 記錄初始 UI 錨點位置
        originalLocalScale = rectTransform.localScale;       // 記錄初始縮放大小

        if (canvasGroup != null)
            originalAlpha = canvasGroup.alpha;               // 記錄原始透明度

        if (hoverGlowImage != null)
        {
            var color = hoverGlowColor; color.a = 0f;        // 將 hover 發光顏色的透明度設為 0
            hoverGlowImage.color = color;                    // 套用上去
            hoverGlowImage.gameObject.SetActive(false);      // 預設關閉發光物件
            hoverGlowImage.raycastTarget = false; // 避免透明時攔 UI 射線 // 不讓發光圖層阻擋滑鼠事件
        }

        if (layoutElement == null) layoutElement = GetComponent<LayoutElement>(); // 若 Inspector 未指定，就從自身找
    }

    private void OnEnable()
    {
        suppressNextHover = false; // 啟用時先取消抑制狀態

        // ★ 啟用當下，與 BattleManager 的鎖定旗標同步（保險）
        if (battleManager == null) battleManager = FindObjectOfType<BattleManager>(); // 再次保險抓 BattleManager
        if (battleManager != null) SetInteractable(!battleManager.IsCardInteractionLocked); // 根據是否鎖定卡片互動來決定是否可互動

        if (rectTransform == null) rectTransform = GetComponent<RectTransform>(); // 確保 rectTransform 有值
        if (rectTransform == null) return;
        rectTransform.localScale = originalLocalScale; // 重設縮放回原始大小

        Camera targetCamera = mainCamera != null ? mainCamera : Camera.main; // 確認要用的相機
        if (EventSystem.current != null &&
            RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, targetCamera))
        {
            suppressNextHover = true; // 啟用當幀滑鼠在上方，不觸發 hover  // 若啟用當下滑鼠在卡上，避免立刻觸發放大/懸浮動畫
        }
    }

    private void LateUpdate()
    {
        if (rectTransform == null) return;

        bool isResting = !isDragging && !isHovering && (positionTween == null || !positionTween.IsActive()); // 是否處於靜止（無拖曳、無懸停、無位移動畫）
        if (!isResting) return;

        Vector2 currentPosition = rectTransform.anchoredPosition; // 取得目前錨點位置
        if (currentPosition != originalAnchoredPosition)
            originalAnchoredPosition = currentPosition;           // 若有變動，更新為新的「原始」位置
    }

    /// <summary>設定卡片顯示內容</summary>
    public void SetupCard(CardBase data)
    {
        cardData = data;                                      // 設定卡片資料
        if (cardImage != null && data != null && data.cardImage != null)
            cardImage.sprite = data.cardImage;                // 若有圖片，就把 ScriptableObject 的圖片顯示出來
    }

    #region 拖曳事件
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!interactable || !allowDragging) return;          // 若不能互動或不允許拖曳就直接返回

        if (battleManager == null)
            battleManager = FindObjectOfType<BattleManager>(); // 再次確保拿到 BattleManager
        
        isDragging = true;                                   // 設為拖曳中
        ResetHoverPosition(true);                            // 先把懸停效果收回（避免拖曳時還保持懸浮）

        originalParent = transform.parent;                   // 記錄原始父物件
        originalSiblingIndex = transform.GetSiblingIndex();  // 記錄原始兄弟 index
        originalAnchoredPosition = rectTransform.anchoredPosition; // 記錄原始錨點位置

        CreatePlaceholder();                                 // 在原位置放一個 placeholder，維持 layout

        if (layoutElement != null) layoutElement.ignoreLayout = true; // 暫時脫離 Layout，不受排版影響

        if (canvasRoot != null) transform.SetParent(canvasRoot, true); // 把卡片移到 Canvas 頂層，方便拖曳顯示在最上方

        FadeCardAlpha(draggingAlpha);                        // 拖曳時降低透明度

        if (battleManager != null && cardData != null)
        {
            if (cardData.cardType == CardType.Attack)        // 若是攻擊卡
            {
                battleManager.StartAttackSelect(cardData);   // 進入選擇攻擊目標模式
                battleManager.UpdateAttackHover(GetWorldPosition(eventData)); // 同步目前拖曳點的瞄準狀態
            }
            else if (cardData.cardType == CardType.Movement) // 若是移動卡
                battleManager.UseMovementCard(cardData);     // 立即觸發移動卡的使用流程（進入選格狀態）
        }

        if (canvasGroup != null) canvasGroup.blocksRaycasts = false; // 避免擋住 Drop // 拖曳中暫時不攔截射線，讓 Drop 觸發其他物件
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!interactable || !allowDragging) return;         // 若不能互動或不允許拖曳就直接返回
        float scaleFactor = canvas != null ? canvas.scaleFactor : 1f; // 取 Canvas 縮放比例
        rectTransform.anchoredPosition += eventData.delta / scaleFactor; // 依照滑鼠移動距離更新錨點位置（除以 scaleFactor 修正）

        if (cardData != null && cardData.cardType == CardType.Attack && battleManager != null)
        {
            battleManager.UpdateAttackHover(GetWorldPosition(eventData)); // 拖曳中更新攻擊瞄準高亮
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!interactable || !allowDragging) return;         // 若不能互動或不允許拖曳就直接返回
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true; // 結束拖曳後恢復阻擋 Raycast

        isDragging = false;                                  // 標記拖曳結束

        if (battleManager == null)
            battleManager = FindObjectOfType<BattleManager>();   // 確認 battleManager 存在

        Vector2 worldPos = GetWorldPosition(eventData);          // 將滑鼠座標轉為世界座標

        Collider2D hit = Physics2D.OverlapPoint(worldPos);       // 用世界座標檢查碰到哪個 2D Collider
        bool used = false;                                       // 記錄是否成功用出這張卡

        if (battleManager != null && cardData != null)
        {
            if (cardData.cardType == CardType.Attack)            // 攻擊卡：嘗試打到敵人
                used = HandleAttackDrop(hit);
            else if (cardData.cardType == CardType.Movement)     // 移動卡：嘗試落在可移動的格子
                used = HandleMovementDrop(hit);
            else if (cardData.cardType == CardType.Skill)        // 技能卡：目前邏輯是針對 player 自身
                used = HandleSkillDrop(hit);
        }

        if (used)
        {
            // 成功使用：等一幀刷新 Deck/Discard，再銷毀這張卡的 UI
            StartCoroutine(ConsumeAndRefreshThenDestroy());      // 啟動協程：先刷新 UI，再 Destroy 這張卡 UI

            FadeCardAlpha(originalAlpha, instant: true);         // 立即恢復原本透明度（避免閃一下）
            if (layoutElement != null) layoutElement.ignoreLayout = false; // 回復 Layout 控制
            DestroyPlaceholder();                                // 把 placeholder 刪掉
            return; // 重要：避免繼續往下執行                      // 結束方法，不再執行回手的邏輯
        }
        else
        {
            // 失敗或未命中：卡片回到手牌原位
            ReturnToHand();                                      // 沒用出卡，就回到原手牌位置
        }
    }

    #endregion

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDragging || !interactable) return;             // 正在拖曳或不能互動就不處理 hover

        if (suppressNextHover) { suppressNextHover = false; return; } // 若被標示要忽略下一次 hover，就略過一次

       if (displayContext == DisplayContext.Reward)          // 如果這張卡是在獎勵介面中
        {
            AnimateRewardHover(true);                        // 播放獎勵介面用的懸停放大動畫
        }
        else
        {
            Vector2 targetPosition = originalAnchoredPosition + Vector2.up * hoverMoveDistance; // 算出向上抬起後的位置
            TweenCardPosition(targetPosition, hoverMoveDuration, hoverMoveEase); // 播放位移補間
        }
        isHovering = true;                                   // 設為懸停中
        SetHoverGlowVisible(true);                           // 顯示 hover 發光效果
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDragging) return;                              // 若正在拖曳，不處理滑鼠移出（避免干擾）

        suppressNextHover = false;                           // 移出時重置 suppress 標記
        ResetHoverPosition();                                // 把卡片位置、縮放等 reset 回原本狀態
    }

    private Tweener TweenCardPosition(Vector2 targetPosition, float duration, Ease ease)
    {
        if (rectTransform == null) return null;

        if (positionTween != null) { positionTween.Kill(); positionTween = null; } // 若已有位移 tween，先關掉

        if (duration <= 0f)
        {
            rectTransform.anchoredPosition = targetPosition; // 沒有時間就直接瞬移到目標位置
            return null;
        }

        positionTween = rectTransform
            .DOAnchorPos(targetPosition, duration)          // 使用 DOTween 對 anchoredPosition 做補間
            .SetEase(ease)                                  // 設定補間曲線
            .SetUpdate(true)                                // 即使時間暫停也會更新（忽略 TimeScale）
            .SetLink(gameObject, LinkBehaviour.KillOnDisable) // 此物件 Disable/Destroy 時自動 Kill tween
            .OnKill(() => positionTween = null);            // tween 被 Kill 時，把參考清空
        return positionTween;
    }

    private void ReturnToHand()
    {
        FadeCardAlpha(originalAlpha);                       // 將透明度淡回原狀

        if (placeholder != null)
        {
            var targetParent = placeholder.parent != null ? placeholder.parent : originalParent; // 目標父物件是 placeholder 的父親，否則用 originalParent
            if (targetParent != null)
            {
                transform.SetParent(targetParent, true);    // 卡片重新掛回原來的容器
                transform.SetSiblingIndex(placeholder.GetSiblingIndex()); // 把卡片放回原本的排序位置
            }
            DestroyPlaceholder();                           // 刪除 placeholder
        }
        else if (originalParent != null)
        {
            transform.SetParent(originalParent, true);      // 若沒有 placeholder，就直接掛回原父物件
            transform.SetSiblingIndex(originalSiblingIndex);// 並設定回原索引位置
        }

        originalParent = transform.parent;                  // 更新原始父物件記錄
        originalSiblingIndex = transform.GetSiblingIndex(); // 更新原始排序索引

        Vector2 targetPosition = originalAnchoredPosition;  // 目標位置為原始錨點

        var returnTween = TweenCardPosition(targetPosition, returnMoveDuration, returnMoveEase); // 播放回位置的補間
        if (returnTween != null)
            returnTween.OnComplete(() => originalAnchoredPosition = rectTransform.anchoredPosition); // 動畫完成後，更新原始位置
        else
            originalAnchoredPosition = rectTransform.anchoredPosition; // 若沒 tween，直接更新

        if (layoutElement != null) layoutElement.ignoreLayout = false; // 恢復 Layout 參與

        isDragging = false;                            // 拖曳結束
        isHovering = false;                            // 不再懸停
        SetHoverGlowVisible(false);                    // 關閉發光效果
    }

    private void FadeCardAlpha(float alpha, bool instant = false)
    {
        if (canvasGroup == null) return;               // 沒有 CanvasGroup 就不處理

        if (alphaTween != null) { alphaTween.Kill(); alphaTween = null; } // 若已有透明度 tween，先 Kill

        if (instant || fadeDuration <= 0f)
        {
            canvasGroup.alpha = alpha;                 // 立即套用目標透明度
            return;
        }

        alphaTween = canvasGroup
            .DOFade(alpha, fadeDuration)               // 使用 DOFade 讓 alpha 做補間
            .SetUpdate(true)                           // 忽略 TimeScale
            .SetLink(gameObject, LinkBehaviour.KillOnDisable) // 掛在這個 gameObject 上
            .OnKill(() => alphaTween = null);          // tween 被 Kill 後清除參考
    }

    private void OnDestroy()
    {
        if (positionTween != null) { positionTween.Kill(); positionTween = null; } // 刪除前清理所有 tween
        if (alphaTween != null) { alphaTween.Kill(); alphaTween = null; }
        if (hoverGlowTween != null) { hoverGlowTween.Kill(); hoverGlowTween = null; }
        if (scaleTween != null) { scaleTween.Kill(); scaleTween = null; }
    }

    private void ResetHoverPosition(bool instant = false)
    {
        if (displayContext == DisplayContext.Reward)
        {
            AnimateRewardHover(false, instant);        // 若在獎勵介面，就用縮放方式返回
        }
        else
        {
            TweenCardPosition(originalAnchoredPosition, instant ? 0f : returnMoveDuration, returnMoveEase); // 否則用位移 tween 回原位置
        }
        if (isHovering || instant)
        {
            isHovering = false;                        // 不再懸停
            SetHoverGlowVisible(false, instant);       // 關閉發光
        }
    }

    private void SetHoverGlowVisible(bool visible, bool instant = false)
    {
        if (hoverGlowImage == null) return;            // 沒有設定 hoverGlowImage 就不處理

        if (hoverGlowTween != null) { hoverGlowTween.Kill(); hoverGlowTween = null; } // 關掉原有 tween

        if (visible) hoverGlowImage.gameObject.SetActive(true); // 若要顯示，先確保物件是啟用的

        float targetAlpha = visible ? 1f : 0f;         // 目標透明度，1=發光，0=隱藏

        if (instant || hoverGlowFadeDuration <= 0f)
        {
            var color = hoverGlowImage.color;
            color.a = targetAlpha;                     // 直接改 alpha
            hoverGlowImage.color = color;
            if (!visible) hoverGlowImage.gameObject.SetActive(false); // 若要隱藏就關閉物件
            return;
        }

        hoverGlowTween = hoverGlowImage
            .DOFade(targetAlpha, hoverGlowFadeDuration) // 用 DOFade 做發光淡入淡出
            .SetEase(Ease.OutQuad)                      // 使用 Ease 曲線
            .SetUpdate(true)                            // 忽略 TimeScale
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() => hoverGlowTween = null)
            .OnComplete(() => { if (!visible) hoverGlowImage.gameObject.SetActive(false); }); // 淡出完再關閉物件
    }

    public void SetHoverGlowColor(Color color)
    {
        hoverGlowColor = color;                        // 更新預設的 hover 顏色

        if (hoverGlowImage != null)
        {
            var current = hoverGlowImage.color;        // 保留目前的 alpha
            color.a = current.a;                       // 只更新 RGB，不動 alpha
            hoverGlowImage.color = color;             // 套用新顏色
        }
    }

    private void AnimateRewardHover(bool hover, bool instant = false)
    {
        if (rectTransform == null) return;

        if (scaleTween != null) { scaleTween.Kill(); scaleTween = null; } // 清除原有的縮放 tween

        Vector3 targetScale = hover ? originalLocalScale * rewardHoverScale : originalLocalScale; // 懸停放大 / 回原大小
        float duration = hover ? rewardHoverDuration : rewardReturnDuration;                      // 使用不同動畫時間
        Ease ease = hover ? rewardHoverEase : rewardReturnEase;                                   // 使用不同補間曲線

        if (instant || duration <= 0f)
        {
            rectTransform.localScale = targetScale;   // 立即套用縮放
            return;
        }

        scaleTween = rectTransform
            .DOScale(targetScale, duration)          // 對 localScale 做補間
            .SetEase(ease)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() => scaleTween = null);
    }

    public void SetDisplayContext(DisplayContext context)
    {
        displayContext = context;                    // 設定目前顯示情境（手牌 or 獎勵）
        bool desiredAllowDragging = displayContext == DisplayContext.Hand; // 只有在手牌時才允許拖曳
        if (isPlayingDrawAnimation)
        {
            allowDraggingBeforeDraw = desiredAllowDragging; // 若正在抽牌動畫中，暫存目標狀態
            allowDragging = false;                          // 抽牌期間不允許拖曳
        }
        else
        {
            allowDragging = desiredAllowDragging;           // 直接更新拖曳權限
        }
        ResetHoverPosition(true);                           // 換情境時重設 hover 效果
    }
    
    // 公開 API：回合輪替時一鍵收尾 & 重綁手牌
    public void ForceResetToHand(Transform newHandParent = null)
    {
        positionTween?.Kill(); positionTween = null;        // 清除位移 tween
        hoverGlowTween?.Kill(); hoverGlowTween = null;      // 清除發光 tween
        alphaTween?.Kill(); alphaTween = null;              // 清除透明度 tween

        if (canvasGroup != null)
        {
            canvasGroup.alpha = originalAlpha;              // 還原透明度
            canvasGroup.blocksRaycasts = true;              // 確保可以被點擊 / 擋射線
        }
        SetHoverGlowVisible(false, instant: true);          // 立即關閉 hover 光效
        isDragging = false; isHovering = false;             // 重置拖曳與懸浮狀態

        if (newHandParent != null) originalParent = newHandParent; // 若指定新的手牌容器，就更新 originalParent
        if (originalParent != null) transform.SetParent(originalParent, true); // 把卡片掛回手牌容器
        if (layoutElement != null) layoutElement.ignoreLayout = false;        // 重新讓 Layout 控制它

        DestroyPlaceholder();                               // 刪除 placeholder

        if (rectTransform != null)
            rectTransform.localScale = originalLocalScale; // 重設縮放回原始大小

        rectTransform.anchoredPosition = Vector2.zero;     // 把 anchoredPosition 歸零
        originalAnchoredPosition = Vector2.zero;           // 更新原始位置為零（交給外層 layout 控制）
    }

     public void SetInteractable(bool value)
    {
        if (isPlayingDrawAnimation)                         // 若正在抽牌動畫中
        {
            interactableBeforeDraw = value;                 // 先暫存目標互動狀態

            if (!value)
                interactable = false;                       // 若傳入 false，就立即禁止互動

            return;
        }

        interactable = value;                               // 若不在動畫中，就直接更新
    }

    public void PlayDrawAnimation(RectTransform deckOrigin, float? durationOverride = null, float? startScaleOverride = null, Ease? easeOverride = null)    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();  // 確保 rectTransform 存在

        if (rectTransform == null)
            return;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();        // 確保有 Canvas 參考

        float duration = durationOverride ?? drawAnimationDuration;      // 若有覆寫就用覆寫值，否則用預設
        float startScale = startScaleOverride ?? drawStartScale;        // 抽牌初始縮放
        Ease ease = easeOverride ?? drawAnimationEase;                  // 抽牌補間曲線

        Vector2 targetAnchoredPosition = rectTransform.anchoredPosition; // 目標是目前位置
        Vector3 targetScale = originalLocalScale;                        // 目標縮放是原始大小
        Vector2 startingAnchoredPosition = targetAnchoredPosition;       // 預設起始位置先等於目標

        bool temporarilyIgnoredLayout = false; // 是否暫時忽略 Layout
        bool layoutRestored = false;           // 是否已經還原 Layout 狀態

        if (layoutElement != null && !layoutElement.ignoreLayout)
        {
            layoutElement.ignoreLayout = true; // 抽牌動畫期間不要讓 Layout 改它位置
            temporarilyIgnoredLayout = true;
        }

        if (deckOrigin != null && rectTransform.parent is RectTransform parentRect)
        {
            Vector3 deckWorldCenter = deckOrigin.TransformPoint(deckOrigin.rect.center); // 算出牌堆中心的世界座標
            Camera camera = null;

            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                camera = canvas.worldCamera; // 若是 Camera 模式 Canvas，就用對應的 camera

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    RectTransformUtility.WorldToScreenPoint(camera, deckWorldCenter),
                    camera,
                    out Vector2 localPoint))
            {
                startingAnchoredPosition = localPoint; // 把起點設為牌堆中心對應的 localPosition
            }
        }

        positionTween?.Kill();
        scaleTween?.Kill();                             // 清除原有位置與縮放 tween

        BeginDrawAnimationPhase();                      // 進入抽牌動畫狀態，鎖互動等

        rectTransform.anchoredPosition = startingAnchoredPosition; // 先把卡片放到起始位置
        rectTransform.localScale = targetScale * startScale;       // 縮小到起始比例

        void RestoreLayoutIfNeeded()
        {
            if (layoutRestored)
                return;

            layoutRestored = true;

            rectTransform.anchoredPosition = targetAnchoredPosition; // 動畫完回到目標位置
            rectTransform.localScale = targetScale;                   // 回到原始縮放

            if (temporarilyIgnoredLayout)
            {
                layoutElement.ignoreLayout = false;                   // 還原 Layout 控制

                if (rectTransform.parent is RectTransform parentRect)
                    LayoutRebuilder.MarkLayoutForRebuild(parentRect); // 通知 Unity 重新計算 Layout
            }
        }

       
        if (duration <= 0f)
        {
            RestoreLayoutIfNeeded();               // 若 duration <= 0，直接瞬移到目標
            CompleteDrawAnimationInstantly();      // 直接結束抽牌動畫狀態
            return;
        }

        RegisterDrawAnimationTween();              // 記錄一個位置 tween
        positionTween = rectTransform
            .DOAnchorPos(targetAnchoredPosition, duration)  // 從起點移動到目標
            .SetEase(ease)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() =>
            {
                positionTween = null;
                RestoreLayoutIfNeeded();           // tween 結束或中止時，保證 Layout 有被還原
                OnDrawAnimationTweenTerminated();  // 通知有一個 tween 結束
            });

        RegisterDrawAnimationTween();              // 記錄一個縮放 tween
        scaleTween = rectTransform
            .DOScale(targetScale, duration)        // 縮放到原始大小
            .SetEase(ease)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() =>
            {
                scaleTween = null;
                RestoreLayoutIfNeeded();
                OnDrawAnimationTweenTerminated();
            });
    }

    private void BeginDrawAnimationPhase()
    {
        if (!isPlayingDrawAnimation)
        {
            interactableBeforeDraw = interactable;                 // 記錄開始前的互動狀態
            allowDraggingBeforeDraw = allowDragging;               // 記錄開始前的拖曳權限
            blocksRaycastsBeforeDraw = canvasGroup != null && canvasGroup.blocksRaycasts; // 記錄開始前是否攔截射線

            SetInteractable(false);                                // 抽牌動畫期間暫時不可互動
            allowDragging = false;                                 // 也不能拖曳

            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = false;                // 不攔截射線，避免影響其他 UI

            ResetHoverPosition(true);                              // 收起 hover 效果
            isHovering = false;
            suppressNextHover = true;                              // 動畫完後第一幀忽略 hover
            isDragging = false;
        }

        isPlayingDrawAnimation = true;                             // 標記為抽牌動畫中
        drawAnimationTweenCount = 0;                               // 重設 tween 計數
    }

    private void RegisterDrawAnimationTween()
    {
        drawAnimationTweenCount++;                                 // 註冊一個新的抽牌 tween
    }

    private void OnDrawAnimationTweenTerminated()
    {
        if (!isPlayingDrawAnimation)
            return;

        drawAnimationTweenCount = Mathf.Max(0, drawAnimationTweenCount - 1); // 減少一個 tween 計數
        if (drawAnimationTweenCount > 0)
            return;                                                // 若還有未完成的 tween，就先不結束整個抽牌狀態

        EndDrawAnimationPhase();                                   // 所有 tween 結束時，正式結束抽牌動畫階段
    }

    private void CompleteDrawAnimationInstantly()
    {
        if (!isPlayingDrawAnimation)
            return;

        EndDrawAnimationPhase();                                   // 立即結束抽牌階段
    }

    private void EndDrawAnimationPhase()
    {
        isPlayingDrawAnimation = false;                            // 不再處於抽牌動畫狀態
        drawAnimationTweenCount = 0;                               // tween 計數歸零

        allowDragging = displayContext == DisplayContext.Hand && allowDraggingBeforeDraw; // 若是手牌且原本允許拖曳，就恢復拖曳

        bool shouldBeInteractable = interactableBeforeDraw;        // 預設恢復為動畫前的互動狀態

        if (battleManager != null)
            shouldBeInteractable = interactableBeforeDraw && !battleManager.IsCardInteractionLocked; // 若 BattleManager 有鎖卡，就依它為準

        SetInteractable(shouldBeInteractable);                     // 套用互動狀態

        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = blocksRaycastsBeforeDraw; // 恢復 blocksRaycasts

        suppressNextHover = true;                                  // 結束當幀忽略 hover，避免一結束就觸發懸浮動畫
    }

    private bool HandleAttackDrop(Collider2D hit)
{
    if (hit != null)
    {
        // 從父鏈抓 Enemy：就算命中子節點 Collider 也能找到
        var enemy = hit.GetComponentInParent<Enemy>();             // 往上找最近的 Enemy 元件
        if (enemy != null)
        {
            if (battleManager.OnEnemyClicked(enemy))               // 交給 BattleManager 處理敵人被點擊（可能會判定能否打到）
                return true;                                       // 若回傳 true，代表這張攻擊卡已成功使用
        }
        else
        {
            Debug.LogWarning($"[CardUI] Attack drop hit {hit.name} but no Enemy found in parents."); // 偵錯：打到的物件沒有 Enemy
        }
    }
    battleManager.EndAttackSelect();                               // 沒成功使用卡，結束攻擊選取模式
    return false;                                                  // 回報沒有用出卡
}


    private bool HandleMovementDrop(Collider2D hit)
    {
        if (hit != null)
        {
            BoardTile tile;
            if (hit.TryGetComponent(out tile))                     // 檢查 drop 到的 Collider 是否為 BoardTile
                return battleManager.OnTileClicked(tile);          // 若是，交給 BattleManager 處理點擊格子（移動邏輯）
        }
        battleManager.CancelMovementSelection();                   // 若沒有有效格子，就取消移動選擇狀態
        return false;
    }
    
    private bool HandleSkillDrop(Collider2D hit)
    {
        if (!IsCardPlayableFromHand())                             // 若這張卡目前不在手牌或無法使用，直接 false
            return false;

        if (hit != null)
        {
            Player playerTarget = hit.GetComponentInParent<Player>(); // 往上找 Player，用來確認是否丟到玩家身上
            if (playerTarget != null && playerTarget == battleManager.player)
            {
                return battleManager.PlayCard(cardData);           // 若丟到我方玩家且在手牌中，直接讓 BattleManager 播放這張卡
            }
        }
        return false;                                              // 沒有正確目標就不使用
    }

    private bool IsCardPlayableFromHand()
    {
        if (battleManager == null || cardData == null)
            return false;

        Player playerReference = battleManager.player;             // 從 BattleManager 取得玩家
        if (playerReference == null || playerReference.Hand == null)
            return false;

        return playerReference.Hand.Contains(cardData);            // 檢查這張 cardData 是否存在於玩家手牌中
    }

    private Vector2 GetWorldPosition(PointerEventData eventData)
    {
        Camera targetCamera = mainCamera != null ? mainCamera : Camera.main; // 確認要用的相機
        return targetCamera != null
            ? (Vector2)targetCamera.ScreenToWorldPoint(eventData.position) // 將滑鼠座標轉為世界座標
            : eventData.position;
    }
    
    private void CreatePlaceholder()
    {
        if (placeholder != null || originalParent == null) return; // 若已經有 placeholder 或 parent 是 null，就不用再建

        var placeholderObject = new GameObject($"{name}_Placeholder", typeof(RectTransform)); // 建一個空的 GameObject 當占位
        placeholder = placeholderObject.transform;
        placeholder.SetParent(originalParent, false);              // 掛在原本父物件下
        placeholder.SetSiblingIndex(originalSiblingIndex);         // 放在原本的索引位置
        placeholder.localScale = Vector3.one;                      // 設定縮放為 1

        var placeholderLayoutElement = placeholderObject.AddComponent<LayoutElement>(); // 加上 LayoutElement，讓 LayoutGroup 看到它

        if (layoutElement != null)
        {
            placeholderLayoutElement.preferredWidth = layoutElement.preferredWidth;   // 複製原卡的 Layout 設定
            placeholderLayoutElement.preferredHeight = layoutElement.preferredHeight;
            placeholderLayoutElement.minWidth = layoutElement.minWidth;
            placeholderLayoutElement.minHeight = layoutElement.minHeight;
            placeholderLayoutElement.flexibleWidth = layoutElement.flexibleWidth;
            placeholderLayoutElement.flexibleHeight = layoutElement.flexibleHeight;
        }
        else if (rectTransform != null)
        {
            var rect = rectTransform.rect;                                          // 若沒有 LayoutElement，就用 Rect 大小替代
            placeholderLayoutElement.preferredWidth = rect.width;
            placeholderLayoutElement.preferredHeight = rect.height;
        }
    }

    private void DestroyPlaceholder()
    {
        if (placeholder == null) return;                         // 沒有 placeholder 就不用做事

        if (placeholder.gameObject != null)
            Destroy(placeholder.gameObject);                     // 刪除占位的 GameObject

        placeholder = null;                                      // 清空參考
    }
    // 等到「下一個 frame」再刷新，保證 Player.deck / discardPile 已更新完畢
    // CardUI.cs 內，覆蓋原本的 RefreshDeckDiscardPanelsNextFrame()
    private IEnumerator RefreshDeckDiscardPanelsNextFrame()
{
    Debug.Log("[CardUI] RefreshDeckDiscardPanelsNextFrame start"); // Log：開始刷新 UI（下一幀）

    // 等一幀，讓出牌邏輯完成（手牌→棄牌）
    yield return null;                                           // 先 yield 一幀，確保資料結構（手牌/棄牌）已更新

    // 穩定抓 Player（最多重試 10 幀）
    Player playerRef = null;
    for (int i = 0; i < 10 && playerRef == null; i++)
    {
        if (battleManager == null) battleManager = FindObjectOfType<BattleManager>(); // 嘗試重新取得 battleManager
        if (battleManager != null) playerRef = battleManager.player;                  // 從 battleManager 拿 player
        if (playerRef == null) playerRef = FindObjectOfType<Player>();                // 若還是 null，場景中直接找 Player
        if (playerRef == null) yield return null;                                     // 若還是找不到，就再等一幀繼續找
    }

    Debug.Log("[CardUI] Bus refresh. views=" + DeckUIBus.ViewCount + ", player=" + (playerRef ? "OK" : "NULL")); // 印出目前 View 數量與 player 是否存在
    DeckUIBus.RefreshAll(playerRef);                                                  // 透過 DeckUIBus 刷新所有相關 UI（牌庫/棄牌/手牌）
}



        private IEnumerator ConsumeAndRefreshThenDestroy()
    {
        // 先等一幀，讓手牌→棄牌的資料更新完成
        yield return null;

        // 呼叫刷新流程（走匯流排）
        yield return RefreshDeckDiscardPanelsNextFrame();       // 走剛剛那個協程，等 UI 刷新完成

        // 刷新後再銷毀這張 UI 卡片
        Destroy(gameObject);                                    // 最後刪掉這張 CardUI 物件（畫面上不再顯示）
    }
}
