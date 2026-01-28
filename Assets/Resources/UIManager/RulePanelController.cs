using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class RulePanelController : MonoBehaviour
{
    [System.Serializable]
    public class ElementRuleSet
    {
        public string id;
        public Toggle tabToggle;
        public Sprite[] pages;
    }

    [Header("Rule UI")]
    [SerializeField] private GameObject rulePanel;
    [SerializeField] private Image ruleImage;
    [SerializeField] private ElementRuleSet[] elementRuleSets;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button closeButton;

    [Header("Tab Switch Animation")]
    [SerializeField] private float fadeOutDuration = 0.15f;
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private float slideDuration = 0.2f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Tab Highlight")]
    [SerializeField] private float selectedTabScale = 1.05f;
    [SerializeField] private float tabScaleDuration = 0.15f;

    private int _currentElementIndex = -1;
    private int _currentPageIndex;
    private CanvasGroup _ruleImageCanvasGroup;
    private readonly Dictionary<Toggle, Vector3> _tabBaseScales = new();
    private Tween _imageTween;
    private bool _isSwitching;
    private UIFxController _fx;

    public void Initialize(UIFxController fx)
    {
        _fx = fx;
        WireButtons();
        if (rulePanel) rulePanel.SetActive(false);
        if (ruleImage != null)
        {
            _ruleImageCanvasGroup = ruleImage.GetComponent<CanvasGroup>();
            if (_ruleImageCanvasGroup == null)
            {
                _ruleImageCanvasGroup = ruleImage.gameObject.AddComponent<CanvasGroup>();
            }
            _ruleImageCanvasGroup.alpha = 1f;
        }
        CacheTabBaseScales();
        SelectFirstAvailableTab();
    }

    private void WireButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(NextPage);
        }

        if (prevButton != null)
        {
            prevButton.onClick.RemoveAllListeners();
            prevButton.onClick.AddListener(PrevPage);
        }

        if (elementRuleSets == null) return;
        for (int i = 0; i < elementRuleSets.Length; i++)
        {
            var index = i;
            var toggle = elementRuleSets[index]?.tabToggle;
            if (toggle == null) continue;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(isOn =>
            {
                if (!isOn) return;

                SelectElementTab(index);

                // ⭐關鍵：讓 Toggle 不要一直保持 ON，下一次點同一個才會再次觸發
                toggle.SetIsOnWithoutNotify(false);
            });
        }
    }

    public void Open()
    {
        if (rulePanel) _fx?.ShowPanel(rulePanel);
    }

    public void OpenAndSelect(string id)
    {
        Open();
        if (TrySelectElementById(id, logWarning: false)) return;
        SelectFirstAvailableTab();
    }

    public void Close()
    {
        if (rulePanel) _fx?.HidePanel(rulePanel);
    }

    private void OnDisable()
    {
        DOTween.Kill(this);
        if (_ruleImageCanvasGroup != null)
        {
            _ruleImageCanvasGroup.alpha = 1f;
        }
    }

    public void NextPage()
    {
        if (!TryGetCurrentPages(out var pages)) return;
        if (pages.Length == 0) return;
        int next = (_currentPageIndex + 1) % pages.Length;
        SwitchToPage(next, toRight: true);
    }

    public void PrevPage()
    {
        if (!TryGetCurrentPages(out var pages)) return;
        if (pages.Length == 0) return;
        int next = (_currentPageIndex - 1 + pages.Length) % pages.Length;
        SwitchToPage(next, toRight: false);
    }

    public void SelectElementTab(int index)
    {
        if (_isSwitching) return;
        if (elementRuleSets == null || index < 0 || index >= elementRuleSets.Length)
        {
            Debug.LogWarning($"[RulePanel] Invalid tab index: {index}");
            return;
        }

        var ruleSet = elementRuleSets[index];
        if (ruleSet == null)
        {
            Debug.LogWarning($"[RulePanel] Rule set at index {index} is null.");
            return;
        }

        if (ruleSet.pages == null || ruleSet.pages.Length == 0)
        {
            Debug.LogWarning($"[RulePanel] No pages configured for tab: {ruleSet.id}");
            return;
        }

        if (ruleImage == null)
        {
            Debug.LogWarning("[RulePanel] Rule image is missing.");
            return;
        }

        int previousIndex = _currentElementIndex;
        _currentElementIndex = index;
        _currentPageIndex = 0;
        UpdateTabHighlight(index);
        UpdateNavButtons();

        if (_fx != null)
        {
            _isSwitching = true;
            bool toRight = previousIndex < 0 || index > previousIndex;
            _fx.CrossSlideRulePage(ruleImage, ruleSet.pages[_currentPageIndex], toRight);
            DOVirtual.DelayedCall(slideDuration, () => _isSwitching = false)
                .SetUpdate(useUnscaledTime)
                .SetId(this);
            return;
        }

        _isSwitching = true;
        SwitchToSprite(ruleSet.pages[_currentPageIndex]);
    }

    public void SelectElement(string id)
    {
        TrySelectElementById(id, logWarning: true);
    }

    private void SwitchToPage(int pageIndex, bool toRight)
    {
        if (_isSwitching) return;
        if (!TryGetCurrentPages(out var pages)) return;
        if (pageIndex < 0 || pageIndex >= pages.Length) return;
        _currentPageIndex = pageIndex;

        if (_fx != null)
        {
            _isSwitching = true;
            _fx.CrossSlideRulePage(ruleImage, pages[pageIndex], toRight);
            UpdateNavButtons();
            DOVirtual.DelayedCall(slideDuration, () => _isSwitching = false)
                .SetUpdate(useUnscaledTime)
                .SetId(this);
            return;
        }

        _isSwitching = true;
        SwitchToSprite(pages[pageIndex]);
        UpdateNavButtons();
    }

    private void SwitchToSprite(Sprite nextSprite)
    {
        if (nextSprite == null || ruleImage == null)
        {
            _isSwitching = false;
            return;
        }
        if (_ruleImageCanvasGroup == null)
        {
            _ruleImageCanvasGroup = ruleImage.GetComponent<CanvasGroup>();
            if (_ruleImageCanvasGroup == null)
            {
                _ruleImageCanvasGroup = ruleImage.gameObject.AddComponent<CanvasGroup>();
            }
        }

        _imageTween?.Kill();

        if (ruleImage.sprite == nextSprite)
        {
            ruleImage.sprite = nextSprite;
            _ruleImageCanvasGroup.alpha = 1f;
            _isSwitching = false;
            return;
        }

        var sequence = DOTween.Sequence().SetUpdate(useUnscaledTime).SetId(this);
        sequence.Append(_ruleImageCanvasGroup.DOFade(0f, fadeOutDuration));
        sequence.AppendCallback(() => ruleImage.sprite = nextSprite);
        sequence.Append(_ruleImageCanvasGroup.DOFade(1f, fadeInDuration));
        sequence.OnComplete(() => _isSwitching = false);
        _imageTween = sequence;
    }

    private void UpdateTabHighlight(int selectedIndex)
    {
        if (elementRuleSets == null) return;
        for (int i = 0; i < elementRuleSets.Length; i++)
        {
            var toggle = elementRuleSets[i]?.tabToggle;
            if (toggle == null) continue;

            if (!_tabBaseScales.TryGetValue(toggle, out var baseScale))
            {
                baseScale = toggle.transform.localScale;
                _tabBaseScales[toggle] = baseScale;
            }

            float scale = i == selectedIndex ? selectedTabScale : 1f;
            var targetScale = baseScale * scale;
            DOTween.Kill(toggle.transform);
            toggle.transform.DOScale(targetScale, tabScaleDuration)
                .SetUpdate(useUnscaledTime)
                .SetId(this);
        }
    }

    private void CacheTabBaseScales()
    {
        _tabBaseScales.Clear();
        if (elementRuleSets == null) return;
        foreach (var ruleSet in elementRuleSets)
        {
            if (ruleSet?.tabToggle == null) continue;
            _tabBaseScales[ruleSet.tabToggle] = ruleSet.tabToggle.transform.localScale;
        }
    }

    private void SelectFirstAvailableTab()
    {
        if (elementRuleSets == null) return;
        foreach (var ruleSet in elementRuleSets)
        {
            if (ruleSet?.tabToggle == null) continue;
            ruleSet.tabToggle.SetIsOnWithoutNotify(false);
        }

        for (int i = 0; i < elementRuleSets.Length; i++)
        {
            var ruleSet = elementRuleSets[i];
            if (ruleSet?.tabToggle == null || ruleSet.pages == null || ruleSet.pages.Length == 0) continue;
            ruleSet.tabToggle.SetIsOnWithoutNotify(true);
            SelectElementTab(i);
            return;
        }
    }

    private bool TrySelectElementById(string id, bool logWarning)
    {
        if (string.IsNullOrWhiteSpace(id) || elementRuleSets == null) return false;
        var trimmedId = id.Trim();
        for (int i = 0; i < elementRuleSets.Length; i++)
        {
            if (elementRuleSets[i] != null && string.Equals(elementRuleSets[i].id, trimmedId, System.StringComparison.Ordinal))
            {
                SelectElementTab(i);
                return true;
            }
        }

        if (logWarning)
        {
            Debug.LogWarning($"[RulePanel] No tab found with id: {id}");
        }
        return false;
    }

    private void UpdateNavButtons()
    {
        if (_currentElementIndex < 0 || elementRuleSets == null) return;
        if (_currentElementIndex >= elementRuleSets.Length) return;
        var ruleSet = elementRuleSets[_currentElementIndex];
        if (ruleSet == null || ruleSet.pages == null || ruleSet.pages.Length == 0) return;
        var pages = ruleSet.pages;
        bool interactable = pages.Length > 1;
        if (nextButton != null) nextButton.interactable = interactable;
        if (prevButton != null) prevButton.interactable = interactable;
    }

    private bool TryGetCurrentPages(out Sprite[] pages)
    {
        pages = null;
        if (ruleImage == null)
        {
            Debug.LogWarning("[RulePanel] Rule image is missing.");
            return false;
        }

        if (elementRuleSets == null || _currentElementIndex < 0 || _currentElementIndex >= elementRuleSets.Length)
        {
            Debug.LogWarning("[RulePanel] No element tab selected.");
            return false;
        }

        var ruleSet = elementRuleSets[_currentElementIndex];
        if (ruleSet == null || ruleSet.pages == null || ruleSet.pages.Length == 0)
        {
            Debug.LogWarning($"[RulePanel] No pages configured for tab: {ruleSet?.id}");
            return false;
        }

        pages = ruleSet.pages;
        return true;
    }
}
