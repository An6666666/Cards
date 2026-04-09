using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class RewardUI
{
    private void ConfigureSkipButtonHover()
    {
        if (skipHoverConfigured || skipButton == null)
        {
            return;
        }

        Transform hoverTransform = skipButton.transform.Find("h");
        if (hoverTransform == null)
        {
            return;
        }

        skipHoverIndicator = hoverTransform.gameObject;
        skipHoverIndicator.SetActive(false);

        skipHoverIndicatorImage = skipHoverIndicator.GetComponent<Image>();
        if (skipHoverIndicatorImage != null)
        {
            skipHoverIndicatorImage.raycastTarget = false;
            CacheSkipHoverIndicatorVisuals();
        }

        EventTrigger trigger = skipButton.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = skipButton.gameObject.AddComponent<EventTrigger>();
        }

        AddHoverEntry(trigger, EventTriggerType.PointerEnter, true);
        AddHoverEntry(trigger, EventTriggerType.PointerExit, false);
        skipHoverConfigured = true;
    }

    private void AddHoverEntry(EventTrigger trigger, EventTriggerType eventType, bool visible)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(_ => SetSkipHoverVisible(visible));
        trigger.triggers.Add(entry);
    }

    private void SetSkipHoverVisible(bool visible)
    {
        if (skipHoverIndicator != null)
        {
            skipHoverIndicator.SetActive(visible);
        }
    }

    private void CacheSkipButtonVisuals()
    {
        if (skipButton == null)
        {
            return;
        }

        if (skipButtonImage == null)
        {
            skipButtonImage = skipButton.targetGraphic as Image;
            if (skipButtonImage == null)
            {
                skipButtonImage = skipButton.GetComponent<Image>();
            }
        }

        Transform existingImage = skipButton.transform.Find("RuntimeConfirmImage");
        if (existingImage != null)
        {
            Destroy(existingImage.gameObject);
        }

        if (!hasCachedSkipButtonSprite && skipButtonImage != null)
        {
            defaultSkipButtonSprite = skipButtonImage.sprite;
            hasCachedSkipButtonSprite = true;
        }
    }

    private void SetSkipButtonUseConfirmSprite(bool useConfirmSprite)
    {
        CacheSkipButtonVisuals();
        useConfirmSkipButtonVisuals = useConfirmSprite && relicConfirmButtonSprite != null;

        if (skipButtonImage == null)
        {
            SetSkipHoverVisible(false);
            return;
        }

        if (useConfirmSkipButtonVisuals)
        {
            skipButtonImage.sprite = relicConfirmButtonSprite;
        }
        else if (hasCachedSkipButtonSprite)
        {
            skipButtonImage.sprite = defaultSkipButtonSprite;
        }

        UpdateSkipHoverIndicatorVisual();
        SetSkipHoverVisible(false);
    }

    private void CacheSkipHoverIndicatorVisuals()
    {
        if (hasCachedSkipHoverIndicatorVisuals || skipHoverIndicatorImage == null)
        {
            return;
        }

        RectTransform rect = skipHoverIndicatorImage.rectTransform;
        defaultSkipHoverIndicatorSprite = skipHoverIndicatorImage.sprite;
        defaultSkipHoverIndicatorSize = rect.sizeDelta;
        defaultSkipHoverIndicatorOffset = rect.anchoredPosition;
        defaultSkipHoverIndicatorPreserveAspect = skipHoverIndicatorImage.preserveAspect;
        hasCachedSkipHoverIndicatorVisuals = true;
    }

    private void UpdateSkipHoverIndicatorVisual()
    {
        if (skipHoverIndicatorImage == null)
        {
            return;
        }

        CacheSkipHoverIndicatorVisuals();

        RectTransform rect = skipHoverIndicatorImage.rectTransform;
        bool useConfirmHoverSprite = useConfirmSkipButtonVisuals && relicConfirmButtonHoverSprite != null;

        if (useConfirmHoverSprite)
        {
            skipHoverIndicatorImage.sprite = relicConfirmButtonHoverSprite;
            skipHoverIndicatorImage.preserveAspect = relicConfirmButtonHoverPreserveAspect;
            rect.anchoredPosition = relicConfirmButtonHoverIndicatorOffset;
            rect.sizeDelta = relicConfirmButtonHoverIndicatorSize;
        }
        else if (hasCachedSkipHoverIndicatorVisuals)
        {
            skipHoverIndicatorImage.sprite = defaultSkipHoverIndicatorSprite;
            skipHoverIndicatorImage.preserveAspect = defaultSkipHoverIndicatorPreserveAspect;
            rect.anchoredPosition = defaultSkipHoverIndicatorOffset;
            rect.sizeDelta = defaultSkipHoverIndicatorSize;
        }
    }
}
