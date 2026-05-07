using System.Collections.Generic;
using DeckUI;
using UnityEngine;
using UnityEngine.UI;

public class RunAllDeckPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private CardIconItem cardItemPrefab;

    private readonly List<CardIconItem> spawnedItems = new List<CardIconItem>(64);

    private void Awake()
    {
        ResolveReferences();
        WireButtons();
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        WireButtons();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }
    }

    public void Open()
    {
        ResolveReferences();
        Refresh();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void SetReferences(ScrollRect newScrollRect, Transform newContentRoot, Button newCloseButton)
    {
        scrollRect = newScrollRect;
        contentRoot = newContentRoot;
        closeButton = newCloseButton;
        ResolveReferences();
        WireButtons();
    }

    public void Refresh()
    {
        ResolveReferences();
        ClearItems();

        if (contentRoot == null || cardItemPrefab == null)
        {
            return;
        }

        List<CardBase> orderedCards = CardDisplaySortUtility.BuildOrderedCards(GetAllDeckCards());
        for (int i = 0; i < orderedCards.Count; i++)
        {
            CardBase card = orderedCards[i];
            if (card == null)
            {
                continue;
            }

            CardIconItem item = Instantiate(cardItemPrefab, contentRoot, false);
            ResetItemTransform(item);
            item.gameObject.SetActive(true);
            item.Bind(card, ResolveCardSprite(card));
            spawnedItems.Add(item);
        }

        if (scrollRect != null)
        {
            scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }
    }

    private void ResolveReferences()
    {
        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        }

        if (contentRoot == null && scrollRect != null && scrollRect.content != null)
        {
            contentRoot = scrollRect.content;
        }

        if (closeButton == null)
        {
            closeButton = FindDirectChildButton(transform);
        }

        EnsureContentLayout();
    }

    private void WireButtons()
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.onClick.RemoveListener(Close);
        closeButton.onClick.AddListener(Close);
    }

    private void EnsureContentLayout()
    {
        if (contentRoot == null)
        {
            return;
        }

        VerticalLayoutGroup verticalLayout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout != null)
        {
            verticalLayout.enabled = false;
        }

        GridLayoutGroup gridLayout = contentRoot.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
        {
            gridLayout = contentRoot.gameObject.AddComponent<GridLayoutGroup>();
        }

        if (gridLayout == null)
        {
            return;
        }

        gridLayout.enabled = true;
        gridLayout.padding = new RectOffset(0, 0, 0, 0);
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.cellSize = new Vector2(25.73f, 134f);
        gridLayout.spacing = new Vector2(10.93f, 6.26f);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 4;

        ContentSizeFitter fitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        }

        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private List<CardBase> GetAllDeckCards()
    {
        PlayerRunSnapshot snapshot = RunManager.Instance != null ? RunManager.Instance.CurrentRunSnapshot : null;
        if (snapshot != null && snapshot.deck != null)
        {
            return new List<CardBase>(snapshot.deck);
        }

        return new List<CardBase>();
    }

    private void ClearItems()
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            if (spawnedItems[i] != null)
            {
                Destroy(spawnedItems[i].gameObject);
            }
        }

        spawnedItems.Clear();

        if (contentRoot == null)
        {
            return;
        }

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }
    }

    private Sprite ResolveCardSprite(CardBase card)
    {
        if (card == null) return null;

        System.Type type = card.GetType();
        System.Reflection.FieldInfo field = type.GetField("cardImage")
            ?? type.GetField("cardSprite")
            ?? type.GetField("artwork")
            ?? type.GetField("icon")
            ?? type.GetField("sprite");
        if (field != null && field.GetValue(card) is Sprite fieldSprite) return fieldSprite;

        System.Reflection.PropertyInfo property = type.GetProperty("CardImage")
            ?? type.GetProperty("CardSprite")
            ?? type.GetProperty("Artwork")
            ?? type.GetProperty("Icon")
            ?? type.GetProperty("Sprite");
        if (property != null && property.GetValue(card, null) is Sprite propertySprite) return propertySprite;

        System.Reflection.MethodInfo method = type.GetMethod("GetThumbnailSprite") ?? type.GetMethod("GetSprite");
        if (method != null && method.Invoke(card, null) is Sprite methodSprite) return methodSprite;

        return null;
    }

    private static void ResetItemTransform(CardIconItem item)
    {
        if (item == null)
        {
            return;
        }

        RectTransform rectTransform = item.transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
            rectTransform.anchoredPosition3D = Vector3.zero;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return;
        }

        item.transform.localScale = Vector3.one;
        item.transform.localPosition = Vector3.zero;
    }

    private static Button FindDirectChildButton(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Button button = parent.GetChild(i).GetComponent<Button>();
            if (button != null)
            {
                return button;
            }
        }

        return null;
    }
}
