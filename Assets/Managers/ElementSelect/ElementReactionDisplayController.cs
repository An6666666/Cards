using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class ElementReactionDisplayController : MonoBehaviour
{
    [Serializable]
    private struct ElementVisualEntry
    {
        public ElementType element;
        public Sprite icon;
    }

    [Serializable]
    private struct ReactionVideoEntry
    {
        public ElementType a;
        public ElementType b;
        public VideoClip clip;
    }

    private struct ReactionPair : IEquatable<ReactionPair>
    {
        public ElementType first;
        public ElementType second;

        public ReactionPair(ElementType a, ElementType b)
        {
            if ((int)a <= (int)b)
            {
                first = a;
                second = b;
            }
            else
            {
                first = b;
                second = a;
            }
        }

        public bool Equals(ReactionPair other) => first == other.first && second == other.second;
        public override bool Equals(object obj) => obj is ReactionPair other && Equals(other);
        public override int GetHashCode() => ((int)first * 397) ^ (int)second;
    }

    [Header("Source")]
    [SerializeField] private StartingElementSelectionUI selectionUI;

    [Header("Top")]
    [SerializeField] private Text combinationText;
    [SerializeField] private Image firstElementIcon;
    [SerializeField] private Image secondElementIcon;

    [Header("Right")]
    [SerializeField] private Text reactionNameText;

    [Header("Bottom")]
    [SerializeField] private Text reactionDescriptionText;

    [Header("Center Video")]
    [SerializeField] private VideoPlayer reactionVideoPlayer;

    [Header("Navigation")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private GameObject navigationRoot;

    [Header("Fallback Display")]
    [SerializeField] private string emptyCombinationText = "尚未形成元素組合";
    [SerializeField] private string emptyReactionNameText = "請先選擇元素";
    [SerializeField] private string emptyReactionDescriptionText = "選取元素後，這裡會顯示對應的元素反應說明。";

    [Header("Visual Data")]
    [SerializeField] private List<ElementVisualEntry> elementVisuals = new List<ElementVisualEntry>();
    [SerializeField] private List<ReactionVideoEntry> reactionVideos = new List<ReactionVideoEntry>();

    private readonly List<ReactionPair> currentPairs = new List<ReactionPair>();
    private readonly Dictionary<ElementType, Sprite> iconLookup = new Dictionary<ElementType, Sprite>();
    private readonly Dictionary<ReactionPair, VideoClip> videoLookup = new Dictionary<ReactionPair, VideoClip>();
    private int currentIndex;

    private void Awake()
    {
        BuildLookup();

        if (previousButton != null)
            previousButton.onClick.AddListener(ShowPrevious);

        if (nextButton != null)
            nextButton.onClick.AddListener(ShowNext);
    }

    private void OnEnable()
    {
        if (selectionUI != null)
            selectionUI.SelectionChanged += HandleSelectionChanged;

        RefreshFromSelection();
    }

    private void OnDisable()
    {
        if (selectionUI != null)
            selectionUI.SelectionChanged -= HandleSelectionChanged;
    }

    private void OnDestroy()
    {
        if (previousButton != null)
            previousButton.onClick.RemoveListener(ShowPrevious);

        if (nextButton != null)
            nextButton.onClick.RemoveListener(ShowNext);
    }

    public void RefreshFromSelection()
    {
        if (selectionUI == null)
        {
            ApplyEmptyState();
            return;
        }

        RebuildPairs(selectionUI.GetSelectedElementsSnapshot());
        currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, currentPairs.Count - 1));
        RefreshDisplay();
    }

    public void ShowPrevious()
    {
        if (currentPairs.Count <= 1)
            return;

        currentIndex = (currentIndex - 1 + currentPairs.Count) % currentPairs.Count;
        RefreshDisplay();
    }

    public void ShowNext()
    {
        if (currentPairs.Count <= 1)
            return;

        currentIndex = (currentIndex + 1) % currentPairs.Count;
        RefreshDisplay();
    }

    private void BuildLookup()
    {
        iconLookup.Clear();
        for (int i = 0; i < elementVisuals.Count; i++)
        {
            ElementVisualEntry entry = elementVisuals[i];
            if (entry.icon == null || iconLookup.ContainsKey(entry.element))
                continue;

            iconLookup.Add(entry.element, entry.icon);
        }

        videoLookup.Clear();
        for (int i = 0; i < reactionVideos.Count; i++)
        {
            ReactionVideoEntry entry = reactionVideos[i];
            ReactionPair pair = new ReactionPair(entry.a, entry.b);
            if (entry.clip == null || videoLookup.ContainsKey(pair))
                continue;

            videoLookup.Add(pair, entry.clip);
        }
    }

    private void HandleSelectionChanged(IReadOnlyList<ElementType> selected)
    {
        RebuildPairs(selected);
        currentIndex = 0;
        RefreshDisplay();
    }

    private void RebuildPairs(IReadOnlyList<ElementType> selected)
    {
        currentPairs.Clear();
        if (selected == null)
            return;

        for (int i = 0; i < selected.Count; i++)
        {
            for (int j = i + 1; j < selected.Count; j++)
            {
                currentPairs.Add(new ReactionPair(selected[i], selected[j]));
            }
        }
    }

    private void RefreshDisplay()
    {
        bool hasPair = currentPairs.Count > 0;
        if (!hasPair)
        {
            ApplyEmptyState();
            return;
        }

        ReactionPair pair = currentPairs[currentIndex];
        string firstName = selectionUI.GetElementDisplayName(pair.first);
        string secondName = selectionUI.GetElementDisplayName(pair.second);

        SetText(combinationText, $"{firstName} + {secondName}");
        SetText(reactionNameText, selectionUI.GetReactionDisplayName(pair.first, pair.second));
        SetText(reactionDescriptionText, selectionUI.GetReactionDescription(pair.first, pair.second));

        SetIcon(firstElementIcon, pair.first);
        SetIcon(secondElementIcon, pair.second);
        RefreshVideo(pair);
        RefreshNavigation();
    }

    private void ApplyEmptyState()
    {
        SetText(combinationText, emptyCombinationText);
        SetText(reactionNameText, emptyReactionNameText);
        SetText(reactionDescriptionText, emptyReactionDescriptionText);

        SetImageVisible(firstElementIcon, false);
        SetImageVisible(secondElementIcon, false);
        StopVideo();
        RefreshNavigation();
    }

    private void RefreshNavigation()
    {
        bool showNavigation = currentPairs.Count > 1;

        if (navigationRoot != null)
            navigationRoot.SetActive(showNavigation);

        if (previousButton != null)
            previousButton.interactable = showNavigation;

        if (nextButton != null)
            nextButton.interactable = showNavigation;
    }

    private void RefreshVideo(ReactionPair pair)
    {
        if (reactionVideoPlayer == null)
            return;

        if (!videoLookup.TryGetValue(pair, out VideoClip clip))
        {
            StopVideo();
            return;
        }

        reactionVideoPlayer.Stop();
        reactionVideoPlayer.clip = clip;
        reactionVideoPlayer.Play();
    }

    private void StopVideo()
    {
        if (reactionVideoPlayer == null)
            return;

        reactionVideoPlayer.Stop();
        reactionVideoPlayer.clip = null;
    }

    private void SetIcon(Image target, ElementType element)
    {
        if (target == null)
            return;

        if (!iconLookup.TryGetValue(element, out Sprite icon) || icon == null)
        {
            SetImageVisible(target, false);
            return;
        }

        target.sprite = icon;
        SetImageVisible(target, true);
    }

    private static void SetImageVisible(Image target, bool visible)
    {
        if (target == null)
            return;

        target.enabled = visible;
    }

    private static void SetText(Text target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }
}
