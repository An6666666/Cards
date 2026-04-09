using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Video;

public class RulePanelController : MonoBehaviour
{
    private static readonly string[] DefaultElementOrder = { "Fire", "Water", "Thunder", "Ice", "Wood" };

    private sealed class DefaultReactionSeed
    {
        public string Id;
        public string A;
        public string B;
        public string Combination;
        public string Title;
        public string Description;
    }

    private static readonly DefaultReactionSeed[] DefaultReactionSeeds =
    {
        new() { Id = "Fire_Water", A = "Fire", B = "Water", Combination = "火 + 水", Title = "蒸發", Description = "火+水的蒸發反應，會讓後者使用的傷害增加1.5倍，並留下後者攻擊的元素。" },
        new() { Id = "Fire_Thunder", A = "Fire", B = "Thunder", Combination = "火 + 雷", Title = "超載", Description = "超載會對被攻擊的妖怪周圍的敵人，也造成0.5倍的傷害，被攻擊的妖怪會留下後者的元素。" },
        new() { Id = "Fire_Wood", A = "Fire", B = "Wood", Combination = "火 + 木", Title = "燃燒", Description = "火+木會產生燃燒的效果，會殘留火元素，燃燒持續5回合，每次造成2點傷害。" },
        new() { Id = "Fire_Ice", A = "Fire", B = "Ice", Combination = "火 + 冰", Title = "融化", Description = "火+冰的融化反應，會讓後者使用的傷害增加1.5倍，被攻擊的妖怪會留下後者的元素。" },
        new() { Id = "Water_Thunder", A = "Water", B = "Thunder", Combination = "水 + 雷", Title = "導電", Description = "先使用水元素的特性讓鋪設好環境，再用雷元素造成導電的反應。" },
        new() { Id = "Water_Wood", A = "Water", B = "Wood", Combination = "水 + 木", Title = "生長", Description = "生長反應會在棋盤上生長出荊棘，荊棘會在玩家結束回合時，攻擊在荊棘格上的妖怪造成3點傷害。" },
        new() { Id = "Water_Ice", A = "Water", B = "Ice", Combination = "水 + 冰", Title = "冰凍", Description = "冰+水的冰凍反應，可以冰凍妖怪，讓妖怪無法移動和攻擊。" },
        new() { Id = "Thunder_Wood", A = "Thunder", B = "Wood", Combination = "雷 + 木", Title = "雷擊", Description = "雷+木的雷擊反應，會讓接下來造成的傷害變成2倍。" },
        new() { Id = "Thunder_Ice", A = "Thunder", B = "Ice", Combination = "雷 + 冰", Title = "超導", Description = "雷+冰的超導反應，會讓後攻擊的傷害增加6點。" },
        new() { Id = "Wood_Ice", A = "Wood", B = "Ice", Combination = "木 + 冰", Title = "結霜", Description = "冰+木的結霜反應，會讓接下來造成的傷害額外增加2點傷害，最多可以疊加6層，每玩家回合開始時會減少1層。" },
    };

    [Serializable]
    public class ElementTab
    {
        public string id;
        public Button tabButton;
        public Toggle tabToggle;
        [FormerlySerializedAs("previewSprite")]
        public Sprite centerIcon;
    }

    [Serializable]
    public class ReactionEntry
    {
        public string id;
        public string primaryElementId;
        public string secondaryElementId;
        public Sprite leftIcon;
        public Sprite rightIcon;
        public string combinationText;
        public string titleText;
        [TextArea(2, 8)] public string descriptionText;
        public VideoClip videoClip;

