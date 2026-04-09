using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class RewardUI
{
    private void DisplayRelicChoices(List<RelicBase> relicChoices)
    {
        if (cardParent == null || relicChoices == null)
        {
            return;
        }

        for (int i = 0; i < relicChoices.Count; i++)
        {
            RelicBase relic = relicChoices[i];
            if (relic == null)
            {
                continue;
            }

            RelicChoiceView choiceView = CreateRelicChoiceView(relic);
            if (choiceView != null)
            {
                relicChoiceViews.Add(choiceView);
            }
        }

        RefreshRewardLayout();
    }

    private RelicChoiceView CreateRelicChoiceView(RelicBase relic)
    {
        GameObject container = new GameObject($"RelicChoice_{relic.cardName}", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.SetParent(cardParent, false);
        containerRect.localScale = Vector3.one;

        LayoutElement layoutElement = container.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = relicChoicePreferredSize.x;
        layoutElement.preferredHeight = relicChoicePreferredSize.y;

        Image background = container.GetComponent<Image>();
        background.color = relicChoiceNormalColor;
        background.raycastTarget = false;

        GameObject relicObject = manager != null && manager.RelicRewardIconPrefab != null
            ? Instantiate(manager.RelicRewardIconPrefab, containerRect, false)
            : CreateFallbackRelicVisual(containerRect, relic);

        if (relicObject == null)
        {
            Destroy(container);
            return null;
        }

        RectTransform relicRect = relicObject.transform as RectTransform;
        if (relicRect != null)
        {
            relicRect.anchorMin = Vector2.zero;
            relicRect.anchorMax = Vector2.one;
            relicRect.offsetMin = Vector2.zero;
            relicRect.offsetMax = Vector2.zero;
            relicRect.localScale = Vector3.one;
            relicRect.anchoredPosition = Vector2.zero;
        }

        Image relicRootImage = relicObject.GetComponent<Image>();
        if (relicRootImage != null)
        {
            relicRootImage.color = new Color(1f, 1f, 1f, 0.001f);
            relicRootImage.raycastTarget = true;
        }

        BattleRelicUIItem itemView = relicObject.GetComponent<BattleRelicUIItem>() ??
                                     relicObject.GetComponentInChildren<BattleRelicUIItem>(true);
        if (itemView != null)
        {
            itemView.Bind(relic);
            itemView.SetTooltipTransformOverride(containerRect, relicTooltipScaleMultiplier, true, relicTooltipPositionOffset);
        }

        Button button = relicObject.GetComponent<Button>() ?? relicObject.GetComponentInChildren<Button>(true);
        if (button == null)
        {
            button = relicObject.AddComponent<Button>();
        }

        if (relicRootImage != null)
        {
            button.targetGraphic = relicRootImage;
        }

        RectTransform iconRect = relicObject.transform.Find("Image") as RectTransform;
        if (iconRect != null)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = relicIconSize;
        }

        Transform counterText = relicObject.transform.Find("CounterText");
        if (counterText != null)
        {
            counterText.gameObject.SetActive(false);
        }

        RelicChoiceView choiceView = new RelicChoiceView
        {
            Relic = relic,
            ContainerRect = containerRect,
            BackgroundImage = background,
            Button = button,
            ItemView = itemView
        };

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SelectRelicChoice(choiceView));
        SetRelicChoiceVisualState(choiceView, false);

        return choiceView;
    }

    private GameObject CreateFallbackRelicVisual(Transform parent, RelicBase relic)
    {
        GameObject fallback = new GameObject("RelicFallback", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = fallback.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = fallback.GetComponent<Image>();
        image.sprite = relic != null ? relic.cardImage : null;
        image.preserveAspect = true;
        image.color = Color.white;

        return fallback;
    }

    private void SelectRelicChoice(RelicChoiceView choiceView)
    {
        if (choiceView == null)
        {
            return;
        }

        selectedRelicChoiceView = choiceView;

        for (int i = 0; i < relicChoiceViews.Count; i++)
        {
            RelicChoiceView current = relicChoiceViews[i];
            SetRelicChoiceVisualState(current, current == choiceView);
        }

        if (skipButton != null)
        {
            skipButton.interactable = true;
        }

        EventSystem.current?.SetSelectedGameObject(choiceView.Button != null ? choiceView.Button.gameObject : null);
    }

    private void SetRelicChoiceVisualState(RelicChoiceView choiceView, bool isSelected)
    {
        if (choiceView == null)
        {
            return;
        }

        if (choiceView.BackgroundImage != null)
        {
            choiceView.BackgroundImage.color = isSelected ? relicChoiceSelectedColor : relicChoiceNormalColor;
        }

        if (choiceView.ContainerRect != null)
        {
            choiceView.ContainerRect.DOKill();
            choiceView.ContainerRect
                .DOScale(isSelected ? Vector3.one * relicChoiceSelectedScale : Vector3.one, relicChoiceTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(choiceView.ContainerRect.gameObject, LinkBehaviour.KillOnDisable)
                .SetUpdate(true);
        }
    }

    private void ConfirmSelectedRelic()
    {
        if (rewardStage != RewardStage.RelicReward || selectedRelicChoiceView == null)
        {
            return;
        }

        if (manager != null && manager.player != null && selectedRelicChoiceView.Relic != null)
        {
            manager.player.AcquireRelic(selectedRelicChoiceView.Relic);
            manager.RefreshRelicUI();
        }

        pendingRelicChoices = null;
        selectedRelicChoiceView = null;

        if (pendingCardChoices != null && pendingCardChoices.Count > 0)
        {
            ShowCardRewardStage(pendingCardChoices);
            return;
        }

        Close();
    }
}
