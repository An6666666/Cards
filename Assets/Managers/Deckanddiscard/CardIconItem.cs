using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeckUI
{
    public class CardIconItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image icon;

        [Header("Tooltip")]
        [SerializeField] private Vector2 tooltipOffset = new Vector2(0f, 18f);
        [SerializeField] private int tooltipSortingOrder = 6000;
        [SerializeField] private bool matchParentCanvasSettings = true;
        [SerializeField] private GameObject tooltipTemplate;
        [SerializeField] private string tooltipTemplatePath = "CardVisual/CardTooltip";

        [Header("Scope")]
        [SerializeField] private Vector2 scopeOffset = new Vector2(0f, -112f);
        [SerializeField] private int scopeSortingOrder = 6000;
        [SerializeField] private GameObject scopeTemplate;
        [SerializeField] private string scopeTemplatePath = "CardVisual/Scope";

        private readonly Vector3[] worldCorners = new Vector3[4];

        private CardBase boundCard;
        private CardIconItem tooltipOwner;
        private RectTransform itemRect;
        private Canvas rootCanvas;
        private RectTransform rootCanvasRect;
        private RectTransform tooltipRect;
        private GameObject tooltipRoot;
        private Text tooltipNameText;
        private Text tooltipCostText;
        private Text tooltipDescriptionText;
        private Canvas tooltipCanvas;
        private RectTransform scopeRect;
        private GameObject scopeRoot;
        private Image scopePreviewImage;
        private Canvas scopeCanvas;
        private bool hasScope;
        private bool isHovering;

        private void Awake()
        {
            if (icon == null)
            {
                icon = GetComponent<Image>();
            }

            tooltipOwner = ResolveTooltipOwner();
            itemRect = transform as RectTransform;
        }

        private void OnDisable()
        {
            HideTooltip();
        }

        private void OnDestroy()
        {
            if (tooltipRoot != null)
            {
                Destroy(tooltipRoot);
            }

            if (scopeRoot != null)
            {
                Destroy(scopeRoot);
            }
        }

        private void LateUpdate()
        {
            if (isHovering)
            {
                RefreshPopupPositions();
            }
        }

        public void Bind(CardBase card, Sprite sprite)
        {
            if (tooltipOwner != null)
            {
                tooltipOwner.Bind(card, sprite);
                return;
            }

            boundCard = card;
            SetSprite(sprite);
            RefreshTooltipContent();
            RefreshScopeContent();
        }

        public void SetSprite(Sprite sprite)
        {
            if (icon == null)
            {
                return;
            }

            if (sprite != null)
            {
                icon.enabled = true;
                icon.sprite = sprite;
                icon.color = Color.white;
            }
            else
            {
                icon.enabled = true;
                icon.sprite = null;
                icon.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltipOwner != null)
            {
                tooltipOwner.OnPointerEnter(eventData);
                return;
            }

            if (boundCard == null)
            {
                return;
            }

            ShowTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltipOwner != null)
            {
                if (tooltipOwner.IsPointerInsideOwner(eventData))
                {
                    return;
                }

                tooltipOwner.HideTooltip();
                return;
            }

            HideTooltip();
        }

        private void ShowTooltip()
        {
            EnsureTooltip();
            EnsureScope();
            if (tooltipRoot == null && scopeRoot == null)
            {
                return;
            }

            RefreshTooltipContent();
            RefreshScopeContent();
            RefreshPopupPositions();
            if (tooltipRoot != null)
            {
                tooltipRoot.SetActive(true);
            }

            if (scopeRoot != null)
            {
                scopeRoot.SetActive(hasScope);
            }

            BringToFront();
            isHovering = true;
        }

        private void HideTooltip()
        {
            isHovering = false;
            if (tooltipRoot != null)
            {
                tooltipRoot.SetActive(false);
            }

            if (scopeRoot != null)
            {
                scopeRoot.SetActive(false);
            }
        }

        private void EnsureTooltip()
        {
            if (tooltipRoot != null)
            {
                return;
            }

            if (!EnsureCanvasReferences())
            {
                return;
            }

            GameObject resolvedTemplate = ResolveTooltipTemplate();
            if (resolvedTemplate == null)
            {
                return;
            }

            tooltipRoot = Instantiate(resolvedTemplate, rootCanvas.transform, false);
            tooltipRoot.name = "CardTooltip";
            tooltipRect = tooltipRoot.GetComponent<RectTransform>();

            tooltipNameText = FindTooltipText("CardNameText");
            tooltipCostText = FindTooltipText("CostText");
            tooltipDescriptionText = FindTooltipText("DescriptionText");

            EnsureTooltipCanvas();
            tooltipRoot.SetActive(false);
        }

        private void EnsureScope()
        {
            if (scopeRoot != null)
            {
                return;
            }

            if (!EnsureCanvasReferences())
            {
                return;
            }

            GameObject resolvedTemplate = ResolveScopeTemplate();
            if (resolvedTemplate == null)
            {
                return;
            }

            scopeRoot = Instantiate(resolvedTemplate, rootCanvas.transform, false);
            scopeRoot.name = "CardScope";
            scopeRect = scopeRoot.GetComponent<RectTransform>();
            scopePreviewImage = FindScopeImage();

            EnsureScopeCanvas();
            scopeRoot.SetActive(false);
        }

        private void RefreshTooltipContent()
        {
            if (tooltipRoot == null || boundCard == null)
            {
                return;
            }

            if (tooltipNameText != null)
            {
                tooltipNameText.text = boundCard.cardName;
            }

            if (tooltipCostText != null)
            {
                tooltipCostText.text = $"Cost: {Mathf.Max(0, boundCard.cost)}";
            }

            if (tooltipDescriptionText != null)
            {
                tooltipDescriptionText.text = boundCard.description;
            }
        }

        private void RefreshScopeContent()
        {
            if (scopeRoot == null)
            {
                return;
            }

            Sprite sprite = boundCard is AttackCardBase attackData ? attackData.scopeImage : null;
            hasScope = sprite != null;

            if (scopePreviewImage != null)
            {
                scopePreviewImage.sprite = sprite;
                scopePreviewImage.enabled = hasScope;
            }

            if (!hasScope)
            {
                scopeRoot.SetActive(false);
            }
        }

        private void RefreshTooltipPosition()
        {
            RefreshPopupPositions();
        }

        private void RefreshScopePosition()
        {
            RefreshPopupPositions();
        }

        private bool TryGetHoverAnchor(out Vector2 localPoint, out Camera eventCamera)
        {
            localPoint = default;
            eventCamera = null;

            if (itemRect == null || rootCanvasRect == null || rootCanvas == null)
            {
                return false;
            }

            eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;

            itemRect.GetWorldCorners(worldCorners);
            Vector3 topCenter = (worldCorners[1] + worldCorners[2]) * 0.5f;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, topCenter);

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(rootCanvasRect, screenPoint, eventCamera, out localPoint);
        }

        private void PositionPopupRect(RectTransform popupRect, Vector2 offset, Vector2 localPoint)
        {
            PositionPopupRect(popupRect, offset, localPoint, 0f);
        }

        private void PositionPopupRect(RectTransform popupRect, Vector2 offset, Vector2 localPoint, float sharedCorrectionX)
        {
            if (popupRect == null || rootCanvasRect == null)
            {
                return;
            }

            Vector2 popupRectSize = popupRect.rect.size;
            float targetX = localPoint.x + offset.x + sharedCorrectionX;
            float targetY = localPoint.y + offset.y;

            float minX = rootCanvasRect.rect.xMin + popupRectSize.x * 0.5f;
            float maxX = rootCanvasRect.rect.xMax - popupRectSize.x * 0.5f;
            targetX = Mathf.Clamp(targetX, minX, maxX);

            float maxY = rootCanvasRect.rect.yMax - popupRectSize.y * 0.5f;
            float minY = rootCanvasRect.rect.yMin + popupRectSize.y * 0.5f;
            targetY = Mathf.Clamp(targetY, minY, maxY);

            popupRect.anchoredPosition = new Vector2(targetX, targetY);
        }

        private void RefreshPopupPositions()
        {
            if (!TryGetHoverAnchor(out Vector2 localPoint, out _))
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            float sharedCorrectionX = CalculateSharedCorrectionX(localPoint);
            PositionPopupRect(tooltipRect, tooltipOffset, localPoint, sharedCorrectionX);
            if (hasScope)
            {
                PositionPopupRect(scopeRect, scopeOffset, localPoint, sharedCorrectionX);
            }
        }

        private float CalculateSharedCorrectionX(Vector2 localPoint)
        {
            if (rootCanvasRect == null)
            {
                return 0f;
            }

            bool hasAnyPopup = false;
            float minCorrection = float.NegativeInfinity;
            float maxCorrection = float.PositiveInfinity;

            AccumulatePopupCorrectionRange(tooltipRect, tooltipOffset, localPoint, true, ref hasAnyPopup, ref minCorrection, ref maxCorrection);
            AccumulatePopupCorrectionRange(scopeRect, scopeOffset, localPoint, hasScope, ref hasAnyPopup, ref minCorrection, ref maxCorrection);

            if (!hasAnyPopup)
            {
                return 0f;
            }

            if (minCorrection > 0f)
            {
                return minCorrection;
            }

            if (maxCorrection < 0f)
            {
                return maxCorrection;
            }

            return 0f;
        }

        private void AccumulatePopupCorrectionRange(
            RectTransform popupRect,
            Vector2 offset,
            Vector2 localPoint,
            bool includePopup,
            ref bool hasAnyPopup,
            ref float minCorrection,
            ref float maxCorrection)
        {
            if (!includePopup || popupRect == null || rootCanvasRect == null)
            {
                return;
            }

            Vector2 popupRectSize = popupRect.rect.size;
            float rawX = localPoint.x + offset.x;
            float minX = rootCanvasRect.rect.xMin + popupRectSize.x * 0.5f;
            float maxX = rootCanvasRect.rect.xMax - popupRectSize.x * 0.5f;

            hasAnyPopup = true;
            minCorrection = Mathf.Max(minCorrection, minX - rawX);
            maxCorrection = Mathf.Min(maxCorrection, maxX - rawX);
        }

        private void EnsureTooltipCanvas()
        {
            if (tooltipRoot == null)
            {
                return;
            }

            tooltipCanvas = tooltipRoot.GetComponent<Canvas>();
            if (tooltipCanvas == null)
            {
                tooltipCanvas = tooltipRoot.AddComponent<Canvas>();
            }

            tooltipCanvas.overrideSorting = true;
            tooltipCanvas.sortingOrder = tooltipSortingOrder;

            if (matchParentCanvasSettings && rootCanvas != null)
            {
                tooltipCanvas.renderMode = rootCanvas.renderMode;
                tooltipCanvas.worldCamera = rootCanvas.worldCamera;
                tooltipCanvas.sortingLayerID = rootCanvas.sortingLayerID;
            }

            CanvasGroup tooltipCanvasGroup = tooltipRoot.GetComponent<CanvasGroup>();
            if (tooltipCanvasGroup == null)
            {
                tooltipCanvasGroup = tooltipRoot.AddComponent<CanvasGroup>();
            }

            tooltipCanvasGroup.blocksRaycasts = false;
            tooltipCanvasGroup.interactable = false;
        }

        private void EnsureScopeCanvas()
        {
            if (scopeRoot == null)
            {
                return;
            }

            scopeCanvas = scopeRoot.GetComponent<Canvas>();
            if (scopeCanvas == null)
            {
                scopeCanvas = scopeRoot.AddComponent<Canvas>();
            }

            scopeCanvas.overrideSorting = true;
            scopeCanvas.sortingOrder = scopeSortingOrder;

            if (matchParentCanvasSettings && rootCanvas != null)
            {
                scopeCanvas.renderMode = rootCanvas.renderMode;
                scopeCanvas.worldCamera = rootCanvas.worldCamera;
                scopeCanvas.sortingLayerID = rootCanvas.sortingLayerID;
            }

            CanvasGroup scopeCanvasGroup = scopeRoot.GetComponent<CanvasGroup>();
            if (scopeCanvasGroup == null)
            {
                scopeCanvasGroup = scopeRoot.AddComponent<CanvasGroup>();
            }

            scopeCanvasGroup.blocksRaycasts = false;
            scopeCanvasGroup.interactable = false;
        }

        private void BringToFront()
        {
            if (tooltipRect == null)
            {
                if (scopeRect != null)
                {
                    scopeRect.SetAsLastSibling();
                }

                return;
            }

            if (scopeRect != null)
            {
                scopeRect.SetAsLastSibling();
            }

            tooltipRect.SetAsLastSibling();
        }

        private GameObject ResolveTooltipTemplate()
        {
            if (tooltipTemplate != null)
            {
                return tooltipTemplate;
            }

            BattleManager manager = BattleRuntimeContext.Active != null
                ? BattleRuntimeContext.Active.Manager
                : FindObjectOfType<BattleManager>();

            if (manager != null && manager.cardPrefab != null)
            {
                Transform prefabTooltip = manager.cardPrefab.transform.Find(tooltipTemplatePath);
                if (prefabTooltip != null)
                {
                    tooltipTemplate = prefabTooltip.gameObject;
                    return tooltipTemplate;
                }
            }

            CardUI sceneCardUi = FindObjectOfType<CardUI>(true);
            if (sceneCardUi != null)
            {
                Transform sceneTooltip = sceneCardUi.transform.Find(tooltipTemplatePath);
                if (sceneTooltip != null)
                {
                    return sceneTooltip.gameObject;
                }
            }

            return null;
        }

        private GameObject ResolveScopeTemplate()
        {
            if (scopeTemplate != null)
            {
                return scopeTemplate;
            }

            BattleManager manager = BattleRuntimeContext.Active != null
                ? BattleRuntimeContext.Active.Manager
                : FindObjectOfType<BattleManager>();

            if (manager != null && manager.cardPrefab != null)
            {
                Transform prefabScope = manager.cardPrefab.transform.Find(scopeTemplatePath);
                if (prefabScope != null)
                {
                    scopeTemplate = prefabScope.gameObject;
                    return scopeTemplate;
                }
            }

            CardUI sceneCardUi = FindObjectOfType<CardUI>(true);
            if (sceneCardUi != null)
            {
                Transform sceneScope = sceneCardUi.transform.Find(scopeTemplatePath);
                if (sceneScope != null)
                {
                    return sceneScope.gameObject;
                }
            }

            return null;
        }

        private Text FindTooltipText(string childName)
        {
            if (tooltipRoot == null)
            {
                return null;
            }

            Transform child = tooltipRoot.transform.Find(childName);
            return child != null ? child.GetComponent<Text>() : null;
        }

        private Image FindScopeImage()
        {
            if (scopeRoot == null)
            {
                return null;
            }

            Transform child = scopeRoot.transform.Find("ScopeIM");
            if (child != null)
            {
                return child.GetComponent<Image>();
            }

            return scopeRoot.GetComponent<Image>();
        }

        private CardIconItem ResolveTooltipOwner()
        {
            Transform current = transform.parent;
            while (current != null)
            {
                CardIconItem candidate = current.GetComponent<CardIconItem>();
                if (candidate != null)
                {
                    return candidate;
                }

                current = current.parent;
            }

            return null;
        }

        private bool IsPointerInsideOwner(PointerEventData eventData)
        {
            if (itemRect == null)
            {
                return false;
            }

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            Camera eventCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? parentCanvas.worldCamera
                : null;

            return RectTransformUtility.RectangleContainsScreenPoint(itemRect, eventData.position, eventCamera);
        }

        private bool EnsureCanvasReferences()
        {
            if (rootCanvas != null && rootCanvasRect != null)
            {
                return true;
            }

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                return false;
            }

            rootCanvas = parentCanvas.rootCanvas;
            rootCanvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
            return rootCanvas != null && rootCanvasRect != null;
        }
    }
}
