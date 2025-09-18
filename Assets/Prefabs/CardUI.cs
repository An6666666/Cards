using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI 參考")]
    public Text cardNameText;
    public Text costText;
    public Text descriptionText;
    public Image cardImage;
    public Image cardBackground;

    [Header("資料參考")]
    public CardBase cardData; // 對應卡片資料的 ScriptableObject
    public Transform originalParent;
    private Canvas canvas;     // 用於計算拖曳位移（避免受 Canvas 縮放影響）
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    [Header("拖曳外觀")]
    [SerializeField, Range(0f, 1f)]
    private float draggingAlpha = 0.5f;

    private float originalAlpha = 1f;

    [Header("滑鼠懸停效果")]
    [SerializeField, Tooltip("滑鼠懸停時卡片向上移動的距離（UI 座標單位）")]
    private float hoverMoveDistance = 20f;
    [SerializeField, Tooltip("滑鼠懸停時的發光顏色")]
    private Color hoverGlowColor = new Color(1f, 1f, 1f, 0.6f);

    [SerializeField, Tooltip("滑鼠懸停時發光外框的粗細（像素）")]
    private float hoverGlowPadding = 16f;

    private Vector2 originalAnchoredPosition;
    private bool isDragging;
    private bool isHovering;

    private Outline hoverGlowOutline;
    private Color originalGlowColor;
    private Vector2 originalGlowDistance;
    private bool originalGlowEnabled;
    private bool originalGlowUseGraphicAlpha;


    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = FindObjectOfType<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        originalAnchoredPosition = rectTransform.anchoredPosition;

        if (canvasGroup != null)
        {
            originalAlpha = canvasGroup.alpha;
        }

        SetupHoverGlow();
    }

    /// <summary>
    /// 設定卡片顯示內容
    /// </summary>
    public void SetupCard(CardBase data)
    {
        cardData = data;
        if (cardNameText) cardNameText.text = data.cardName;
        if (costText) costText.text = data.cost.ToString();
        if (descriptionText) descriptionText.text = data.description;
        if (cardImage && data.cardImage) cardImage.sprite = data.cardImage;

        // 並依據 cardType 設定背景顏色
        switch (data.cardType)
        {
            case CardType.Attack:
                if (cardBackground) cardBackground.color = Color.red;
                break;
            case CardType.Skill:
                if (cardBackground) cardBackground.color = Color.blue;
                break;
            case CardType.Movement:
                if (cardBackground) cardBackground.color = Color.green;
                break;
            case CardType.Relic:
                if (cardBackground) cardBackground.color = Color.yellow;
                break;
        }
    }

    #region 拖曳事件
    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        ResetHoverPosition();
        DisableHoverGlow();

        originalParent = transform.parent;
        transform.SetParent(FindObjectOfType<Canvas>().transform);

        SetCardAlpha(draggingAlpha);


        BattleManager bm = FindObjectOfType<BattleManager>();
        if (bm != null)
        {
            if (cardData.cardType == CardType.Attack)
            {
                bm.StartAttackSelect(cardData);
            }
            else if (cardData.cardType == CardType.Movement)
            {
                bm.UseMovementCard(cardData);
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 以 Canvas 的 scaleFactor 校正拖曳位移，避免縮放造成位移過大/過小
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        SetCardAlpha(originalAlpha);

        BattleManager bm = FindObjectOfType<BattleManager>();
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(eventData.position);
        Collider2D hit = Physics2D.OverlapPoint(worldPos);
        bool used = false;

        if (bm != null)
        {
            if (cardData.cardType == CardType.Attack)
            {
                // 攻擊牌：若拖放到 Enemy 上就觸發使用，否則結束選取
                if (hit != null)
                {
                    Enemy e = hit.GetComponent<Enemy>();
                    if (e != null)
                    {
                        used = bm.OnEnemyClicked(e);
                    }
                }
                if (!used)
                {
                    bm.EndAttackSelect();
                }
            }
            else if (cardData.cardType == CardType.Movement)
            {
                // 移動牌：若拖放到 BoardTile 上就觸發使用，否則取消移動選取
                if (hit != null)
                {
                    BoardTile tile = hit.GetComponent<BoardTile>();
                    if (tile != null)
                    {
                        used = bm.OnTileClicked(tile);
                    }
                }
                if (!used)
                {
                    bm.CancelMovementSelection();
                }
            }
        }

        // 使用成功就把這張卡片的 UI 移除；未使用則回到手牌
        if (used)
        {
            Destroy(gameObject);
        }
        else
        {
            ReturnToHand();
        }
    }
    #endregion

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDragging)
        {
            return;
        }

        originalAnchoredPosition = rectTransform.anchoredPosition;
        rectTransform.anchoredPosition = originalAnchoredPosition + Vector2.up * hoverMoveDistance;
        isHovering = true;

        EnableHoverGlow();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDragging)
        {
            return;
        }

        ResetHoverPosition();
        DisableHoverGlow();
    }

    // 攻擊牌：若拖到 Enemy 上會觸發使用；否則還原到手牌
    // （此備註對應上方 OnEndDrag 的行為說明）

    // 移動牌：不需要刻意拖回手牌，會直接進入可選移動格流程
    // 若偵測到是 HandPanel，則直接復位到手牌
    private void HandleMovementCard(PointerEventData eventData)
    {
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(eventData.position);
        Collider2D hit = Physics2D.OverlapPoint(worldPos);

        // 檢查是否命中 HandPanel（可用 Tag / 名稱 / 專用元件來判斷）
        if (hit != null)
        {
            HandPanelMarker handPanel = hit.GetComponent<HandPanelMarker>();

            if (handPanel != null)
            {
                // 命中 HandPanel => 回到手牌
                ReturnToHand();
                return;
            }
        }

        // 若不是 HandPanel => 觸發移動牌流程
        BattleManager bm = FindObjectOfType<BattleManager>();
        bm.UseMovementCard(cardData); // 進入可選擇的可達 Tile 流程

        // 同步 UI 狀態（例如：從手牌移除、扣除能量等）
        Destroy(gameObject);
    }

    private void ReturnToHand()
    {
        SetCardAlpha(originalAlpha);
        transform.SetParent(originalParent);
        rectTransform.anchoredPosition = Vector2.zero;
        originalAnchoredPosition = rectTransform.anchoredPosition;
        isDragging = false;
        isHovering = false;
        DisableHoverGlow();
    }

    /// <summary>
    /// 檢查拖放結束時的目標（敵人 / 地板格 / 其他）
    /// </summary>
    private void CheckDropTarget(PointerEventData eventData)
    {
        // 可選：用 UI 的 GraphicRaycaster 或 2D 物理 Raycast
        // 判斷邏輯：
        //   - 若指到 Enemy UI／碰撞體 => 視為對敵人使用
        //   - 若指到 Board Tile => 視為移動
        //   - 否則 => 還原到手牌
        RaycastHit2D hit2D = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(eventData.position), Vector2.zero);
        if (hit2D.collider != null)
        {
            // 檢查是否為 Enemy
            Enemy e = hit2D.collider.GetComponent<Enemy>();
            if (e != null)
            {
                // 對目標使用卡片（造成傷害 / 指定目標等）
                UseCardOnEnemy(e);
                return;
            }
        }

        // 若沒有命中可用目標 => 還原到手牌
        transform.SetParent(originalParent);
        rectTransform.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// 對指定的敵人使用此卡
    /// </summary>
    private void UseCardOnEnemy(Enemy enemyTarget)
    {
        // 交給 BattleManager 處理「PlayCard」的實際效果
        BattleManager bm = FindObjectOfType<BattleManager>();
        if (bm != null)
        {
            bm.PlayCard(cardData);
        }
        // 後續會由 BattleManager 決定是否移除手牌、更新 UI 等
    }

    private void SetCardAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
    }
    
    private void ResetHoverPosition()
    {
        if (!isHovering)
        {
            return;
        }

        rectTransform.anchoredPosition = originalAnchoredPosition;
        isHovering = false;
    }

    private void EnableHoverGlow()
    {
        if (hoverGlowOutline == null)
        {
            return;
        }

        hoverGlowOutline.useGraphicAlpha = false;
        hoverGlowOutline.effectColor = hoverGlowColor;
        hoverGlowOutline.effectDistance = Vector2.one * hoverGlowPadding;
        hoverGlowOutline.enabled = true;
    }

    private void DisableHoverGlow()
    {
        if (hoverGlowOutline == null)
        {
            return;
        }

        hoverGlowOutline.effectColor = originalGlowColor;
        hoverGlowOutline.effectDistance = originalGlowDistance;
        hoverGlowOutline.useGraphicAlpha = originalGlowUseGraphicAlpha;
        hoverGlowOutline.enabled = originalGlowEnabled;
    }

    private void SetupHoverGlow()
    {
        if (cardBackground == null)
        {
            return;
        }

        hoverGlowOutline = cardBackground.GetComponent<Outline>();
        if (hoverGlowOutline == null)
        {
            hoverGlowOutline = cardBackground.gameObject.AddComponent<Outline>();
            hoverGlowOutline.effectColor = new Color(hoverGlowColor.r, hoverGlowColor.g, hoverGlowColor.b, 0f);
            hoverGlowOutline.effectDistance = Vector2.zero;
            hoverGlowOutline.useGraphicAlpha = false;
        }

        originalGlowColor = hoverGlowOutline.effectColor;
        originalGlowDistance = hoverGlowOutline.effectDistance;
        originalGlowEnabled = hoverGlowOutline.enabled;
        originalGlowUseGraphicAlpha = hoverGlowOutline.useGraphicAlpha;

        if (!originalGlowEnabled)
        {
            hoverGlowOutline.enabled = false;
        }

    }
}