        public bool ContainsElement(string elementId)
        {
            if (string.IsNullOrWhiteSpace(elementId))
                return false;

            return string.Equals(primaryElementId, elementId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(secondaryElementId, elementId, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Header("Panel")]
    [SerializeField] private GameObject rulePanel;
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private Button closeButton;
    [FormerlySerializedAs("prevButton")]
    [SerializeField] private Button previousEntryButton;
    [FormerlySerializedAs("nextButton")]
    [SerializeField] private Button nextEntryButton;

    [Header("Right Content")]
    [FormerlySerializedAs("previewImage")]
    [SerializeField] private Image centerIconImage;
    [SerializeField] private Image leftMixImage;
    [SerializeField] private Image rightMixImage;
    [FormerlySerializedAs("combinationDisplayText")]
    [SerializeField] private Text combinationText;
    [FormerlySerializedAs("titleDisplayText")]
    [SerializeField] private Text titleText;
    [FormerlySerializedAs("descriptionDisplayText")]
    [SerializeField] private Text descriptionText;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject videoRoot;

    [Header("Empty State")]
    [SerializeField] private string unmetCombinationText = string.Empty;
    [SerializeField] private string unmetTitleText = "未達成條件";
    [TextArea(2, 6)]
    [SerializeField] private string unmetDescriptionText = "請至少選擇 2 個元素以查看元素反應。";

    [Header("Data")]
    [SerializeField] private ElementTab[] elementTabs;
    [SerializeField] private ReactionEntry[] reactionEntries;

    [Header("Animation")]
    [SerializeField] private float fadeOutDuration = 0.15f;
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Tab Highlight")]
    [SerializeField] private Color selectedTabColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color normalTabColor = Color.white;
    [SerializeField] private float tabColorDuration = 0.15f;

    private readonly Dictionary<string, Button> _autoButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ReactionEntry> _filteredEntries = new();
    private readonly HashSet<string> _selectedElementIds = new(StringComparer.OrdinalIgnoreCase);

    private UIFxController _fx;
    private CanvasGroup _centerIconCanvasGroup;
    private Tween _iconTween;
    private int _currentEntryIndex;

    private Sprite _defaultCenterIcon;
    private Sprite _defaultLeftIcon;
    private Sprite _defaultRightIcon;
    private string _defaultCombinationText;
    private string _defaultTitleText;
    private string _defaultDescriptionText;

    public void Initialize(UIFxController fx)
    {
        _fx = fx;
        AutoBindSceneReferences();
        EnsureDefaultData();
        CacheDefaultContent();
        WireButtons();

        if (rulePanel != null)
            rulePanel.SetActive(false);

        if (contentRoot != null)
            contentRoot.SetActive(false);

        EnsureCenterIconCanvasGroup();
        ResetSelection();
    }

    public void Open()
    {
        ResetSelection();

        if (rulePanel != null)
        {
            if (_fx != null) _fx.ShowPanel(rulePanel);
            else rulePanel.SetActive(true);
        }

        if (contentRoot != null)
            contentRoot.SetActive(true);
    }

    public void OpenAndSelect(string id)
    {
        Open();
        ToggleElementById(id, false);
    }

    public void Close()
    {
        StopVideo();

        if (contentRoot != null)
            contentRoot.SetActive(false);

        if (rulePanel != null)
        {
            if (_fx != null) _fx.HidePanel(rulePanel);
            else rulePanel.SetActive(false);
        }

        ResetSelection();
    }

    public void SelectElement(string id)
    {
        ToggleElementById(id, true);
    }

    public void SelectElementTab(int index)
    {
        if (elementTabs == null || index < 0 || index >= elementTabs.Length)
            return;

        ElementTab tab = elementTabs[index];
        if (tab == null)
            return;

        ToggleElementById(tab.id, true);
    }

    public void ClearSelections()
    {
        ResetSelection();
    }

    private void ToggleElementById(string id, bool logWarning)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        ElementTab tab = FindElementTabById(id);
        if (tab == null)
        {
            if (logWarning)
                Debug.LogWarning($"[RulePanel] No tab found with id: {id}");
            return;
        }

        if (!_selectedElementIds.Add(tab.id))
            _selectedElementIds.Remove(tab.id);

        _currentEntryIndex = 0;
        RebuildFilteredEntries();
        ApplyCurrentEntry();
        UpdateTabHighlight();
        UpdateEntryNavigation();
    }

    public void SelectNextEntry()
    {
        MoveEntry(1);
    }

    public void SelectPreviousEntry()
    {
        MoveEntry(-1);
    }

    private void AutoBindSceneReferences()
    {
        if (rulePanel == null)
            rulePanel = gameObject;

        Transform searchRoot = rulePanel != null && rulePanel.transform.parent != null
            ? rulePanel.transform.parent
            : transform.parent;

        if (contentRoot == null)
            contentRoot = FindChildGameObject(searchRoot, "E B");

        if (contentRoot == null)
            return;

        if (centerIconImage == null)
            centerIconImage = FindImage(contentRoot.transform, "E");

        if (leftMixImage == null)
            leftMixImage = FindImage(contentRoot.transform, "mix L");

        if (rightMixImage == null)
            rightMixImage = FindImage(contentRoot.transform, "mix  R") ?? FindImage(contentRoot.transform, "mix R");

        if (combinationText == null)
            combinationText = FindText(contentRoot.transform, "1+1Text");

        if (titleText == null)
            titleText = FindText(contentRoot.transform, "mix text");

        if (descriptionText == null)
            descriptionText = FindText(contentRoot.transform, "LeftInfoText");

        if (previousEntryButton == null)
            previousEntryButton = FindButton(contentRoot.transform, "Prev");

        if (nextEntryButton == null)
            nextEntryButton = FindButton(contentRoot.transform, "Next");

        if (videoPlayer == null)
            videoPlayer = contentRoot.GetComponentInChildren<VideoPlayer>(true);

        if (videoRoot == null && videoPlayer != null)
            videoRoot = videoPlayer.gameObject;

        _autoButtons.Clear();
        CollectButtons(contentRoot.transform);
    }

    private void CacheDefaultContent()
    {
        _defaultCenterIcon = centerIconImage != null ? centerIconImage.sprite : null;
        _defaultLeftIcon = leftMixImage != null ? leftMixImage.sprite : null;
        _defaultRightIcon = rightMixImage != null ? rightMixImage.sprite : null;
        _defaultCombinationText = combinationText != null ? combinationText.text : string.Empty;
        _defaultTitleText = titleText != null ? titleText.text : string.Empty;
        _defaultDescriptionText = descriptionText != null ? descriptionText.text : string.Empty;
    }

    private void EnsureDefaultData()
    {
        if (elementTabs == null || elementTabs.Length == 0)
            BuildDefaultElementTabs();

        if (reactionEntries == null || reactionEntries.Length == 0)
            BuildDefaultReactionEntries();
    }

    private void BuildDefaultElementTabs()
    {
        List<ElementTab> tabs = new();
        foreach (string elementId in DefaultElementOrder)
        {
            Button button = FindButtonByElementId(elementId);
            tabs.Add(new ElementTab
            {
                id = elementId,
                tabButton = button,
                centerIcon = FindElementSprite(elementId, button)
            });
        }

        elementTabs = tabs.ToArray();
    }

    private void BuildDefaultReactionEntries()
    {
        List<ReactionEntry> entries = new();
        foreach (DefaultReactionSeed seed in DefaultReactionSeeds)
        {
            entries.Add(new ReactionEntry
            {
                id = seed.Id,
                primaryElementId = seed.A,
                secondaryElementId = seed.B,
                leftIcon = FindElementSprite(seed.A),
                rightIcon = FindElementSprite(seed.B),
                combinationText = seed.Combination,
                titleText = seed.Title,
                descriptionText = seed.Description,
                videoClip = null
            });
        }

        reactionEntries = entries.ToArray();
    }

    private Sprite FindElementSprite(string elementId, Button knownButton = null)
    {
        Button button = knownButton ?? FindButtonByElementId(elementId);
        if (button == null)
            return null;

        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null && buttonImage.sprite != null)
            return buttonImage.sprite;

        Image childImage = button.GetComponentInChildren<Image>(true);
        return childImage != null ? childImage.sprite : null;
    }

