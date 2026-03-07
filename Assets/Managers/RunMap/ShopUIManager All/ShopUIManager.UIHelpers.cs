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

        return false;
    }

    private void CreateOfferEntry(
        GameObject template,
        Transform parent,
        string title,
        int price,
        string description,
        UnityEngine.Events.UnityAction onClick)
    {
        if (template == null || parent == null)
            return;

        var entry = Instantiate(template, parent);
        entry.name = title;
        entry.SetActive(true);

        var button = entry.GetComponent<Button>() ?? entry.GetComponentInChildren<Button>(true);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onClick != null)
                button.onClick.AddListener(onClick);
        }

        ApplyOfferTexts(entry, title, price, description);
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
}
