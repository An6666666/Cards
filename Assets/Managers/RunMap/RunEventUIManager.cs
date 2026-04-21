using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RunEventUIManager : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private GameObject relicPrefab;
    [SerializeField, Min(0.01f)] private float cardRewardScale = 0.24f;
    [SerializeField, Min(0.01f)] private float relicRewardScale = 7.2f;
    [SerializeField] private List<EventOptionView> optionViews = new();

    private static Font fallbackFont;
    private Action<RunEventOption> onOptionSelected;

    private const float RewardRootOffsetX = -640f;
    private const float RewardRootWidth = 210f;
    private const float RewardSpacing = 8f;
    private static readonly Vector2 CardRewardSlotSize = new Vector2(92f, 128f);
    private static readonly Vector2 RelicRewardSlotSize = new Vector2(76f, 76f);
    private static readonly Color RewardFallbackTextColor = new Color(0.24f, 0.10f, 0.08f, 1f);

    private void Awake()
    {
        Hide();
    }

    public void ShowEvent(RunEventDefinition definition, Action<RunEventOption> optionCallback)
    {
        onOptionSelected = optionCallback;

        if (definition == null)
        {
            optionCallback?.Invoke(null);
            return;
        }

        if (panelRoot != null && !panelRoot.activeSelf)
        {
            panelRoot.SetActive(true);
        }

        if (titleText != null)
        {
            titleText.text = definition.Title;
        }

        if (descriptionText != null)
        {
            descriptionText.text = definition.Description;
        }

        IReadOnlyList<RunEventOption> options = definition.Options;
        for (int i = 0; i < optionViews.Count; i++)
        {
            EventOptionView view = optionViews[i];
            bool hasOption = options != null && i < options.Count;
            if (hasOption)
            {
                view.Bind(options[i], HandleOptionClicked, this);
            }
            else
            {
                view.Reset();
            }
        }
    }

    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void HandleOptionClicked(RunEventOption option)
    {
        Hide();
        onOptionSelected?.Invoke(option);
    }

    private bool HasRewardContent(RunEventOption option)
    {
        return option != null
            && (HasAnyEntries(option.rewardCards)
            || HasAnyEntries(option.rewardRelics));
    }

    private static bool HasAnyEntries<T>(IList<T> entries) where T : class
    {
        if (entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private GameObject CreateCardReward(Transform parent, CardBase card)
    {
        if (parent == null || card == null)
        {
            return null;
        }

        RectTransform slotRect = CreateRewardSlot(parent, $"CardReward_{card.name}", CardRewardSlotSize);
        if (slotRect == null)
        {
            return null;
        }

        if (cardPrefab == null)
        {
            CreateFallbackRewardLabel(slotRect, card.cardName);
            return slotRect.gameObject;
        }

        GameObject cardObject = Instantiate(cardPrefab, slotRect, false);
        cardObject.name = $"Reward_{card.cardName}";

        RectTransform cardRect = cardObject.transform as RectTransform;
        if (cardRect != null)
        {
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.localScale = Vector3.one * cardRewardScale;
        }

        CardUI cardUi = cardObject.GetComponent<CardUI>();
        if (cardUi != null)
        {
            cardUi.SetupCard(card);
            cardUi.SetDisplayContext(CardUI.DisplayContext.Reward);
            cardUi.SetInteractable(true);
        }

        return slotRect.gameObject;
    }

    private GameObject CreateRelicReward(Transform parent, RelicBase relic)
    {
        if (parent == null || relic == null)
        {
            return null;
        }

        RectTransform slotRect = CreateRewardSlot(parent, $"RelicReward_{relic.name}", RelicRewardSlotSize);
        if (slotRect == null)
        {
            return null;
        }

        if (relicPrefab == null)
        {
            CreateFallbackRewardLabel(slotRect, relic.cardName);
            return slotRect.gameObject;
        }

        GameObject relicObject = Instantiate(relicPrefab, slotRect, false);
        relicObject.name = $"RewardRelic_{relic.cardName}";

        RectTransform relicRect = relicObject.transform as RectTransform;
        if (relicRect != null)
        {
            relicRect.anchorMin = new Vector2(0.5f, 0.5f);
            relicRect.anchorMax = new Vector2(0.5f, 0.5f);
            relicRect.pivot = new Vector2(0.5f, 0.5f);
            relicRect.anchoredPosition = Vector2.zero;
            relicRect.localScale = Vector3.one * relicRewardScale;
        }

        BattleRelicUIItem itemView = relicObject.GetComponent<BattleRelicUIItem>()
            ?? relicObject.GetComponentInChildren<BattleRelicUIItem>(true);
        if (itemView != null)
        {
            itemView.Bind(relic);
        }

        Transform counterText = relicObject.transform.Find("CounterText");
        if (counterText != null)
        {
            counterText.gameObject.SetActive(false);
        }

        return slotRect.gameObject;
    }

    private void CreateFallbackRewardLabel(RectTransform parent, string labelText)
    {
        if (parent == null)
        {
            return;
        }

        Text valueText = CreateTextElement(parent, "FallbackLabel", 22, FontStyle.Bold, TextAnchor.MiddleCenter, RewardFallbackTextColor);
        valueText.text = labelText;
        valueText.horizontalOverflow = HorizontalWrapMode.Wrap;
        valueText.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private RectTransform CreateRewardSlot(Transform parent, string objectName, Vector2 size)
    {
        if (parent == null)
        {
            return null;
        }

        GameObject slot = new GameObject(objectName, typeof(RectTransform), typeof(LayoutElement));
        RectTransform slotRect = slot.GetComponent<RectTransform>();
        slotRect.SetParent(parent, false);
        slotRect.localScale = Vector3.one;
        slotRect.anchorMin = new Vector2(0.5f, 0.5f);
        slotRect.anchorMax = new Vector2(0.5f, 0.5f);
        slotRect.pivot = new Vector2(0.5f, 0.5f);
        slotRect.sizeDelta = size;

        LayoutElement layoutElement = slot.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = size.x;
        layoutElement.preferredHeight = size.y;

        return slotRect;
    }

    private Text CreateTextElement(Transform parent, string objectName, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color? colorOverride = null)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.localScale = Vector3.one;

        Text text = textObject.GetComponent<Text>();
        ConfigureText(text, fontSize, fontStyle, alignment, colorOverride);
        return text;
    }

    private void ConfigureText(Text text, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color? colorOverride = null)
    {
        if (text == null)
        {
            return;
        }

        text.font = ResolveEventFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = colorOverride ?? new Color(0.24f, 0.10f, 0.08f, 1f);
        text.supportRichText = true;
        text.raycastTarget = false;
    }

    private Font ResolveEventFont()
    {
        if (titleText != null && titleText.font != null)
        {
            return titleText.font;
        }

        if (descriptionText != null && descriptionText.font != null)
        {
            return descriptionText.font;
        }

        for (int i = 0; i < optionViews.Count; i++)
        {
            Font optionFont = optionViews[i]?.LabelFont;
            if (optionFont != null)
            {
                return optionFont;
            }
        }

        return GetFallbackFont();
    }

    private static Font GetFallbackFont()
    {
        if (fallbackFont == null)
        {
            fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return fallbackFont;
    }

    [Serializable]
    private class EventOptionView
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button button;
        [SerializeField] private Text label;

        private readonly List<GameObject> spawnedRewardEntries = new();
        private RectTransform rewardRoot;

        public Font LabelFont => label != null ? label.font : null;

        public void Bind(RunEventOption option, Action<RunEventOption> onClick, RunEventUIManager owner)
        {
            SetActive(true);

            if (label != null)
            {
                label.text = option != null ? option.optionLabel : string.Empty;
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke(option));
            }

            RebuildRewards(option, owner);
        }

        public void Reset()
        {
            ClearRewards();
            SetActive(false);
        }

        public void SetActive(bool active)
        {
            if (root != null)
            {
                root.SetActive(active);
            }
        }

        private void EnsureRewardRoot()
        {
            if (rewardRoot != null || root == null)
            {
                return;
            }

            GameObject rewardObject = new GameObject(
                "RewardRoot",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));

            rewardRoot = rewardObject.GetComponent<RectTransform>();
            rewardRoot.SetParent(root.transform, false);
            rewardRoot.anchorMin = new Vector2(0.5f, 0.5f);
            rewardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            rewardRoot.pivot = new Vector2(0.5f, 0.5f);
            rewardRoot.anchoredPosition = new Vector2(RewardRootOffsetX, -55f);
            rewardRoot.sizeDelta = new Vector2(RewardRootWidth, 0f);

            VerticalLayoutGroup layoutGroup = rewardObject.GetComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = RewardSpacing;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            ContentSizeFitter fitter = rewardObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void RebuildRewards(RunEventOption option, RunEventUIManager owner)
        {
            ClearRewards();
            EnsureRewardRoot();

            if (rewardRoot == null)
            {
                return;
            }

            bool hasRewardContent = owner != null && owner.HasRewardContent(option);
            rewardRoot.gameObject.SetActive(hasRewardContent);
            if (!hasRewardContent || owner == null || option == null)
            {
                return;
            }

            if (option.rewardCards != null)
            {
                for (int i = 0; i < option.rewardCards.Count; i++)
                {
                    GameObject rewardObject = owner.CreateCardReward(rewardRoot, option.rewardCards[i]);
                    TrackReward(rewardObject);
                }
            }

            if (option.rewardRelics != null)
            {
                for (int i = 0; i < option.rewardRelics.Count; i++)
                {
                    GameObject rewardObject = owner.CreateRelicReward(rewardRoot, option.rewardRelics[i]);
                    TrackReward(rewardObject);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rewardRoot);
        }

        private void TrackReward(GameObject rewardObject)
        {
            if (rewardObject != null)
            {
                spawnedRewardEntries.Add(rewardObject);
            }
        }

        private void ClearRewards()
        {
            for (int i = 0; i < spawnedRewardEntries.Count; i++)
            {
                GameObject rewardObject = spawnedRewardEntries[i];
                if (rewardObject != null)
                {
                    UnityEngine.Object.Destroy(rewardObject);
                }
            }

            spawnedRewardEntries.Clear();
        }
    }
}