    private void WireButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (previousEntryButton != null)
        {
            previousEntryButton.onClick.RemoveAllListeners();
            previousEntryButton.onClick.AddListener(SelectPreviousEntry);
        }

        if (nextEntryButton != null)
        {
            nextEntryButton.onClick.RemoveAllListeners();
            nextEntryButton.onClick.AddListener(SelectNextEntry);
        }

        if (elementTabs == null)
            return;

        for (int i = 0; i < elementTabs.Length; i++)
        {
            int index = i;
            ElementTab tab = elementTabs[i];
            if (tab == null)
                continue;

            Button button = tab.tabButton != null ? tab.tabButton : FindButtonByElementId(tab.id);
            if (button != null)
            {
                tab.tabButton = button;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectElementTab(index));
            }

            if (tab.tabToggle != null)
            {
                Toggle toggle = tab.tabToggle;
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener(isOn =>
                {
                    if (!isOn)
                        return;

                    SelectElementTab(index);
                    toggle.SetIsOnWithoutNotify(false);
                });
            }
        }
    }

    private void ResetSelection()
    {
        _currentEntryIndex = 0;
        _filteredEntries.Clear();
        _selectedElementIds.Clear();
        ApplyCurrentEntry();
        UpdateTabHighlight();
        UpdateEntryNavigation();
    }

    private void RebuildFilteredEntries()
    {
        _filteredEntries.Clear();
        if (reactionEntries == null || _selectedElementIds.Count < 2)
            return;

        foreach (ReactionEntry entry in reactionEntries)
        {
            if (entry == null)
                continue;

            if (_selectedElementIds.Contains(entry.primaryElementId) &&
                _selectedElementIds.Contains(entry.secondaryElementId))
                _filteredEntries.Add(entry);
        }
    }

    private void ApplyCurrentEntry()
    {
        if (_selectedElementIds.Count < 2 || _filteredEntries.Count == 0)
        {
            ApplyUnmetContent();
            UpdateEntryNavigation();
            return;
        }

        ReactionEntry entry = _filteredEntries[Mathf.Clamp(_currentEntryIndex, 0, _filteredEntries.Count - 1)];
        ApplyReactionEntry(entry);
    }

    private void ApplyReactionEntry(ReactionEntry entry)
    {
        if (entry == null)
        {
            ApplyUnmetContent();
            return;
        }

        SwitchCenterIcon(_defaultCenterIcon);
        SetImage(leftMixImage, entry.leftIcon != null ? entry.leftIcon : _defaultLeftIcon);
        SetImage(rightMixImage, entry.rightIcon != null ? entry.rightIcon : _defaultRightIcon);
        SetText(combinationText, string.IsNullOrWhiteSpace(entry.combinationText) ? _defaultCombinationText : entry.combinationText);
        SetText(titleText, string.IsNullOrWhiteSpace(entry.titleText) ? _defaultTitleText : entry.titleText);
        SetText(descriptionText, string.IsNullOrWhiteSpace(entry.descriptionText) ? _defaultDescriptionText : entry.descriptionText);
        PlayVideo(entry.videoClip);
    }

    private void ApplyUnmetContent()
    {
        SwitchCenterIcon(_defaultCenterIcon);
        SetImage(leftMixImage, _defaultLeftIcon);
        SetImage(rightMixImage, _defaultRightIcon);
        SetText(combinationText, unmetCombinationText);
        SetText(titleText, unmetTitleText);
        SetText(descriptionText, unmetDescriptionText);
        StopVideo();
    }

    private void MoveEntry(int delta)
    {
        if (_filteredEntries.Count <= 1)
        {
            UpdateEntryNavigation();
            return;
        }

        int nextIndex = Mathf.Clamp(_currentEntryIndex + delta, 0, _filteredEntries.Count - 1);
        if (nextIndex == _currentEntryIndex)
        {
            UpdateEntryNavigation();
            return;
        }

        _currentEntryIndex = nextIndex;
        ApplyCurrentEntry();
        UpdateEntryNavigation();
    }

    private void SwitchCenterIcon(Sprite nextSprite)
    {
        if (centerIconImage == null)
            return;

        EnsureCenterIconCanvasGroup();
        _iconTween?.Kill();

        if (nextSprite == null)
        {
            centerIconImage.enabled = false;
            _centerIconCanvasGroup.alpha = 1f;
            return;
        }

        centerIconImage.enabled = true;

        if (centerIconImage.sprite == nextSprite)
        {
            centerIconImage.sprite = nextSprite;
            _centerIconCanvasGroup.alpha = 1f;
            return;
        }

        Sequence sequence = DOTween.Sequence().SetUpdate(useUnscaledTime).SetId(this);
        sequence.Append(_centerIconCanvasGroup.DOFade(0f, fadeOutDuration));
        sequence.AppendCallback(() => centerIconImage.sprite = nextSprite);
        sequence.Append(_centerIconCanvasGroup.DOFade(1f, fadeInDuration));
        _iconTween = sequence;
    }

    private void PlayVideo(VideoClip clip)
    {
        if (videoPlayer == null)
            return;

        videoPlayer.Stop();
        videoPlayer.clip = clip;

        bool hasClip = clip != null;
        if (videoRoot != null)
            videoRoot.SetActive(hasClip);

        if (hasClip)
            videoPlayer.Play();
    }

    private void StopVideo()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.Stop();
        videoPlayer.clip = null;

        if (videoRoot != null)
            videoRoot.SetActive(false);
    }

    private void UpdateTabHighlight()
    {
        if (elementTabs == null)
            return;

        for (int i = 0; i < elementTabs.Length; i++)
        {
            ElementTab tab = elementTabs[i];
            if (tab == null)
                continue;

            bool isSelected = _selectedElementIds.Contains(tab.id);
            Color targetColor = isSelected ? selectedTabColor : normalTabColor;

            if (tab.tabButton != null)
                TweenGraphicColor(tab.tabButton.targetGraphic as Graphic, targetColor);

            if (tab.tabToggle != null)
                TweenGraphicColor(tab.tabToggle.targetGraphic as Graphic, targetColor);
        }
    }

    private void EnsureCenterIconCanvasGroup()
    {
        if (centerIconImage == null)
            return;

        _centerIconCanvasGroup = centerIconImage.GetComponent<CanvasGroup>();
        if (_centerIconCanvasGroup == null)
            _centerIconCanvasGroup = centerIconImage.gameObject.AddComponent<CanvasGroup>();

        _centerIconCanvasGroup.alpha = 1f;
    }

    private Button FindButtonByElementId(string elementId)
    {
        if (string.IsNullOrWhiteSpace(elementId))
            return null;

        string normalized = elementId.Trim();
        if (_autoButtons.TryGetValue(normalized, out Button button))
            return button;

        string lower = normalized.ToLowerInvariant();
        if (_autoButtons.TryGetValue(lower, out Button lowerButton))
            return lowerButton;

        return null;
    }

    private ElementTab FindElementTabById(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || elementTabs == null)
            return null;

        foreach (ElementTab tab in elementTabs)
        {
            if (tab != null && string.Equals(tab.id, id, StringComparison.OrdinalIgnoreCase))
                return tab;
        }

        return null;
    }

    private void UpdateEntryNavigation()
    {
        if (previousEntryButton != null)
            previousEntryButton.interactable = _filteredEntries.Count > 1 && _currentEntryIndex > 0;

        if (nextEntryButton != null)
            nextEntryButton.interactable = _filteredEntries.Count > 1 && _currentEntryIndex < _filteredEntries.Count - 1;
    }

    private void CollectButtons(Transform root)
    {
        if (root == null)
            return;

        Button button = root.GetComponent<Button>();
        if (button != null && !_autoButtons.ContainsKey(root.name))
            _autoButtons.Add(root.name, button);

        for (int i = 0; i < root.childCount; i++)
            CollectButtons(root.GetChild(i));
    }

    private static GameObject FindChildGameObject(Transform root, string name)
    {
        Transform found = FindChildRecursive(root, name);
        return found != null ? found.gameObject : null;
    }

    private static Image FindImage(Transform root, string name)
    {
        Transform found = FindChildRecursive(root, name);
        return found != null ? found.GetComponent<Image>() : null;
    }

    private static Button FindButton(Transform root, string name)
    {
        Transform found = FindChildRecursive(root, name);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private static Text FindText(Transform root, string name)
    {
        Transform found = FindChildRecursive(root, name);
        return found != null ? found.GetComponent<Text>() : null;
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null)
            return null;

        if (string.Equals(root.name, name, StringComparison.Ordinal))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private void TweenGraphicColor(Graphic target, Color color)
    {
        if (target == null)
            return;

        DOTween.Kill(target);
        target.DOColor(color, tabColorDuration)
            .SetUpdate(useUnscaledTime)
            .SetId(this);
    }

    private static void SetText(Text target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }

    private static void SetImage(Image target, Sprite sprite)
    {
        if (target == null)
            return;

        target.sprite = sprite;
        target.enabled = sprite != null;
    }

    private void OnDisable()
    {
        DOTween.Kill(this);
        if (_centerIconCanvasGroup != null)
            _centerIconCanvasGroup.alpha = 1f;
    }
}
