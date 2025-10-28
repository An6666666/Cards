using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
     public enum DisplayContext
    {
        Hand,
        Reward
    }


    [Header("UI 參考")]
    public Image cardImage;

    [Header("資料參考")]
    public CardBase cardData; // 對應卡片資料的 ScriptableObject
    public Transform originalParent;
    private Canvas canvas;     // 用於計算拖曳位移（避免受 Canvas 縮放影響）
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform canvasRoot;
    private BattleManager battleManager;
    private Camera mainCamera;

    [Header("拖曳外觀")]
    [SerializeField, Range(0f, 1f)]
    private float draggingAlpha = 0.5f;

    private float originalAlpha = 1f;

    [Header("滑鼠懸停效果")]
    [SerializeField, Tooltip("滑鼠懸停時卡片向上移動的距離（UI 座標單位）")]
    private float hoverMoveDistance = 20f;

    [SerializeField, Tooltip("滑鼠懸停時用於顯示的發光圖層（可為額外的 Image 或特效物件）")]
    private Image hoverGlowImage;

    [SerializeField, Tooltip("滑鼠懸停時的發光顏色")]
    private Color hoverGlowColor = Color.green;

    private Vector2 originalAnchoredPosition;
    private Vector3 originalLocalScale;

    private bool isDragging;
    private bool isHovering;
    private int originalSiblingIndex;
    private Transform placeholder;

    [Header("DOTween 設定")]
    [SerializeField]
    private float hoverMoveDuration = 0.2f;

    [Header("抽牌動畫")]
    [SerializeField, Tooltip("抽牌時從牌庫移動到手牌所花費的時間")]
    private float drawAnimationDuration = 0.35f;

    [SerializeField, Tooltip("抽牌時卡片的起始縮放倍數（相對於原始大小）")]
    private float drawStartScale = 0.5f;

    [SerializeField, Tooltip("抽牌移動與縮放所使用的緩動曲線")]
    private Ease drawAnimationEase = Ease.OutCubic;

    [SerializeField]
    private float returnMoveDuration = 0.2f;

    [SerializeField]
    private Ease hoverMoveEase = Ease.OutQuad;

    [SerializeField]
    private Ease returnMoveEase = Ease.InOutQuad;

    [SerializeField]
    private float fadeDuration = 0.15f;

    [SerializeField]
    private float hoverGlowFadeDuration = 0.2f;

    [Header("獎勵介面懸停效果")]
    [SerializeField]
    private float rewardHoverScale = 1.05f;

    [SerializeField]
    private float rewardHoverDuration = 0.15f;

    [SerializeField]
    private float rewardReturnDuration = 0.15f;

    [SerializeField]
    private Ease rewardHoverEase = Ease.OutQuad;

    [SerializeField]
    private Ease rewardReturnEase = Ease.InOutQuad;

    [Header("Layout可選")]
    [SerializeField] private LayoutElement layoutElement;

    [Header("互動權限")]
    [SerializeField] private bool interactable = true;
    private bool allowDragging = true;
    private DisplayContext displayContext = DisplayContext.Hand;
    private Tweener positionTween;
    private Tweener alphaTween;
    private Tweener hoverGlowTween;
    private Tweener scaleTween;
    private bool suppressNextHover;
    private bool isPlayingDrawAnimation;
    private int drawAnimationTweenCount;
    private bool allowDraggingBeforeDraw;
    private bool blocksRaycastsBeforeDraw;
    private bool interactableBeforeDraw = true;


    private void OnDisable()
    {
        if (positionTween != null) { positionTween.Kill(); positionTween = null; }
        if (hoverGlowTween != null) { hoverGlowTween.Kill(); hoverGlowTween = null; }
        if (alphaTween != null) { alphaTween.Kill(); alphaTween = null; }
        if (scaleTween != null) { scaleTween.Kill(); scaleTween = null; }
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        canvasRoot = canvas != null ? canvas.transform : null;
        canvasGroup = GetComponent<CanvasGroup>();
        battleManager = FindObjectOfType<BattleManager>();
        mainCamera = Camera.main;
        originalParent = transform.parent;
        originalAnchoredPosition = rectTransform.anchoredPosition;
        originalLocalScale = rectTransform.localScale;

        if (canvasGroup != null)
            originalAlpha = canvasGroup.alpha;

        if (hoverGlowImage != null)
        {
            var color = hoverGlowColor; color.a = 0f;
            hoverGlowImage.color = color;
            hoverGlowImage.gameObject.SetActive(false);
            hoverGlowImage.raycastTarget = false; // 避免透明時攔 UI 射線
        }

        if (layoutElement == null) layoutElement = GetComponent<LayoutElement>();
    }

    private void OnEnable()
    {
        suppressNextHover = false;

        // ★ 啟用當下，與 BattleManager 的鎖定旗標同步（保險）
        if (battleManager == null) battleManager = FindObjectOfType<BattleManager>();
        if (battleManager != null) SetInteractable(!battleManager.IsCardInteractionLocked);

        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null) return;
        rectTransform.localScale = originalLocalScale;

        Camera targetCamera = mainCamera != null ? mainCamera : Camera.main;
        if (EventSystem.current != null &&
            RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, targetCamera))
        {
            suppressNextHover = true; // 啟用當幀滑鼠在上方，不觸發 hover
        }
    }

    private void LateUpdate()
    {
        if (rectTransform == null) return;

        bool isResting = !isDragging && !isHovering && (positionTween == null || !positionTween.IsActive());
        if (!isResting) return;

        Vector2 currentPosition = rectTransform.anchoredPosition;
        if (currentPosition != originalAnchoredPosition)
            originalAnchoredPosition = currentPosition;
    }

    /// <summary>設定卡片顯示內容</summary>
    public void SetupCard(CardBase data)
    {
        cardData = data;
        if (cardImage != null && data != null && data.cardImage != null)
            cardImage.sprite = data.cardImage;
    }

    #region 拖曳事件
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!interactable || !allowDragging) return;

        if (battleManager == null)
            battleManager = FindObjectOfType<BattleManager>();
        
        isDragging = true;
        ResetHoverPosition(true);

        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalAnchoredPosition = rectTransform.anchoredPosition;

        CreatePlaceholder();


        if (layoutElement != null) layoutElement.ignoreLayout = true; // 暫時脫離 Layout

        if (canvasRoot != null) transform.SetParent(canvasRoot, true);

        FadeCardAlpha(draggingAlpha);

        if (battleManager != null && cardData != null)
        {
            if (cardData.cardType == CardType.Attack)
                battleManager.StartAttackSelect(cardData);
            else if (cardData.cardType == CardType.Movement)
                battleManager.UseMovementCard(cardData);
        }

        if (canvasGroup != null) canvasGroup.blocksRaycasts = false; // 避免擋住 Drop
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!interactable || !allowDragging) return;
        float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
        rectTransform.anchoredPosition += eventData.delta / scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
{
    if (!interactable || !allowDragging) return;
    if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

    isDragging = false;

    if (battleManager == null)
        battleManager = FindObjectOfType<BattleManager>();

    Camera targetCamera = mainCamera != null ? mainCamera : Camera.main;
    Vector2 worldPos = targetCamera != null
        ? targetCamera.ScreenToWorldPoint(eventData.position)
        : eventData.position;

    Collider2D hit = Physics2D.OverlapPoint(worldPos);
    bool used = false;

    if (battleManager != null && cardData != null)
    {
        if (cardData.cardType == CardType.Attack)
            used = HandleAttackDrop(hit);
        else if (cardData.cardType == CardType.Movement)
            used = HandleMovementDrop(hit);
        else if (cardData.cardType == CardType.Skill)
            used = HandleSkillDrop(hit);
    }

    if (used)
    {
        // 成功使用：等一幀刷新 Deck/Discard，再銷毀這張卡的 UI
        StartCoroutine(ConsumeAndRefreshThenDestroy());

        FadeCardAlpha(originalAlpha, instant: true);
        if (layoutElement != null) layoutElement.ignoreLayout = false;
        DestroyPlaceholder();
        return; // 重要：避免繼續往下執行
    }
    else
    {
        // 失敗或未命中：卡片回到手牌原位
        ReturnToHand();
    }
}

    #endregion

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDragging || !interactable) return;

        if (suppressNextHover) { suppressNextHover = false; return; }

       if (displayContext == DisplayContext.Reward)
        {
            AnimateRewardHover(true);
        }
        else
        {
            Vector2 targetPosition = originalAnchoredPosition + Vector2.up * hoverMoveDistance;
            TweenCardPosition(targetPosition, hoverMoveDuration, hoverMoveEase);
        }
        isHovering = true;
        SetHoverGlowVisible(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDragging) return;

        suppressNextHover = false;
        ResetHoverPosition();
    }

    private Tweener TweenCardPosition(Vector2 targetPosition, float duration, Ease ease)
    {
        if (rectTransform == null) return null;

        if (positionTween != null) { positionTween.Kill(); positionTween = null; }

        if (duration <= 0f)
        {
            rectTransform.anchoredPosition = targetPosition;
            return null;
        }

        positionTween = rectTransform
            .DOAnchorPos(targetPosition, duration)
            .SetEase(ease)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() => positionTween = null);
        return positionTween;
    }

    private void ReturnToHand()
    {
        FadeCardAlpha(originalAlpha);

        if (placeholder != null)
        {
            var targetParent = placeholder.parent != null ? placeholder.parent : originalParent;
            if (targetParent != null)
            {
                transform.SetParent(targetParent, true);
                transform.SetSiblingIndex(placeholder.GetSiblingIndex());
            }
            DestroyPlaceholder();
        }
        else if (originalParent != null)
        {
            transform.SetParent(originalParent, true);
            transform.SetSiblingIndex(originalSiblingIndex);
        }

        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        Vector2 targetPosition = originalAnchoredPosition;

        var returnTween = TweenCardPosition(targetPosition, returnMoveDuration, returnMoveEase);
        if (returnTween != null)
            returnTween.OnComplete(() => originalAnchoredPosition = rectTransform.anchoredPosition);
        else
            originalAnchoredPosition = rectTransform.anchoredPosition;

        if (layoutElement != null) layoutElement.ignoreLayout = false;

        isDragging = false;
        isHovering = false;
        SetHoverGlowVisible(false);
    }

    private void FadeCardAlpha(float alpha, bool instant = false)
    {
        if (canvasGroup == null) return;

        if (alphaTween != null) { alphaTween.Kill(); alphaTween = null; }

        if (instant || fadeDuration <= 0f)
        {
            canvasGroup.alpha = alpha;
            return;
        }

        alphaTween = canvasGroup
            .DOFade(alpha, fadeDuration)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() => alphaTween = null);
    }

    private void OnDestroy()
    {
        if (positionTween != null) { positionTween.Kill(); positionTween = null; }
        if (alphaTween != null) { alphaTween.Kill(); alphaTween = null; }
        if (hoverGlowTween != null) { hoverGlowTween.Kill(); hoverGlowTween = null; }
        if (scaleTween != null) { scaleTween.Kill(); scaleTween = null; }
    }

    private void ResetHoverPosition(bool instant = false)
    {
        if (displayContext == DisplayContext.Reward)
        {
            AnimateRewardHover(false, instant);
        }
        else
        {
            TweenCardPosition(originalAnchoredPosition, instant ? 0f : returnMoveDuration, returnMoveEase);
        }
        if (isHovering || instant)
        {
            isHovering = false;
            SetHoverGlowVisible(false, instant);
        }
    }

    private void SetHoverGlowVisible(bool visible, bool instant = false)
    {
        if (hoverGlowImage == null) return;

        if (hoverGlowTween != null) { hoverGlowTween.Kill(); hoverGlowTween = null; }

        if (visible) hoverGlowImage.gameObject.SetActive(true);

        float targetAlpha = visible ? 1f : 0f;

        if (instant || hoverGlowFadeDuration <= 0f)
        {
            var color = hoverGlowImage.color;
            color.a = targetAlpha;
            hoverGlowImage.color = color;
            if (!visible) hoverGlowImage.gameObject.SetActive(false);
            return;
        }

        hoverGlowTween = hoverGlowImage
            .DOFade(targetAlpha, hoverGlowFadeDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() => hoverGlowTween = null)
            .OnComplete(() => { if (!visible) hoverGlowImage.gameObject.SetActive(false); });
    }

    public void SetHoverGlowColor(Color color)
    {
        hoverGlowColor = color;

        if (hoverGlowImage != null)
        {
            var current = hoverGlowImage.color;
            color.a = current.a;
            hoverGlowImage.color = color;
        }
    }

    private void AnimateRewardHover(bool hover, bool instant = false)
    {
        if (rectTransform == null) return;

        if (scaleTween != null) { scaleTween.Kill(); scaleTween = null; }

        Vector3 targetScale = hover ? originalLocalScale * rewardHoverScale : originalLocalScale;
        float duration = hover ? rewardHoverDuration : rewardReturnDuration;
        Ease ease = hover ? rewardHoverEase : rewardReturnEase;

        if (instant || duration <= 0f)
        {
            rectTransform.localScale = targetScale;
            return;
        }

        scaleTween = rectTransform
            .DOScale(targetScale, duration)
            .SetEase(ease)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() => scaleTween = null);
    }

    public void SetDisplayContext(DisplayContext context)
    {
        displayContext = context;
        bool desiredAllowDragging = displayContext == DisplayContext.Hand;
        if (isPlayingDrawAnimation)
        {
            allowDraggingBeforeDraw = desiredAllowDragging;
            allowDragging = false;
        }
        else
        {
            allowDragging = desiredAllowDragging;
        }
        ResetHoverPosition(true);
    }
    
    // 公開 API：回合輪替時一鍵收尾 & 重綁手牌
    public void ForceResetToHand(Transform newHandParent = null)
    {
        positionTween?.Kill(); positionTween = null;
        hoverGlowTween?.Kill(); hoverGlowTween = null;
        alphaTween?.Kill(); alphaTween = null;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = originalAlpha;
            canvasGroup.blocksRaycasts = true;
        }
        SetHoverGlowVisible(false, instant: true);
        isDragging = false; isHovering = false;

        if (newHandParent != null) originalParent = newHandParent;
        if (originalParent != null) transform.SetParent(originalParent, true);
        if (layoutElement != null) layoutElement.ignoreLayout = false;

        DestroyPlaceholder();

        if (rectTransform != null)
            rectTransform.localScale = originalLocalScale;

        rectTransform.anchoredPosition = Vector2.zero;
        originalAnchoredPosition = Vector2.zero;
    }

     public void SetInteractable(bool value)
    {
        if (isPlayingDrawAnimation)
        {
            interactableBeforeDraw = value;

            if (!value)
                interactable = false;

            return;
        }

        interactable = value;
    }

    public void PlayDrawAnimation(RectTransform deckOrigin, float? durationOverride = null, float? startScaleOverride = null, Ease? easeOverride = null)    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (rectTransform == null)
            return;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        float duration = durationOverride ?? drawAnimationDuration;
        float startScale = startScaleOverride ?? drawStartScale;
        Ease ease = easeOverride ?? drawAnimationEase;

        Vector2 targetAnchoredPosition = rectTransform.anchoredPosition;
        Vector3 targetScale = originalLocalScale;
        Vector2 startingAnchoredPosition = targetAnchoredPosition;

        bool temporarilyIgnoredLayout = false;
        bool layoutRestored = false;

        if (layoutElement != null && !layoutElement.ignoreLayout)
        {
            layoutElement.ignoreLayout = true;
            temporarilyIgnoredLayout = true;
        }

        if (deckOrigin != null && rectTransform.parent is RectTransform parentRect)
        {
            Vector3 deckWorldCenter = deckOrigin.TransformPoint(deckOrigin.rect.center);
            Camera camera = null;

            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                camera = canvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    RectTransformUtility.WorldToScreenPoint(camera, deckWorldCenter),
                    camera,
                    out Vector2 localPoint))
            {
                startingAnchoredPosition = localPoint;
            }
        }

        positionTween?.Kill();
        scaleTween?.Kill();

        BeginDrawAnimationPhase();

        rectTransform.anchoredPosition = startingAnchoredPosition;
        rectTransform.localScale = targetScale * startScale;

        void RestoreLayoutIfNeeded()
        {
            if (layoutRestored)
                return;

            layoutRestored = true;

            rectTransform.anchoredPosition = targetAnchoredPosition;
            rectTransform.localScale = targetScale;

            if (temporarilyIgnoredLayout)
            {
                layoutElement.ignoreLayout = false;

                if (rectTransform.parent is RectTransform parentRect)
                    LayoutRebuilder.MarkLayoutForRebuild(parentRect);
            }
        }

       
        if (duration <= 0f)
        {
            RestoreLayoutIfNeeded();
            CompleteDrawAnimationInstantly();
            return;
        }

        RegisterDrawAnimationTween();
        positionTween = rectTransform
            .DOAnchorPos(targetAnchoredPosition, duration)
            .SetEase(ease)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() =>
            {
                positionTween = null;
                RestoreLayoutIfNeeded();
                OnDrawAnimationTweenTerminated();
            });

        RegisterDrawAnimationTween();
        scaleTween = rectTransform
            .DOScale(targetScale, duration)
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
            interactableBeforeDraw = interactable;
            allowDraggingBeforeDraw = allowDragging;
            blocksRaycastsBeforeDraw = canvasGroup != null && canvasGroup.blocksRaycasts;

            SetInteractable(false);
            allowDragging = false;

            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = false;

            ResetHoverPosition(true);
            isHovering = false;
            suppressNextHover = true;
            isDragging = false;
        }

        isPlayingDrawAnimation = true;
        drawAnimationTweenCount = 0;
    }

    private void RegisterDrawAnimationTween()
    {
        drawAnimationTweenCount++;
    }

    private void OnDrawAnimationTweenTerminated()
    {
        if (!isPlayingDrawAnimation)
            return;

        drawAnimationTweenCount = Mathf.Max(0, drawAnimationTweenCount - 1);
        if (drawAnimationTweenCount > 0)
            return;

        EndDrawAnimationPhase();
    }

    private void CompleteDrawAnimationInstantly()
    {
        if (!isPlayingDrawAnimation)
            return;

        EndDrawAnimationPhase();
    }

    private void EndDrawAnimationPhase()
    {
        isPlayingDrawAnimation = false;
        drawAnimationTweenCount = 0;

        allowDragging = displayContext == DisplayContext.Hand && allowDraggingBeforeDraw;

        bool shouldBeInteractable = interactableBeforeDraw;

        if (battleManager != null)
            shouldBeInteractable = interactableBeforeDraw && !battleManager.IsCardInteractionLocked;

        SetInteractable(shouldBeInteractable);

        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = blocksRaycastsBeforeDraw;

        suppressNextHover = true;
    }

    private bool HandleAttackDrop(Collider2D hit)
{
    if (hit != null)
    {
        // 從父鏈抓 Enemy：就算命中子節點 Collider 也能找到
        var enemy = hit.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            if (battleManager.OnEnemyClicked(enemy))
                return true;
        }
        else
        {
            Debug.LogWarning($"[CardUI] Attack drop hit {hit.name} but no Enemy found in parents.");
        }
    }
    battleManager.EndAttackSelect();
    return false;
}


    private bool HandleMovementDrop(Collider2D hit)
    {
        if (hit != null)
        {
            BoardTile tile;
            if (hit.TryGetComponent(out tile))
                return battleManager.OnTileClicked(tile);
        }
        battleManager.CancelMovementSelection();
        return false;
    }
    
    private bool HandleSkillDrop(Collider2D hit)
    {
        if (!IsCardPlayableFromHand())
            return false;

        if (hit != null)
        {
            Player playerTarget = hit.GetComponentInParent<Player>();
            if (playerTarget != null && playerTarget == battleManager.player)
            {
                return battleManager.PlayCard(cardData);
            }
        }
        return false;
    }

    private bool IsCardPlayableFromHand()
    {
        if (battleManager == null || cardData == null)
            return false;

        Player playerReference = battleManager.player;
        if (playerReference == null || playerReference.Hand == null)
            return false;

        return playerReference.Hand.Contains(cardData);
    }

     private void CreatePlaceholder()
    {
        if (placeholder != null || originalParent == null) return;

        var placeholderObject = new GameObject($"{name}_Placeholder", typeof(RectTransform));
        placeholder = placeholderObject.transform;
        placeholder.SetParent(originalParent, false);
        placeholder.SetSiblingIndex(originalSiblingIndex);
        placeholder.localScale = Vector3.one;

        var placeholderLayoutElement = placeholderObject.AddComponent<LayoutElement>();

        if (layoutElement != null)
        {
            placeholderLayoutElement.preferredWidth = layoutElement.preferredWidth;
            placeholderLayoutElement.preferredHeight = layoutElement.preferredHeight;
            placeholderLayoutElement.minWidth = layoutElement.minWidth;
            placeholderLayoutElement.minHeight = layoutElement.minHeight;
            placeholderLayoutElement.flexibleWidth = layoutElement.flexibleWidth;
            placeholderLayoutElement.flexibleHeight = layoutElement.flexibleHeight;
        }
        else if (rectTransform != null)
        {
            var rect = rectTransform.rect;
            placeholderLayoutElement.preferredWidth = rect.width;
            placeholderLayoutElement.preferredHeight = rect.height;
        }
    }

    private void DestroyPlaceholder()
    {
        if (placeholder == null) return;

        if (placeholder.gameObject != null)
            Destroy(placeholder.gameObject);

        placeholder = null;
    }
    // 等到「下一個 frame」再刷新，保證 Player.deck / discardPile 已更新完畢
    // CardUI.cs 內，覆蓋原本的 RefreshDeckDiscardPanelsNextFrame()
    private IEnumerator RefreshDeckDiscardPanelsNextFrame()
{
    Debug.Log("[CardUI] RefreshDeckDiscardPanelsNextFrame start");

    // 等一幀，讓出牌邏輯完成（手牌→棄牌）
    yield return null;

    // 穩定抓 Player（最多重試 10 幀）
    Player playerRef = null;
    for (int i = 0; i < 10 && playerRef == null; i++)
    {
        if (battleManager == null) battleManager = FindObjectOfType<BattleManager>();
        if (battleManager != null) playerRef = battleManager.player;
        if (playerRef == null) playerRef = FindObjectOfType<Player>();
        if (playerRef == null) yield return null;
    }

    Debug.Log("[CardUI] Bus refresh. views=" + DeckUIBus.ViewCount + ", player=" + (playerRef ? "OK" : "NULL"));
    DeckUIBus.RefreshAll(playerRef);
}



        private IEnumerator ConsumeAndRefreshThenDestroy()
    {
        // 先等一幀，讓手牌→棄牌的資料更新完成
        yield return null;

        // 呼叫刷新流程（走匯流排）
        yield return RefreshDeckDiscardPanelsNextFrame();

        // 刷新後再銷毀這張 UI 卡片
        Destroy(gameObject);
    }
}
