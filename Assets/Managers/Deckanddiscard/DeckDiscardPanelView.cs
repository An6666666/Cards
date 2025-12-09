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
        AutoBindIfMissing();
        DeckUIBus.Register(this);
        // 可選：一啟用就嘗試自刷一次
        var p = GameObject.FindObjectOfType<Player>();
        if (p != null)
        {
            if (p.deck != null) RefreshDeck(p.deck);
            if (p.discardPile != null) RefreshDiscard(p.discardPile);
        }
        // Debug
        // Debug.Log($"[DeckDiscardPanelView] Registered. Total views={DeckUIBus.ViewCount}");
    }

    private void OnDisable()
    {
        DeckUIBus.Unregister(this);
        // Debug
        // Debug.Log($"[DeckDiscardPanelView] Unregistered. Total views={DeckUIBus.ViewCount}");
    }


    public void RefreshDeck(List<CardBase> deck)
    {
        if (deckContent == null || cardItemPrefab == null) return;
        ReturnAll(deckContent);
        for (int i = 0; i < deck.Count; i++)
        {
            var item = GetItem(deckContent);
            item.SetSprite(ResolveCardSprite(deck[i]));
        }
        var sr = deckContent.GetComponentInParent<ScrollRect>();
        if (sr != null) sr.normalizedPosition = new Vector2(0, 1);
        Debug.Log("Deck refreshed");
    }

    public void RefreshDiscard(List<CardBase> discard)
    {
        if (discardContent == null || cardItemPrefab == null) return;
        ReturnAll(discardContent);
        for (int i = 0; i < discard.Count; i++)
        {
            var item = GetItem(discardContent);
            item.SetSprite(ResolveCardSprite(discard[i]));
        }
        var sr = discardContent.GetComponentInParent<ScrollRect>();
        if (sr != null) sr.normalizedPosition = new Vector2(0, 1);

    }

    private void AutoBindIfMissing()
    {
        var rootObjects = gameObject.scene.IsValid() && gameObject.scene.isLoaded
            ? gameObject.scene.GetRootGameObjects()
            : null;

        Transform Search(string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return null;
            if (rootObjects != null)
            {
                foreach (var go in rootObjects)
                {
                    var found = FindChildContains(go.transform, keyword);
                    if (found != null) return found;
                }
            }
            return FindChildContains(transform, keyword);
        }

        deckContent ??= Search("Deck") ?? Search("牌庫");
        discardContent ??= Search("Discard") ?? Search("棄牌");
        cardItemPrefab ??= FindComponentInChildren<CardIconItem>("Card") ?? FindComponentInChildren<CardIconItem>(string.Empty);
    }

    private Transform FindChildContains(Transform parent, string keyword)
    {
        if (parent == null || string.IsNullOrEmpty(keyword)) return null;
        if (parent.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindChildContains(parent.GetChild(i), keyword);
            if (found != null) return found;
        }
        return null;
    }

    private T FindComponentInChildren<T>(string keyword) where T : Component
    {
        var comps = GetComponentsInChildren<T>(true);
        foreach (var c in comps)
        {
            if (string.IsNullOrEmpty(keyword) || c.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return c;
        }
        return null;
    }

}


