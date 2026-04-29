using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class IllustratedBookPanelController : MonoBehaviour
{
    public enum BookPage
    {
        [InspectorName("Cards")] Cards,
        [InspectorName("Monsters")] Monsters,
        [InspectorName("Relics")] Relics
    }

    [Serializable]
    public class UITextBinding
    {
        [SerializeField, InspectorName("Legacy Text")] private Text legacyText;
        [SerializeField, InspectorName("TMP Text")] private TMP_Text tmpText;

        public void SetText(string value)
        {
            string safe = value ?? string.Empty;
            if (legacyText != null) legacyText.text = safe;
            if (tmpText != null) tmpText.text = safe;
        }
    }

    [Serializable]
    public class IllustratedBookSlotBinding
    {
        [SerializeField, InspectorName("Root")] private GameObject root;
        [SerializeField, InspectorName("Button")] private Button button;
        [SerializeField, InspectorName("Icon Image")] private Image iconImage;
        [SerializeField, InspectorName("Name Text")] private UITextBinding nameText;
        [SerializeField, InspectorName("Filled Visual")] private GameObject filledVisual;
        [SerializeField, InspectorName("Empty Visual")] private GameObject emptyVisual;

        public GameObject Root => root;
        public Button Button => button;

        public void SetFilled(Sprite icon, string displayName)
        {
            if (root != null) root.SetActive(true);

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (nameText != null) nameText.SetText(displayName);
            if (filledVisual != null) filledVisual.SetActive(true);
            if (emptyVisual != null) emptyVisual.SetActive(false);
            if (button != null) button.interactable = true;
        }

        public void SetEmpty()
        {
            if (root != null) root.SetActive(true);

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (nameText != null) nameText.SetText(string.Empty);
            if (filledVisual != null) filledVisual.SetActive(false);
            if (emptyVisual != null) emptyVisual.SetActive(true);
            if (button != null) button.interactable = false;
        }

        public void ClearClick()
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        public void BindClick(UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }

    [Serializable]
    public class CardDetailBindings
    {
        [SerializeField, InspectorName("Root")] private GameObject root;
        [SerializeField, InspectorName("Name Text")] private UITextBinding nameText;
        [SerializeField, InspectorName("Card Image")] private Image cardImage;
        [SerializeField, InspectorName("Area Image")] private Image areaImage;
        [SerializeField, InspectorName("Intro Text")] private UITextBinding introText;
        [SerializeField, InspectorName("Concept Text")] private UITextBinding conceptText;
        [SerializeField, InspectorName("Previous Variant Button")] private Button previousVariantButton;
        [SerializeField, InspectorName("Next Variant Button")] private Button nextVariantButton;

        public GameObject Root => root;
        public Button PreviousVariantButton => previousVariantButton;
        public Button NextVariantButton => nextVariantButton;

        public void Fill(CardBookData data)
        {
            if (data == null) return;

            if (nameText != null) nameText.SetText(data.DisplayName);
            if (introText != null) introText.SetText(data.Intro);
            if (conceptText != null) conceptText.SetText(data.Concept);

            SetImage(cardImage, data.CardSprite);
            SetImage(areaImage, data.AreaSprite);
        }

        public void BindVariantButtons(UnityEngine.Events.UnityAction previousAction, UnityEngine.Events.UnityAction nextAction)
        {
            EnsureVariantButtons();
            BindButton(previousVariantButton, previousAction);
            BindButton(nextVariantButton, nextAction);
        }

        public void SetVariantNavigation(bool visible, bool canPrevious, bool canNext)
        {
            EnsureVariantButtons();

            if (previousVariantButton != null)
            {
                previousVariantButton.gameObject.SetActive(visible);
                previousVariantButton.interactable = canPrevious;
            }

            if (nextVariantButton != null)
            {
                nextVariantButton.gameObject.SetActive(visible);
                nextVariantButton.interactable = canNext;
            }
        }

        private void EnsureVariantButtons()
        {
            if (root == null || (previousVariantButton != null && nextVariantButton != null)) return;

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null) continue;

                string buttonName = button.name;
                if (previousVariantButton == null &&
                    (buttonName.IndexOf("prev", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     buttonName.IndexOf("previous", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    previousVariantButton = button;
                    continue;
                }

                if (nextVariantButton == null &&
                    buttonName.IndexOf("next", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nextVariantButton = button;
                }
            }

        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static void SetImage(Image target, Sprite sprite)
        {
            if (target == null) return;
            target.sprite = sprite;
            target.enabled = sprite != null;
        }
    }

    [Serializable]
    public class MonsterSkillData
    {
        [SerializeField, InspectorName("Icon")] private Sprite icon;
        [SerializeField, InspectorName("Skill Name")] private string skillName;
        [TextArea(2, 6)]
        [SerializeField, InspectorName("Description")] private string description;

        public Sprite Icon => icon;
        public string SkillName => skillName;
        public string Description => description;
    }

    [Serializable]
    public class MonsterDetailBindings
    {
        [SerializeField, InspectorName("Root")] private GameObject root;
        [SerializeField, InspectorName("Name Text")] private UITextBinding nameText;
        [SerializeField, InspectorName("Portrait Image")] private Image portraitImage;
        [SerializeField, InspectorName("Animation Root")] private GameObject animationRoot;
        [SerializeField, InspectorName("Animation Parent")] private Transform animationParent;
        [SerializeField, InspectorName("Previous Preview Button")] private Button previousPreviewButton;
        [SerializeField, InspectorName("Next Preview Button")] private Button nextPreviewButton;
        [SerializeField, InspectorName("Intro Text")] private UITextBinding introText;
        [SerializeField, InspectorName("Legacy Skill Text")] private UITextBinding skillText;
        [SerializeField, InspectorName("Tip Text")] private UITextBinding tipText;
        [SerializeField, InspectorName("Skill Item Prefab")] private MonsterSkillItemBindings skillItemPrefab;
        [SerializeField, InspectorName("Skill List Parent")] private Transform skillListParent;

        private readonly List<MonsterSkillItemBindings> _spawnedSkillItems = new List<MonsterSkillItemBindings>();
        private readonly List<MonsterPreviewEntryData> _availablePreviewEntries = new List<MonsterPreviewEntryData>(8);
        private GameObject _spawnedAnimationInstance;
        private Animator _spawnedAnimationAnimator;
        private int _currentPreviewIndex;
        private MonsterBookData _lastBoundMonsterData;

        public GameObject Root => root;

        public void Fill(MonsterBookData data)
        {
            if (data == null) return;

            if (nameText != null) nameText.SetText(data.DisplayName);
            if (introText != null) introText.SetText(data.Intro);
            if (tipText != null) tipText.SetText(data.Tip);
            if (skillText != null) skillText.SetText(string.Empty);

            RefreshPreviewContent(data);

            FillSkills(data.Skills);
        }

        public void Clear()
        {
            _lastBoundMonsterData = null;
            _availablePreviewEntries.Clear();
            _currentPreviewIndex = 0;

            ClearSkillItems();
            DestroyAnimationInstance();
        }

        public void ClearSkillItems()
        {
            for (int i = 0; i < _spawnedSkillItems.Count; i++)
            {
                MonsterSkillItemBindings item = _spawnedSkillItems[i];
                if (item == null) continue;

                if (Application.isPlaying) UnityEngine.Object.Destroy(item.gameObject);
                else UnityEngine.Object.DestroyImmediate(item.gameObject);
            }

            _spawnedSkillItems.Clear();
        }

        public void FillSkills(List<MonsterSkillData> skills)
        {
            ClearSkillItems();

            if (skillItemPrefab == null || skillListParent == null || skills == null || skills.Count == 0)
            {
                return;
            }

            for (int i = 0; i < skills.Count; i++)
            {
                MonsterSkillData skill = skills[i];
                if (skill == null) continue;

                MonsterSkillItemBindings item = UnityEngine.Object.Instantiate(skillItemPrefab, skillListParent);
                if (item == null) continue;

                item.Fill(skill);
                _spawnedSkillItems.Add(item);
            }
        }

        public void BindPreviewButtons()
        {
            BindButton(previousPreviewButton, ShowPreviousPreview);
            BindButton(nextPreviewButton, ShowNextPreview);
            UpdatePreviewButtonState();
        }

        private void RefreshPreviewContent(MonsterBookData data)
        {
            _lastBoundMonsterData = data;
            _availablePreviewEntries.Clear();

            if (data != null && data.PreviewEntries != null)
            {
                for (int i = 0; i < data.PreviewEntries.Count; i++)
                {
                    MonsterPreviewEntryData entry = data.PreviewEntries[i];
                    if (entry == null || !entry.IsConfigured(data.VisualPrefab, data.Illustration))
                    {
                        continue;
                    }

                    _availablePreviewEntries.Add(entry);
                }
            }

            if (_availablePreviewEntries.Count == 0)
            {
                if (data != null && data.Illustration != null)
                {
                    _availablePreviewEntries.Add(MonsterPreviewEntryData.CreateIllustrationFallback());
                }

                if (data != null && data.VisualPrefab != null)
                {
                    _availablePreviewEntries.Add(MonsterPreviewEntryData.CreateAnimationFallback());
                }
            }

            _currentPreviewIndex = 0;
            ApplyPreview(data);
        }

        private void ShowPreviousPreview()
        {
            ChangePreview(-1);
        }

        private void ShowNextPreview()
        {
            ChangePreview(1);
        }

        private void ChangePreview(int delta)
        {
            if (_availablePreviewEntries.Count <= 1)
            {
                UpdatePreviewButtonState();
                return;
            }

            _currentPreviewIndex = (_currentPreviewIndex + delta + _availablePreviewEntries.Count) % _availablePreviewEntries.Count;
            ApplyPreview(null);
        }

        private void ApplyPreview(MonsterBookData data)
        {
            MonsterBookData resolvedData = data ?? _lastBoundMonsterData;
            _lastBoundMonsterData = resolvedData;
            MonsterPreviewEntryData activeEntry = resolvedData != null && _availablePreviewEntries.Count > 0
                ? _availablePreviewEntries[Mathf.Clamp(_currentPreviewIndex, 0, _availablePreviewEntries.Count - 1)]
                : null;

            bool showIllustration = activeEntry != null
                && activeEntry.EntryType == MonsterPreviewEntryType.Illustration
                && resolvedData != null
                && resolvedData.Illustration != null;
            bool showAnimation = activeEntry != null
                && activeEntry.EntryType == MonsterPreviewEntryType.AnimationState
                && resolvedData != null
                && resolvedData.VisualPrefab != null;

            if (portraitImage != null)
            {
                portraitImage.sprite = showIllustration ? resolvedData.Illustration : null;
                portraitImage.enabled = showIllustration;
                portraitImage.gameObject.SetActive(showIllustration);
            }

            if (animationRoot != null)
            {
                animationRoot.SetActive(showAnimation);
            }

            if (showAnimation)
            {
                EnsureAnimationInstance(resolvedData.VisualPrefab);
                PlayPreviewEntry(activeEntry);
            }
            else
            {
                SetAnimationInstanceActive(false);
            }

            UpdatePreviewButtonState();
        }

        private void EnsureAnimationInstance(GameObject visualPrefab)
        {
            if (visualPrefab == null)
            {
                DestroyAnimationInstance();
                return;
            }

            if (_spawnedAnimationInstance != null && PrefabMatches(_spawnedAnimationInstance, visualPrefab))
            {
                SetAnimationInstanceActive(true);
                return;
            }

            DestroyAnimationInstance();

            Transform parent = animationParent != null
                ? animationParent
                : (animationRoot != null ? animationRoot.transform : null);
            if (parent == null)
            {
                return;
            }

            _spawnedAnimationInstance = UnityEngine.Object.Instantiate(visualPrefab, parent);
            _spawnedAnimationInstance.transform.localPosition = Vector3.zero;
            _spawnedAnimationInstance.transform.localRotation = Quaternion.identity;
            _spawnedAnimationInstance.transform.localScale = Vector3.one;
            _spawnedAnimationAnimator = _spawnedAnimationInstance.GetComponent<Animator>();
            if (_spawnedAnimationAnimator == null)
            {
                _spawnedAnimationAnimator = _spawnedAnimationInstance.GetComponentInChildren<Animator>(true);
            }

            SetAnimationInstanceActive(true);
        }

        private void SetAnimationInstanceActive(bool active)
        {
            if (_spawnedAnimationInstance == null)
            {
                return;
            }

            _spawnedAnimationInstance.SetActive(active);
        }

        private void DestroyAnimationInstance()
        {
            if (_spawnedAnimationInstance == null)
            {
                return;
            }

            if (Application.isPlaying) UnityEngine.Object.Destroy(_spawnedAnimationInstance);
            else UnityEngine.Object.DestroyImmediate(_spawnedAnimationInstance);

            _spawnedAnimationInstance = null;
            _spawnedAnimationAnimator = null;
        }

        private void UpdatePreviewButtonState()
        {
            bool canSwitch = _availablePreviewEntries.Count > 1;

            if (previousPreviewButton != null)
            {
                previousPreviewButton.gameObject.SetActive(canSwitch);
                previousPreviewButton.interactable = canSwitch;
            }

            if (nextPreviewButton != null)
            {
                nextPreviewButton.gameObject.SetActive(canSwitch);
                nextPreviewButton.interactable = canSwitch;
            }
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void PlayPreviewEntry(MonsterPreviewEntryData entry)
        {
            if (entry == null || entry.EntryType != MonsterPreviewEntryType.AnimationState)
            {
                return;
            }

            if (_spawnedAnimationAnimator == null)
            {
                return;
            }

            _spawnedAnimationInstance.transform.localScale = entry.PreviewScale;

            if (string.IsNullOrWhiteSpace(entry.StateName))
            {
                _spawnedAnimationAnimator.Rebind();
                _spawnedAnimationAnimator.Update(0f);
                if (_spawnedAnimationAnimator.layerCount > 0)
                {
                    _spawnedAnimationAnimator.Play(0, 0, 0f);
                    _spawnedAnimationAnimator.Update(0f);
                }

                return;
            }

            _spawnedAnimationAnimator.Rebind();
            _spawnedAnimationAnimator.Update(0f);
            _spawnedAnimationAnimator.Play(entry.StateName.Trim(), 0, 0f);
            _spawnedAnimationAnimator.Update(0f);
        }

        private static bool PrefabMatches(GameObject instance, GameObject prefab)
        {
            if (instance == null || prefab == null)
            {
                return false;
            }

            string instanceName = instance.name.Replace("(Clone)", string.Empty).Trim();
            return string.Equals(instanceName, prefab.name, StringComparison.Ordinal);
        }
    }

    public enum MonsterPreviewEntryType
    {
        Illustration,
        AnimationState
    }

    [Serializable]
    public class MonsterPreviewEntryData
    {
        [SerializeField, InspectorName("Label")] private string label;
        [SerializeField, InspectorName("Type")] private MonsterPreviewEntryType entryType = MonsterPreviewEntryType.AnimationState;
        [SerializeField, InspectorName("State Name")] private string stateName;
        [SerializeField, InspectorName("Preview Scale")] private Vector3 previewScale = Vector3.one;

        public string Label => label;
        public MonsterPreviewEntryType EntryType => entryType;
        public string StateName => stateName;
        public Vector3 PreviewScale => previewScale == Vector3.zero ? Vector3.one : previewScale;

        public bool IsConfigured(GameObject visualPrefab, Sprite illustration)
        {
            return entryType switch
            {
                MonsterPreviewEntryType.Illustration => illustration != null,
                MonsterPreviewEntryType.AnimationState => visualPrefab != null,
                _ => false
            };
        }

        public static MonsterPreviewEntryData CreateIllustrationFallback()
        {
            return new MonsterPreviewEntryData
            {
                label = "立繪",
                entryType = MonsterPreviewEntryType.Illustration
            };
        }

        public static MonsterPreviewEntryData CreateAnimationFallback()
        {
            return new MonsterPreviewEntryData
            {
                label = "動畫",
                entryType = MonsterPreviewEntryType.AnimationState
            };
        }
    }

    [Serializable]
    public class RelicDetailBindings
    {
        [SerializeField, InspectorName("Root")] private GameObject root;
        [SerializeField, InspectorName("Name Text")] private UITextBinding nameText;
        [SerializeField, InspectorName("Portrait Image")] private Image portraitImage;
        [SerializeField, InspectorName("Intro Text")] private UITextBinding introText;
        [SerializeField, InspectorName("Concept Text")] private UITextBinding conceptText;
        [SerializeField, InspectorName("Area Image")] private Image areaImage;

        public GameObject Root => root;

        public void Fill(RelicBookData data)
        {
            if (data == null) return;

            if (nameText != null) nameText.SetText(data.DisplayName);
            if (introText != null) introText.SetText(data.Intro);
            if (conceptText != null) conceptText.SetText(data.Concept);

            if (portraitImage != null)
            {
                portraitImage.sprite = data.Portrait;
                portraitImage.enabled = data.Portrait != null;
            }

            if (areaImage != null)
            {
                areaImage.sprite = data.AreaSprite;
                areaImage.enabled = data.AreaSprite != null;
            }
        }
    }

    [Serializable]
    public class PaginationBindings
    {
        [SerializeField, InspectorName("Prev Button")] private Button prevButton;
        [SerializeField, InspectorName("Next Button")] private Button nextButton;
        [SerializeField, InspectorName("Page Text")] private UITextBinding pageText;

        public Button PrevButton => prevButton;
        public Button NextButton => nextButton;
        public UITextBinding PageText => pageText;
    }

    [Serializable]
    public class BookDataBase
    {
        [SerializeField, InspectorName("ID")] private string id;
        [SerializeField, InspectorName("Display Name")] private string displayName;
        [SerializeField, InspectorName("Icon")] private Sprite icon;
        [SerializeField, InspectorName("Portrait")] private Sprite portrait;
        [SerializeField, InspectorName("Pinned")] private bool pinned;
        [SerializeField, InspectorName("Order")] private int order;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public Sprite Icon => icon;
        public Sprite Portrait => portrait;
        public bool Pinned => pinned;
        public int Order => order;
    }

    [Serializable]
    public class CardBookData : BookDataBase
    {
        [SerializeField, InspectorName("Card Kind")] private string cardKind;
        [SerializeField, InspectorName("Element ID")] private string elementId;
        [SerializeField, InspectorName("Variant Group ID")] private string variantGroupId;
        [SerializeField, InspectorName("Card Image")] private Sprite cardSprite;
        [SerializeField, InspectorName("Area Image")] private Sprite areaSprite;
        [TextArea(2, 6)]
        [SerializeField, InspectorName("Intro")] private string intro;
        [TextArea(2, 6)]
        [SerializeField, InspectorName("Concept")] private string concept;

        public Sprite CardSprite => cardSprite != null ? cardSprite : Portrait;
        public Sprite AreaSprite => areaSprite;
        public string Intro => intro;
        public string Concept => concept;
        public string CardKind => string.IsNullOrWhiteSpace(cardKind) ? InferCardKind(Id, DisplayName) : cardKind.Trim();
        public string ElementId => string.IsNullOrWhiteSpace(elementId) ? InferElementId(Id, DisplayName) : elementId.Trim();
        public string VariantGroupId => string.IsNullOrWhiteSpace(variantGroupId) ? InferVariantGroupId(Id) : variantGroupId.Trim();
        public bool IsAttackCard => string.Equals(CardKind, "Attack", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public class MonsterBookData : BookDataBase
    {
        [FormerlySerializedAs("portrait")]
        [SerializeField, InspectorName("Illustration")] private Sprite illustration;
        [FormerlySerializedAs("animationPrefab")]
        [SerializeField, InspectorName("Visual Prefab")] private GameObject visualPrefab;
        [SerializeField, InspectorName("Preview Entries")] private List<MonsterPreviewEntryData> previewEntries = new List<MonsterPreviewEntryData>();
        [TextArea(2, 6)]
        [SerializeField, InspectorName("Intro")] private string intro;
        [TextArea(2, 6)]
        [SerializeField, InspectorName("Tip")] private string tip;
        [SerializeField, InspectorName("Skills")] private List<MonsterSkillData> skills = new List<MonsterSkillData>();

        public Sprite Illustration => illustration != null ? illustration : Portrait;
        public GameObject VisualPrefab => visualPrefab;
        public List<MonsterPreviewEntryData> PreviewEntries => previewEntries;
        public string Intro => intro;
        public string Tip => tip;
        public List<MonsterSkillData> Skills => skills;
    }

    [Serializable]
    public class RelicBookData : BookDataBase
    {
        [TextArea(2, 6)]
        [SerializeField, InspectorName("Intro")] private string intro;
        [TextArea(2, 6)]
        [SerializeField, InspectorName("Concept")] private string concept;
        [SerializeField, InspectorName("Area Sprite")] private Sprite areaSprite;

        public string Intro => intro;
        public string Concept => concept;
        public Sprite AreaSprite => areaSprite;
    }

    [Header("Panel Roots")]
    [SerializeField, InspectorName("Illustrated Book Panel"), Tooltip("Root of the whole panel.")]
    private GameObject illustratedBookPanel;
    [SerializeField, InspectorName("Blocker"), Tooltip("Optional overlay shown while the panel is open.")]
    private GameObject blocker;
    [SerializeField, InspectorName("Cards Panel"), Tooltip("List root for cards.")]
    private GameObject cardsPanel;
    [SerializeField, InspectorName("Monsters Panel"), Tooltip("List root for monsters.")]
    private GameObject monstersPanel;
    [SerializeField, InspectorName("Relics Panel"), Tooltip("List root for relics.")]
    private GameObject relicsPanel;

    [Header("Buttons")]
    [SerializeField, InspectorName("Close Button"), Tooltip("Closes the panel.")]
    private Button closeButton;
    [SerializeField, InspectorName("Cards Tab Button")] private Button cardsTabButton;
    [SerializeField, InspectorName("Monsters Tab Button")] private Button monstersTabButton;
    [SerializeField, InspectorName("Relics Tab Button")] private Button relicsTabButton;

    [Header("Pagination")]
    [SerializeField, InspectorName("Cards Pagination")] private PaginationBindings cardsPagination;
    [SerializeField, InspectorName("Monsters Pagination")] private PaginationBindings monstersPagination;
    [SerializeField, InspectorName("Relics Pagination")] private PaginationBindings relicsPagination;

    [Header("Shared Pagination")]
    [SerializeField, InspectorName("Use Shared Pagination")] private bool useSharedPaginationBindings;
    [SerializeField, InspectorName("Shared Pagination")] private PaginationBindings sharedPagination;

    [Header("Fixed Slots")]
    [SerializeField, InspectorName("Cards Slots"), Tooltip("Fixed slots used by the cards list.")]
    private List<IllustratedBookSlotBinding> cardsSlots = new List<IllustratedBookSlotBinding>();
    [SerializeField, InspectorName("Monsters Slots")] private List<IllustratedBookSlotBinding> monstersSlots = new List<IllustratedBookSlotBinding>();
    [SerializeField, InspectorName("Relics Slots")] private List<IllustratedBookSlotBinding> relicsSlots = new List<IllustratedBookSlotBinding>();

    [Header("Detail Bindings")]
    [SerializeField, InspectorName("Card Detail")] private CardDetailBindings cardDetail;
    [SerializeField, InspectorName("Monster Detail")] private MonsterDetailBindings monsterDetail;
    [SerializeField, InspectorName("Relic Detail")] private RelicDetailBindings relicDetail;

    [Header("Detail Back Buttons")]
    [SerializeField, InspectorName("Card Detail Back Button")] private Button cardDetailBackButton;
    [SerializeField, InspectorName("Monster Detail Back Button")] private Button monsterDetailBackButton;
    [SerializeField, InspectorName("Relic Detail Back Button")] private Button relicDetailBackButton;

    public bool IsOpen => illustratedBookPanel != null && illustratedBookPanel.activeSelf;

    [Header("Behavior")]
    [SerializeField, InspectorName("Default Page")] private BookPage defaultPage = BookPage.Cards;
    [SerializeField, InspectorName("Hide Panel On Awake")] private bool hidePanelOnAwake = true;
    [SerializeField, InspectorName("Close On Escape")] private bool closeOnEscape;

    [Header("Data")]
    [SerializeField, InspectorName("Cards Data")] private List<CardBookData> cardsData = new List<CardBookData>();
    [SerializeField, InspectorName("Monsters Data")] private List<MonsterBookData> monstersData = new List<MonsterBookData>();
    [SerializeField, InspectorName("Relics Data")] private List<RelicBookData> relicsData = new List<RelicBookData>();

    private BookPage _currentPage = BookPage.Cards;
    private int _cardsPageIndex;
    private int _monstersPageIndex;
    private int _relicsPageIndex;
    private bool _isDetailOpen;
    private readonly List<CardBookData> _currentCardVariants = new List<CardBookData>();
    private int _currentCardVariantIndex;
    private const string DefaultVisibleAttackElement = "Water";

    private void Awake()
    {
        EnsureButtonSfxForChildren(illustratedBookPanel != null ? illustratedBookPanel.transform : transform);
        WireButtons();

        if (hidePanelOnAwake)
        {
            if (illustratedBookPanel != null) illustratedBookPanel.SetActive(false);
            if (blocker != null) blocker.SetActive(false);
        }

        HideAllDetailRoots();
    }

    private void Update()
    {
        if (!closeOnEscape) return;
        if (illustratedBookPanel == null || !illustratedBookPanel.activeSelf) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    public void Open()
    {
        Open(defaultPage);
    }

    public void Open(BookPage page)
    {
        if (illustratedBookPanel == null)
        {
            Debug.LogWarning("IllustratedBookPanelController: illustratedBookPanel is not assigned.", this);
            return;
        }

        illustratedBookPanel.SetActive(true);
        if (blocker != null) blocker.SetActive(true);

        switch (page)
        {
            case BookPage.Monsters:
                ShowMonsters();
                break;
            case BookPage.Relics:
                ShowRelics();
                break;
            default:
                ShowCards();
                break;
        }
    }

    public void Close()
    {
        HideAllDetailRoots();
        _isDetailOpen = false;

        if (illustratedBookPanel != null) illustratedBookPanel.SetActive(false);
        if (blocker != null) blocker.SetActive(false);
    }

    public void Toggle()
    {
        if (illustratedBookPanel == null)
        {
            Debug.LogWarning("IllustratedBookPanelController: illustratedBookPanel is not assigned.", this);
            return;
        }

        if (illustratedBookPanel.activeSelf) Close();
        else Open();
    }

    public void ShowCards()
    {
        _currentPage = BookPage.Cards;
        _cardsPageIndex = 0;
        _isDetailOpen = false;
        HideAllDetailRoots();
        ShowOnlyListPanel(BookPage.Cards);
        BindSharedPaginationIfNeeded(BookPage.Cards);
        RefreshCardsPage();
    }

    public void ShowMonsters()
    {
        _currentPage = BookPage.Monsters;
        _monstersPageIndex = 0;
        _isDetailOpen = false;
        HideAllDetailRoots();
        ShowOnlyListPanel(BookPage.Monsters);
        BindSharedPaginationIfNeeded(BookPage.Monsters);
        RefreshMonstersPage();
    }

    public void ShowRelics()
    {
        _currentPage = BookPage.Relics;
        _relicsPageIndex = 0;
        _isDetailOpen = false;
        HideAllDetailRoots();
        ShowOnlyListPanel(BookPage.Relics);
        BindSharedPaginationIfNeeded(BookPage.Relics);
        RefreshRelicsPage();
    }

    public void BackToList()
    {
        _isDetailOpen = false;
        HideAllDetailRoots();
        ShowOnlyListPanel(_currentPage);

        switch (_currentPage)
        {
            case BookPage.Monsters:
                BindSharedPaginationIfNeeded(BookPage.Monsters);
                RefreshMonstersPage();
                break;
            case BookPage.Relics:
                BindSharedPaginationIfNeeded(BookPage.Relics);
                RefreshRelicsPage();
                break;
            default:
                BindSharedPaginationIfNeeded(BookPage.Cards);
                RefreshCardsPage();
                break;
        }
    }

    public void PrevCardsPage() { ChangePage(BookPage.Cards, -1); }
    public void NextCardsPage() { ChangePage(BookPage.Cards, +1); }
    public void PrevMonstersPage() { ChangePage(BookPage.Monsters, -1); }
    public void NextMonstersPage() { ChangePage(BookPage.Monsters, +1); }
    public void PrevRelicsPage() { ChangePage(BookPage.Relics, -1); }
    public void NextRelicsPage() { ChangePage(BookPage.Relics, +1); }

    public void OpenCardDetailById(string id) { OpenDetail(BookPage.Cards, id); }
    public void OpenMonsterDetailById(string id) { OpenDetail(BookPage.Monsters, id); }
    public void OpenRelicDetailById(string id) { OpenDetail(BookPage.Relics, id); }

    public void RefreshCurrentCategory()
    {
        switch (_currentPage)
        {
            case BookPage.Monsters:
                RefreshMonstersPage();
                break;
            case BookPage.Relics:
                RefreshRelicsPage();
                break;
            default:
                RefreshCardsPage();
                break;
        }
    }

    private void WireButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (cardsTabButton != null)
        {
            cardsTabButton.onClick.RemoveAllListeners();
            cardsTabButton.onClick.AddListener(ShowCards);
        }

        if (monstersTabButton != null)
        {
            monstersTabButton.onClick.RemoveAllListeners();
            monstersTabButton.onClick.AddListener(ShowMonsters);
        }

        if (relicsTabButton != null)
        {
            relicsTabButton.onClick.RemoveAllListeners();
            relicsTabButton.onClick.AddListener(ShowRelics);
        }

        WirePerCategoryPaginationButtons();

        if (cardDetailBackButton != null)
        {
            cardDetailBackButton.onClick.RemoveAllListeners();
            cardDetailBackButton.onClick.AddListener(BackToList);
        }

        if (monsterDetailBackButton != null)
        {
            monsterDetailBackButton.onClick.RemoveAllListeners();
            monsterDetailBackButton.onClick.AddListener(BackToList);
        }

        if (relicDetailBackButton != null)
        {
            relicDetailBackButton.onClick.RemoveAllListeners();
            relicDetailBackButton.onClick.AddListener(BackToList);
        }

        cardDetail?.BindVariantButtons(ShowPreviousCardVariant, ShowNextCardVariant);
        monsterDetail?.BindPreviewButtons();
    }

    private static void EnsureButtonSfxForChildren(Transform root)
    {
        if (root == null) return;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || button.GetComponent<UIButtonSfxPlayer>() != null) continue;

            button.gameObject.AddComponent<UIButtonSfxPlayer>();
        }
    }

    private void WirePerCategoryPaginationButtons()
    {
        BindPaginationButton(cardsPagination != null ? cardsPagination.PrevButton : null, PrevCardsPage);
        BindPaginationButton(cardsPagination != null ? cardsPagination.NextButton : null, NextCardsPage);
        BindPaginationButton(monstersPagination != null ? monstersPagination.PrevButton : null, PrevMonstersPage);
        BindPaginationButton(monstersPagination != null ? monstersPagination.NextButton : null, NextMonstersPage);
        BindPaginationButton(relicsPagination != null ? relicsPagination.PrevButton : null, PrevRelicsPage);
        BindPaginationButton(relicsPagination != null ? relicsPagination.NextButton : null, NextRelicsPage);
    }

    private void BindSharedPaginationIfNeeded(BookPage category)
    {
        if (!useSharedPaginationBindings || sharedPagination == null) return;

        if (sharedPagination.PrevButton != null) sharedPagination.PrevButton.onClick.RemoveAllListeners();
        if (sharedPagination.NextButton != null) sharedPagination.NextButton.onClick.RemoveAllListeners();

        switch (category)
        {
            case BookPage.Monsters:
                BindPaginationButton(sharedPagination.PrevButton, PrevMonstersPage);
                BindPaginationButton(sharedPagination.NextButton, NextMonstersPage);
                break;
            case BookPage.Relics:
                BindPaginationButton(sharedPagination.PrevButton, PrevRelicsPage);
                BindPaginationButton(sharedPagination.NextButton, NextRelicsPage);
                break;
            default:
                BindPaginationButton(sharedPagination.PrevButton, PrevCardsPage);
                BindPaginationButton(sharedPagination.NextButton, NextCardsPage);
                break;
        }
    }

    private void BindPaginationButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void ChangePage(BookPage category, int delta)
    {
        switch (category)
        {
            case BookPage.Monsters:
            {
                List<MonsterBookData> sorted = GetSortedMonsters();
                int totalPages = GetTotalPages(sorted.Count, monstersSlots.Count);
                _monstersPageIndex = Mathf.Clamp(_monstersPageIndex + delta, 0, Mathf.Max(0, totalPages - 1));
                RefreshMonstersPage();
                break;
            }
            case BookPage.Relics:
            {
                List<RelicBookData> sorted = GetSortedRelics();
                int totalPages = GetTotalPages(sorted.Count, relicsSlots.Count);
                _relicsPageIndex = Mathf.Clamp(_relicsPageIndex + delta, 0, Mathf.Max(0, totalPages - 1));
                RefreshRelicsPage();
                break;
            }
            default:
            {
                List<CardBookData> sorted = GetSortedCards();
                int totalPages = GetTotalPages(sorted.Count, cardsSlots.Count);
                _cardsPageIndex = Mathf.Clamp(_cardsPageIndex + delta, 0, Mathf.Max(0, totalPages - 1));
                RefreshCardsPage();
                break;
            }
        }
    }

    private void RefreshCardsPage()
    {
        RefreshPage(cardsSlots, GetSortedCards(), _cardsPageIndex, BookPage.Cards, ResolvePagination(BookPage.Cards));
    }

    private void RefreshMonstersPage()
    {
        RefreshPage(monstersSlots, GetSortedMonsters(), _monstersPageIndex, BookPage.Monsters, ResolvePagination(BookPage.Monsters));
    }

    private void RefreshRelicsPage()
    {
        RefreshPage(relicsSlots, GetSortedRelics(), _relicsPageIndex, BookPage.Relics, ResolvePagination(BookPage.Relics));
    }

    private void RefreshPage<T>(List<IllustratedBookSlotBinding> slots, List<T> sortedItems, int pageIndex, BookPage category, PaginationBindings pagination)
        where T : BookDataBase
    {
        if (slots == null || slots.Count == 0)
        {
            Debug.LogWarning("IllustratedBookPanelController: slots are not assigned for " + category + ".", this);
            UpdatePaginationUi(pagination, 0, 0, 0);
            return;
        }

        int itemsPerPage = slots.Count;
        int totalItems = sortedItems != null ? sortedItems.Count : 0;
        int totalPages = GetTotalPages(totalItems, itemsPerPage);
        int clampedPageIndex = Mathf.Clamp(pageIndex, 0, Mathf.Max(0, totalPages - 1));
        int startIndex = clampedPageIndex * itemsPerPage;

        for (int i = 0; i < slots.Count; i++)
        {
            IllustratedBookSlotBinding slot = slots[i];
            if (slot == null)
            {
                Debug.LogWarning("IllustratedBookPanelController: slot binding is null at index " + i + " for " + category + ".", this);
                continue;
            }

            slot.ClearClick();

            int dataIndex = startIndex + i;
            if (sortedItems != null && dataIndex >= 0 && dataIndex < sortedItems.Count)
            {
                T item = sortedItems[dataIndex];
                slot.SetFilled(item.Icon, item.DisplayName);

                string itemId = item.Id;
                slot.BindClick(() => OpenDetail(category, itemId));
            }
            else
            {
                slot.SetEmpty();
            }
        }

        UpdatePaginationUi(pagination, clampedPageIndex, totalPages, totalItems);

        switch (category)
        {
            case BookPage.Monsters:
                _monstersPageIndex = clampedPageIndex;
                break;
            case BookPage.Relics:
                _relicsPageIndex = clampedPageIndex;
                break;
            default:
                _cardsPageIndex = clampedPageIndex;
                break;
        }
    }

    private void UpdatePaginationUi(PaginationBindings pagination, int pageIndex, int totalPages, int totalItems)
    {
        if (pagination == null) return;

        bool hasPages = totalPages > 0;
        bool canPrev = hasPages && pageIndex > 0;
        bool canNext = hasPages && pageIndex < totalPages - 1;

        if (pagination.PrevButton != null) pagination.PrevButton.interactable = canPrev;
        if (pagination.NextButton != null) pagination.NextButton.interactable = canNext;

        if (pagination.PageText != null)
        {
            pagination.PageText.SetText(totalPages <= 0 || totalItems < 0 ? "0/0" : (pageIndex + 1) + "/" + totalPages);
        }
    }

    private void OpenDetail(BookPage category, string id)
    {
        Debug.Log("OpenDetail: " + category + " / " + id);
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("IllustratedBookPanelController: clicked item id is null/empty.", this);
            return;
        }

        HideAllDetailRoots();
        HideAllListPanels();

        switch (category)
        {
            case BookPage.Monsters:
            {
                MonsterBookData data = FindById(GetSortedMonsters(), id);
                if (data == null)
                {
                    Debug.LogWarning("IllustratedBookPanelController: monster id not found -> " + id, this);
                    BackToList();
                    return;
                }

                if (monsterDetail != null && monsterDetail.Root != null) monsterDetail.Root.SetActive(true);
                monsterDetail?.Fill(data);
                _currentPage = BookPage.Monsters;
                break;
            }
            case BookPage.Relics:
            {
                RelicBookData data = FindById(GetSortedRelics(), id);
                if (data == null)
                {
                    Debug.LogWarning("IllustratedBookPanelController: relic id not found -> " + id, this);
                    BackToList();
                    return;
                }

                if (relicDetail != null && relicDetail.Root != null) relicDetail.Root.SetActive(true);
                relicDetail?.Fill(data);
                _currentPage = BookPage.Relics;
                break;
            }
            default:
            {
                CardBookData data = FindById(GetSortedCards(), id);
                if (data == null)
                {
                    Debug.LogWarning("IllustratedBookPanelController: card id not found -> " + id, this);
                    BackToList();
                    return;
                }

                if (cardDetail != null && cardDetail.Root != null) cardDetail.Root.SetActive(true);
                OpenCardDetail(data);
                _currentPage = BookPage.Cards;
                break;
            }
        }

        _isDetailOpen = true;
    }

    private void OpenCardDetail(CardBookData data)
    {
        _currentCardVariants.Clear();
        _currentCardVariantIndex = 0;

        if (data == null)
        {
            cardDetail?.SetVariantNavigation(false, false, false);
            return;
        }

        if (data.IsAttackCard)
        {
            string groupId = data.VariantGroupId;
            _currentCardVariants.AddRange(GetSortedAllCards()
                .Where(card => card != null &&
                               card.IsAttackCard &&
                               string.Equals(card.VariantGroupId, groupId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(card => GetElementSortOrder(card.ElementId)));

            _currentCardVariantIndex = Mathf.Max(0, _currentCardVariants.FindIndex(card => card.Id == data.Id));
        }

        if (_currentCardVariants.Count == 0)
            _currentCardVariants.Add(data);

        ApplyCurrentCardVariant();
    }

    private void ShowPreviousCardVariant()
    {
        ShowCardVariant(_currentCardVariantIndex - 1);
    }

    private void ShowNextCardVariant()
    {
        ShowCardVariant(_currentCardVariantIndex + 1);
    }

    private void ShowCardVariant(int index)
    {
        if (_currentCardVariants.Count <= 1) return;

        _currentCardVariantIndex = Mathf.Clamp(index, 0, _currentCardVariants.Count - 1);
        ApplyCurrentCardVariant();
    }

    private void ApplyCurrentCardVariant()
    {
        if (_currentCardVariants.Count == 0)
        {
            cardDetail?.SetVariantNavigation(false, false, false);
            return;
        }

        _currentCardVariantIndex = Mathf.Clamp(_currentCardVariantIndex, 0, _currentCardVariants.Count - 1);
        CardBookData data = _currentCardVariants[_currentCardVariantIndex];
        cardDetail?.Fill(data);

        bool canSwitch = data != null && data.IsAttackCard && _currentCardVariants.Count > 1;
        cardDetail?.SetVariantNavigation(
            canSwitch,
            canSwitch && _currentCardVariantIndex > 0,
            canSwitch && _currentCardVariantIndex < _currentCardVariants.Count - 1);
    }

    private void ShowOnlyListPanel(BookPage page)
    {
        HideAllListPanels();

        switch (page)
        {
            case BookPage.Monsters:
                if (monstersPanel != null) monstersPanel.SetActive(true);
                break;
            case BookPage.Relics:
                if (relicsPanel != null) relicsPanel.SetActive(true);
                break;
            default:
                if (cardsPanel != null) cardsPanel.SetActive(true);
                break;
        }
    }

    private void HideAllListPanels()
    {
        if (cardsPanel != null) cardsPanel.SetActive(false);
        if (monstersPanel != null) monstersPanel.SetActive(false);
        if (relicsPanel != null) relicsPanel.SetActive(false);
    }

    private void HideAllDetailRoots()
    {
        _currentCardVariants.Clear();
        _currentCardVariantIndex = 0;
        cardDetail?.SetVariantNavigation(false, false, false);

        if (cardDetail != null && cardDetail.Root != null) cardDetail.Root.SetActive(false);

        if (monsterDetail != null)
        {
            monsterDetail.Clear();
            if (monsterDetail.Root != null) monsterDetail.Root.SetActive(false);
        }

        if (relicDetail != null && relicDetail.Root != null) relicDetail.Root.SetActive(false);
    }

    private PaginationBindings ResolvePagination(BookPage page)
    {
        if (useSharedPaginationBindings && sharedPagination != null) return sharedPagination;

        switch (page)
        {
            case BookPage.Monsters: return monstersPagination;
            case BookPage.Relics: return relicsPagination;
            default: return cardsPagination;
        }
    }

    private List<CardBookData> GetSortedCards()
    {
        return GetSortedAllCards()
            .Where(card => card == null ||
                           !card.IsAttackCard ||
                           string.Equals(card.ElementId, DefaultVisibleAttackElement, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private List<CardBookData> GetSortedAllCards()
    {
        return SortEntries(cardsData);
    }

    private List<MonsterBookData> GetSortedMonsters()
    {
        return SortEntries(monstersData);
    }

    private List<RelicBookData> GetSortedRelics()
    {
        return SortEntries(relicsData);
    }

    private static List<T> SortEntries<T>(List<T> source) where T : BookDataBase
    {
        if (source == null) return new List<T>();

        return source
            .Where(x => x != null)
            .OrderByDescending(x => x.Pinned)
            .ThenBy(x => x.Order)
            .ThenBy(x => string.IsNullOrEmpty(x.DisplayName) ? x.Id : x.DisplayName, StringComparer.Ordinal)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static T FindById<T>(List<T> source, string id) where T : BookDataBase
    {
        if (source == null || string.IsNullOrEmpty(id)) return null;
        string normalizedId = id.Trim();
        return source.FirstOrDefault(x => x != null && string.Equals(x.Id != null ? x.Id.Trim() : string.Empty, normalizedId, StringComparison.Ordinal));
    }

    private static string InferCardKind(string id, string displayName)
    {
        string normalizedId = id ?? string.Empty;
        string normalizedName = displayName ?? string.Empty;

        if (normalizedId.StartsWith("Attack_", StringComparison.OrdinalIgnoreCase) || normalizedName.StartsWith("攻擊", StringComparison.Ordinal))
            return "Attack";

        if (normalizedId.StartsWith("Skill_", StringComparison.OrdinalIgnoreCase) || normalizedName.StartsWith("法術", StringComparison.Ordinal))
            return "Skill";

        if (normalizedId.StartsWith("Move_", StringComparison.OrdinalIgnoreCase) || normalizedName.StartsWith("移動", StringComparison.Ordinal))
            return "Move";

        return string.Empty;
    }

    private static string InferElementId(string id, string displayName)
    {
        string normalizedId = id != null ? id.Trim() : string.Empty;
        string normalizedName = displayName != null ? displayName.Trim() : string.Empty;

        if (normalizedId.EndsWith("_Fire", StringComparison.OrdinalIgnoreCase) || normalizedName.EndsWith("_火", StringComparison.Ordinal)) return "Fire";
        if (normalizedId.EndsWith("_Water", StringComparison.OrdinalIgnoreCase) || normalizedName.EndsWith("_水", StringComparison.Ordinal)) return "Water";
        if (normalizedId.EndsWith("_Thunder", StringComparison.OrdinalIgnoreCase) || normalizedName.EndsWith("_雷", StringComparison.Ordinal)) return "Thunder";
        if (normalizedId.EndsWith("_Ice", StringComparison.OrdinalIgnoreCase) || normalizedName.EndsWith("_冰", StringComparison.Ordinal)) return "Ice";
        if (normalizedId.EndsWith("_Wood", StringComparison.OrdinalIgnoreCase) || normalizedName.EndsWith("_木", StringComparison.Ordinal)) return "Wood";

        return string.Empty;
    }

    private static string InferVariantGroupId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;

        string normalizedId = id.Trim();
        string[] elementSuffixes = { "_Fire", "_Water", "_Thunder", "_Ice", "_Wood" };
        for (int i = 0; i < elementSuffixes.Length; i++)
        {
            string suffix = elementSuffixes[i];
            if (normalizedId.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return normalizedId.Substring(0, normalizedId.Length - suffix.Length);
        }

        return normalizedId;
    }

    private static int GetElementSortOrder(string elementId)
    {
        if (string.Equals(elementId, "Water", StringComparison.OrdinalIgnoreCase)) return 0;
        if (string.Equals(elementId, "Fire", StringComparison.OrdinalIgnoreCase)) return 1;
        if (string.Equals(elementId, "Thunder", StringComparison.OrdinalIgnoreCase)) return 2;
        if (string.Equals(elementId, "Ice", StringComparison.OrdinalIgnoreCase)) return 3;
        if (string.Equals(elementId, "Wood", StringComparison.OrdinalIgnoreCase)) return 4;
        return 99;
    }

    private static int GetTotalPages(int itemCount, int itemsPerPage)
    {
        if (itemsPerPage <= 0) return 0;
        return Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)itemsPerPage));
    }
}
