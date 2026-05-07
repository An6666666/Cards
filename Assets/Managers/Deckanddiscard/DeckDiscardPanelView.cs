using System;
using System.Collections.Generic;
using DeckUI;
using UnityEngine;
using UnityEngine.UI;

public class DeckDiscardPanelView : MonoBehaviour
{
    [Header("Containers")]
    [SerializeField] private Transform deckContent;
    [SerializeField] private Transform discardContent;
    [SerializeField] private Transform allDeckContent;

    [Header("Prefabs")]
    [SerializeField] private CardIconItem cardItemPrefab;

    private readonly Queue<CardIconItem> _pool = new Queue<CardIconItem>(128);

    private Sprite ResolveCardSprite(CardBase card)
    {
        if (card == null) return null;
        // 先嘗試常見欄位
        var t = card.GetType();
        var f = t.GetField("cardImage") ?? t.GetField("cardSprite") ?? t.GetField("artwork") ?? t.GetField("icon") ?? t.GetField("sprite");
        if (f != null && f.GetValue(card) is Sprite s1) return s1;
        // 再試屬性
        var p = t.GetProperty("CardImage") ?? t.GetProperty("CardSprite") ?? t.GetProperty("Artwork") ?? t.GetProperty("Icon") ?? t.GetProperty("Sprite");
        if (p != null && p.GetValue(card, null) is Sprite s2) return s2;
        // 最後試方法
        var m = t.GetMethod("GetThumbnailSprite") ?? t.GetMethod("GetSprite");
        if (m != null && m.Invoke(card, null) is Sprite s3) return s3;
        return null;
    }

    private CardIconItem GetItem(Transform parent)
    {
        CardIconItem item = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(cardItemPrefab);

        // 一律先設父物件
        item.transform.SetParent(parent, false);

        // 嘗試取得 RectTransform；若沒有就用 Transform 退而求其次
        var rt = item.transform as RectTransform;
        if (rt != null)
        {
            // UI 佈局情境
            rt.localScale = Vector3.one;
            rt.anchoredPosition3D = Vector3.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            // 非 UI 佈局（你的根是空物件 Transform）
            item.transform.localScale = Vector3.one;
            item.transform.localPosition = Vector3.zero;
        }

        item.gameObject.SetActive(true);
        return item;
    }


    private void ReturnAll(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var t = parent.GetChild(i);
            var item = t.GetComponent<CardIconItem>();
            if (item == null) { Destroy(t.gameObject); continue; }
            item.gameObject.SetActive(false);
            item.transform.SetParent(transform, false);
            _pool.Enqueue(item);
        }
    }

    private void OnEnable()
    {
        ResolveAutoReferences();
        DeckUIBus.Register(this);
    }

    private void OnDisable()
    {
        DeckUIBus.Unregister(this);
    }


    public void RefreshDeck(List<CardBase> deck)
    {
        if (deckContent == null || cardItemPrefab == null) return;
        ReturnAll(deckContent);
        List<CardBase> orderedDeck = CardDisplaySortUtility.BuildOrderedCards(deck);
        for (int i = 0; i < orderedDeck.Count; i++)
        {
            var item = GetItem(deckContent);
            item.Bind(orderedDeck[i], ResolveCardSprite(orderedDeck[i]));
        }
        var sr = deckContent.GetComponentInParent<ScrollRect>();
        if (sr != null) sr.normalizedPosition = new Vector2(0, 1);
        Debug.Log("Deck refreshed");
    }

    public void RefreshDiscard(List<CardBase> discard)
    {
        if (discardContent == null || cardItemPrefab == null) return;
        ReturnAll(discardContent);
        List<CardBase> orderedDiscard = CardDisplaySortUtility.BuildOrderedCards(discard);
        for (int i = 0; i < orderedDiscard.Count; i++)
        {
            var item = GetItem(discardContent);
            item.Bind(orderedDiscard[i], ResolveCardSprite(orderedDiscard[i]));
        }
        var sr = discardContent.GetComponentInParent<ScrollRect>();
        if (sr != null) sr.normalizedPosition = new Vector2(0, 1);

    }

    public void RefreshAllDeck(List<CardBase> allDeck)
    {
        if (allDeckContent == null || cardItemPrefab == null) return;

        ReturnAll(allDeckContent);
        List<CardBase> orderedAllDeck = CardDisplaySortUtility.BuildOrderedCards(allDeck);
        for (int i = 0; i < orderedAllDeck.Count; i++)
        {
            var item = GetItem(allDeckContent);
            item.Bind(orderedAllDeck[i], ResolveCardSprite(orderedAllDeck[i]));
        }

        var sr = allDeckContent.GetComponentInParent<ScrollRect>();
        if (sr != null) sr.normalizedPosition = new Vector2(0, 1);
    }

    private void Reset()
    {
        // Force explicit binding from the inspector; this method helps Unity auto-assign on add.
        deckContent = null;
        discardContent = null;
        allDeckContent = null;
        cardItemPrefab = null;
    }

    private void ResolveAutoReferences()
    {
        if (allDeckContent != null)
        {
            return;
        }

        GameObject allDeckPanel = FindSceneGameObject("AllDeck Panel");
        if (allDeckPanel == null)
        {
            return;
        }

        ScrollRect scrollRect = allDeckPanel.GetComponentInChildren<ScrollRect>(true);
        if (scrollRect != null && scrollRect.content != null)
        {
            allDeckContent = scrollRect.content;
        }
    }

    private static GameObject FindSceneGameObject(string objectName)
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t != null && t.name == objectName)
            {
                return t.gameObject;
            }
        }

        return null;
    }

}


