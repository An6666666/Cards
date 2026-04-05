using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class ShopUIManager
{
    private void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (ShouldPreserveChild(child))
                continue;

            Destroy(child.gameObject);
        }
    }

    private bool ShouldPreserveChild(Transform child)
    {
        if (child == null)
            return false;

        if (removalCostText != null && child == removalCostText.transform)
            return true;

        if (removalEntryTemplate != null && child == removalEntryTemplate.transform)
            return true;

        if (cardOfferTemplate != null && child == cardOfferTemplate.transform)
            return true;

        if (relicOfferTemplate != null && child == relicOfferTemplate.transform)
            return true;

        return false;
    }

    private GameObject CreateOfferEntry(
        GameObject template,
        Transform parent,
        string title,
        int price,
        string description,
        UnityEngine.Events.UnityAction onClick)
    {
        if (template == null || parent == null)
            return null;

        var entry = Instantiate(template, parent);
        entry.name = title;
        entry.SetActive(true);

        var button = entry.GetComponent<Button>() ?? entry.GetComponentInChildren<Button>(true);
        if (button == null)
        {
            button = entry.AddComponent<Button>();
            button.targetGraphic = entry.GetComponent<Image>() ?? entry.GetComponentInChildren<Image>(true);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onClick != null)
                button.onClick.AddListener(onClick);
        }

        ApplyOfferTexts(entry, title, price, description);
        return entry;
    }

    private void ApplyOfferIcon(GameObject entry, Sprite sprite, bool createIfMissing = true)
    {
        if (entry == null)
            return;

        Image iconImage = FindOfferIconImage(entry);
        if (iconImage == null && createIfMissing)
            iconImage = CreateOfferIconImage(entry.transform);

        if (iconImage == null)
            return;

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
        iconImage.preserveAspect = true;
    }

    private Image ApplyShopRelicOfferIcon(GameObject entry, Sprite sprite)
    {
        if (entry == null)
            return null;

        HideShopRelicEntryBackground(entry);

        Image templateImage = FindShopRelicTemplateImage(entry.transform);
        if (templateImage != null)
        {
            templateImage.sprite = sprite;
            templateImage.enabled = sprite != null;
            templateImage.preserveAspect = true;
            return templateImage;
        }

        ApplyOfferIcon(entry, sprite);
        return FindOfferIconImage(entry);
    }

    private void ConfigureShopRelicInstance(GameObject entry, GameObject relicObject)
    {
        if (entry == null || relicObject == null)
            return;

        HideShopRelicEntryBackground(entry);

        RectTransform relicRect = relicObject.GetComponent<RectTransform>();
        RectTransform parentRect = relicObject.transform.parent as RectTransform;
        if (relicRect != null && parentRect != null)
            StretchRectTransformToParent(relicRect);

        Image relicImage = FindOfferIconImage(relicObject);
        if (relicImage == null)
        {
            ApplyShopRelicTooltipLayout(entry, relicObject);
            return;
        }

        relicImage.preserveAspect = true;

        RectTransform imageRect = relicImage.rectTransform;
        if (imageRect != null)
            StretchRectTransformToParent(imageRect);

        ApplyShopRelicTooltipLayout(entry, relicObject);
    }

    private Image ApplyShopRelicPrefabIcon(GameObject iconObject, Sprite sprite)
    {
        if (iconObject == null)
            return null;

        Image iconImage = FindOfferIconImage(iconObject);
        if (iconImage == null)
        {
            ApplyOfferIcon(iconObject, sprite);
            return FindOfferIconImage(iconObject);
        }

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
        iconImage.preserveAspect = true;
        return iconImage;
    }

    private void ApplyShopRelicTemplateLayout(GameObject entry, RectTransform targetRect)
    {
        if (entry == null || targetRect == null)
            return;

        HideShopRelicEntryBackground(entry);

        Image templateImage = FindShopRelicTemplateImage(entry.transform);
        if (templateImage == null)
            return;

        RectTransform sourceRect = templateImage.rectTransform;
        targetRect.anchorMin = sourceRect.anchorMin;
        targetRect.anchorMax = sourceRect.anchorMax;
        targetRect.pivot = sourceRect.pivot;
        targetRect.anchoredPosition = sourceRect.anchoredPosition;
        targetRect.sizeDelta = sourceRect.sizeDelta;
        targetRect.localRotation = sourceRect.localRotation;
        targetRect.localScale = sourceRect.localScale;

        templateImage.enabled = false;
        templateImage.raycastTarget = false;
    }

    private void StretchRectTransformToParent(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
    }

    private void HideShopRelicEntryBackground(GameObject entry)
    {
        if (entry == null)
            return;

        Image rootImage = entry.GetComponent<Image>();
        if (rootImage != null && rootImage.sprite == null)
        {
            rootImage.enabled = false;
            rootImage.color = Color.clear;
            rootImage.raycastTarget = false;
        }

        Image backgroundImage = FindDirectChildImage(entry.transform, "BG");
        if (backgroundImage != null && backgroundImage.sprite == null)
        {
            backgroundImage.enabled = false;
            backgroundImage.color = Color.clear;
            backgroundImage.raycastTarget = false;
        }
    }

    private void ApplyShopRelicTooltipLayout(GameObject entry, GameObject relicObject)
    {
        if (entry == null || relicObject == null)
            return;

        BattleRelicUIItem relicUiItem = relicObject.GetComponent<BattleRelicUIItem>() ??
                                        relicObject.GetComponentInChildren<BattleRelicUIItem>(true);
        if (relicUiItem == null)
            return;

        RectTransform tooltipAnchor = FindNamedRectTransform(entry.transform, "RelicTooltipAnchor");
        if (tooltipAnchor == null)
        {
            relicUiItem.ClearTooltipTransformOverride();
            return;
        }

        relicUiItem.SetTooltipTransformOverride(tooltipAnchor);
    }

    private void ApplyOfferTexts(GameObject entry, string title, int price, string description, Transform excludeRoot = null)
    {
        if (entry == null)
            return;

        var uiTexts = FilterTexts(entry.GetComponentsInChildren<Text>(true), excludeRoot);
        ApplyOfferTexts(
            uiTexts,
            title,
            price,
            description,
            text => text.name,
            text => text.gameObject,
            text => text.text,
            (text, value) => text.text = value);

        var tmpTexts = FilterTexts(entry.GetComponentsInChildren<TMP_Text>(true), excludeRoot);
        ApplyOfferTexts(
            tmpTexts,
            title,
            price,
            description,
            text => text.name,
            text => text.gameObject,
            text => text.text,
            (text, value) => text.text = value);
    }

    private void ApplyOfferTexts<TText>(
        IEnumerable<TText> texts,
        string title,
        int price,
        string description,
        Func<TText, string> nameSelector,
        Func<TText, GameObject> gameObjectSelector,
        Func<TText, string> textGetter,
        Action<TText, string> textSetter) where TText : Component
    {
        var textList = texts?.Where(text => text != null).ToList();
        if (textList == null || textList.Count == 0)
            return;

        TText titleText = default;
        TText priceText = default;
        TText descriptionText = default;

        foreach (var text in textList)
        {
            string lowerName = nameSelector(text).ToLowerInvariant();

            if (EqualityComparer<TText>.Default.Equals(titleText, default) &&
                (lowerName.Contains("title") || lowerName.Contains("name")))
            {
                titleText = text;
                continue;
            }

            if (EqualityComparer<TText>.Default.Equals(priceText, default) &&
                (lowerName.Contains("price") || lowerName.Contains("cost")))
            {
                priceText = text;
                continue;
            }

            if (EqualityComparer<TText>.Default.Equals(descriptionText, default) &&
                (lowerName.Contains("description") || lowerName.Contains("desc")))
            {
                descriptionText = text;
            }
        }

        if (EqualityComparer<TText>.Default.Equals(priceText, default))
        {
            priceText = textList.FirstOrDefault(text =>
            {
                string content = textGetter(text);
                if (string.IsNullOrEmpty(content))
                    return false;

                string lowerContent = content.ToLowerInvariant();
                return lowerContent.Contains("price") ||
                       lowerContent.Contains("cost") ||
                       lowerContent.Contains("gold") ||
                       lowerContent.Contains("金") ||
                       lowerContent.Contains("價");
            });
        }

        if (EqualityComparer<TText>.Default.Equals(priceText, default))
            priceText = textList.FirstOrDefault(text => !EqualityComparer<TText>.Default.Equals(text, titleText));

        if (!EqualityComparer<TText>.Default.Equals(titleText, default))
        {
            string titleValue = EqualityComparer<TText>.Default.Equals(titleText, priceText)
                ? $"{title} - {price} 金幣"
                : title;
            textSetter(titleText, titleValue);
        }

        if (!EqualityComparer<TText>.Default.Equals(priceText, default) &&
            !EqualityComparer<TText>.Default.Equals(priceText, titleText))
        {
            textSetter(priceText, $"{price}");
        }

        if (!EqualityComparer<TText>.Default.Equals(descriptionText, default))
        {
            gameObjectSelector(descriptionText).SetActive(!string.IsNullOrWhiteSpace(description));
            textSetter(descriptionText, description);
        }

        if (EqualityComparer<TText>.Default.Equals(priceText, default) &&
            !EqualityComparer<TText>.Default.Equals(titleText, default))
        {
            textSetter(titleText, $"{title} - {price} 金幣");
        }
    }

    private IEnumerable<TText> FilterTexts<TText>(IEnumerable<TText> texts, Transform excludeRoot) where TText : Component
    {
        if (excludeRoot == null)
            return texts;

        return texts.Where(text => text != null && !IsUnderContainer(text.transform, excludeRoot));
    }

    private bool IsUnderContainer(Transform target, Transform container)
    {
        if (target == null || container == null)
            return false;

        var current = target;
        while (current != null)
        {
            if (current == container)
                return true;

            current = current.parent;
        }

        return false;
    }

    private void CacheSceneReferences()
    {
        if (goldText == null)
            goldText = FindTextByName("Gold");
        if (messageText == null)
            messageText = FindTextByName("Message");
        if (removalCostText == null)
            removalCostText = FindTextByName("RemovalCost");
        if (cardListParent == null)
            cardListParent = FindContainerByName("CardList");
        if (relicListParent == null)
            relicListParent = FindContainerByName("RelicList");
        if (removalListParent == null)
            removalListParent = FindContainerByName("RemovalList");
    }

    private Text FindTextByName(string partialName)
    {
        var texts = GetComponentsInChildren<Text>(true);
        string lookup = partialName.ToLowerInvariant();
        return texts.FirstOrDefault(text => text.name.ToLowerInvariant().Contains(lookup));
    }

    private Transform FindContainerByName(string partialName)
    {
        var transforms = GetComponentsInChildren<Transform>(true);
        string lookup = partialName.ToLowerInvariant();
        return transforms.FirstOrDefault(current => current != transform && current.name.ToLowerInvariant().Contains(lookup));
    }

    private Transform FindCardContainer(Transform root)
    {
        if (root == null)
            return null;

        foreach (var current in root.GetComponentsInChildren<Transform>(true))
        {
            if (current == root)
                continue;

            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains("cardcontainer") ||
                lowerName.Contains("cardholder") ||
                lowerName.Contains("cardroot") ||
                lowerName == "card")
            {
                return current;
            }
        }

        return null;
    }

    private Transform FindOfferVisualContainer(Transform root)
    {
        if (root == null)
            return null;

        foreach (Transform current in root.GetComponentsInChildren<Transform>(true))
        {
            if (current == root)
                continue;

            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains("reliccontainer") ||
                lowerName.Contains("iconcontainer") ||
                lowerName.Contains("iconroot") ||
                lowerName.Contains("artroot") ||
                lowerName.Contains("visualroot"))
            {
                return current;
            }
        }

        return FindCardContainer(root);
    }

    private void ResetTransform(Transform target)
    {
        if (target == null)
            return;

        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;

        if (target is RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    private Image FindOfferIconImage(GameObject entry)
    {
        if (entry == null)
            return null;

        Image[] images = entry.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            string lowerName = image.name.ToLowerInvariant();
            if (lowerName == "image" ||
                lowerName == "icon" ||
                lowerName.Contains("offericon") ||
                lowerName.Contains("relicicon") ||
                lowerName.Contains("iconimage"))
            {
                return image;
            }
        }

        if (images.Length == 1)
            return images[0];

        return null;
    }

    private Image FindShopRelicTemplateImage(Transform root)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            string lowerName = child.name.ToLowerInvariant();
            if (lowerName == "image" || lowerName == "relicimage" || lowerName == "relicofferimage")
            {
                Image image = child.GetComponent<Image>();
                if (image != null)
                    return image;
            }

            if (lowerName == "bg")
            {
                Image image = child.GetComponent<Image>();
                if (image != null && image.sprite != null)
                    return image;
            }
        }

        return null;
    }

    private Image FindDirectChildImage(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        foreach (Transform child in root)
        {
            if (!string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                continue;

            Image image = child.GetComponent<Image>();
            if (image != null)
                return image;
        }

        return null;
    }

    private RectTransform FindNamedRectTransform(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                return child as RectTransform;
        }

        return null;
    }

    private Image CreateOfferIconImage(Transform parent)
    {
        if (parent == null)
            return null;

        GameObject frameObject = new GameObject("RelicOfferIconFrame", typeof(RectTransform), typeof(Image));
        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.SetParent(parent, false);
        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(132f, 132f);
        frameRect.anchoredPosition = new Vector2(0f, 34f);

        Image frameImage = frameObject.GetComponent<Image>();
        frameImage.color = new Color(1f, 1f, 1f, 0.1f);
        frameImage.raycastTarget = false;

        Outline outline = frameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.4f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        GameObject iconObject = new GameObject("RelicOfferIcon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(frameRect, false);
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(108f, 108f);
        iconRect.anchoredPosition = Vector2.zero;

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.raycastTarget = false;

        return iconImage;
    }
}
