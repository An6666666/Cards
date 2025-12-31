using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public class ElementButtonBinding
{
    public ElementType element;
    public Button button;
    public Image image;
}

public class StartingElementSelectionUI : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private string runSceneName = "RunScene";

    [Header("UI References")]
    [SerializeField] private Text titleText;
    [SerializeField] private List<ElementButtonBinding> elementButtons = new List<ElementButtonBinding>();
    [SerializeField] private Button startButton;
    [SerializeField] private Image startButtonImage;

    [Header("Visuals")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Sprite selectionRingSprite;
    [SerializeField] private Color selectionRingColor = Color.white;
    [SerializeField] private float selectionRingDrawDuration = 0.45f;
    [SerializeField] private float selectionRingHideDuration = 0.2f;
    [SerializeField] private Vector2 selectionRingPadding = new Vector2(12f, 12f);
    [SerializeField] private Color startEnabledColor = new Color(0.2f, 0.6f, 0.2f);
    [SerializeField] private Color startDisabledColor = new Color(0.5f, 0.5f, 0.5f);

    private readonly List<ElementType> selectedElements = new List<ElementType>(3);
    private readonly Dictionary<ElementType, ElementButtonBinding> elementLookup = new Dictionary<ElementType, ElementButtonBinding>();
    private readonly Dictionary<ElementType, Image> selectionRings = new Dictionary<ElementType, Image>();
    private readonly Dictionary<ElementType, Tween> selectionTweens = new Dictionary<ElementType, Tween>();
    private readonly Dictionary<ElementType, bool> selectionStates = new Dictionary<ElementType, bool>();

    private void Awake()
    {
        BuildLookup();
        WireUpButtons();
        RefreshButtonStates();
    }

    private void BuildLookup()
    {
        elementLookup.Clear();
        foreach (ElementButtonBinding binding in elementButtons)
        {
            if (binding == null || binding.button == null || binding.image == null)
                continue;

            if (!elementLookup.ContainsKey(binding.element))
            {
                elementLookup.Add(binding.element, binding);
                TryCreateSelectionRing(binding);
            }
        }
    }

    private void WireUpButtons()
    {
        foreach (ElementButtonBinding binding in elementLookup.Values)
        {
            binding.button.onClick.RemoveAllListeners();
            binding.button.onClick.AddListener(() => ToggleElement(binding.element));
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartClicked);
        }

    }

    private void ToggleElement(ElementType element)
    {
        if (selectedElements.Contains(element))
        {
            selectedElements.Remove(element);
        }
        else if (selectedElements.Count < 3)
        {
            selectedElements.Add(element);
        }

        RefreshButtonStates();
    }

    private void RefreshButtonStates()
    {
        foreach (KeyValuePair<ElementType, ElementButtonBinding> pair in elementLookup)
        {
            bool selected = selectedElements.Contains(pair.Key);
            pair.Value.image.color = normalColor;
            UpdateSelectionRing(pair.Key, selected);
        }

        bool ready = selectedElements.Count == 3;
        if (startButton != null)
        {
            startButton.interactable = ready;
        }

        if (startButtonImage != null)
        {
            startButtonImage.color = ready ? startEnabledColor : startDisabledColor;
        }
    }

    private void TryCreateSelectionRing(ElementButtonBinding binding)
    {
        if (selectionRingSprite == null || selectionRings.ContainsKey(binding.element))
            return;

        GameObject ringObject = new GameObject("SelectionRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        ringObject.transform.SetParent(binding.image.transform, false);

        RectTransform rectTransform = ringObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = -selectionRingPadding;
        rectTransform.offsetMax = selectionRingPadding;

        Image ringImage = ringObject.GetComponent<Image>();
        ringImage.sprite = selectionRingSprite;
        ringImage.type = Image.Type.Filled;
        ringImage.fillMethod = Image.FillMethod.Radial90;
        ringImage.fillAmount = 0f;
        ringImage.color = selectionRingColor;
        ringImage.raycastTarget = false;

        ringObject.SetActive(false);
        selectionRings[binding.element] = ringImage;
    }

    private void UpdateSelectionRing(ElementType element, bool selected)
    {
        if (!selectionRings.TryGetValue(element, out Image ringImage))
        return;

        if (selectionStates.TryGetValue(element, out bool previousSelected) && previousSelected == selected)
        {
            if (selected)
            {
                ringImage.gameObject.SetActive(true);
                ringImage.fillAmount = 1f;
            }
            return;
        }

        if (selectionTweens.TryGetValue(element, out Tween tween))
        {
            tween.Kill(false);
        }

        float duration = selected ? selectionRingDrawDuration : selectionRingHideDuration;

        if (selected)
        {
            ringImage.fillAmount = 0f;
            ringImage.gameObject.SetActive(true);
        }

        selectionTweens[element] = ringImage
            .DOFillAmount(selected ? 1f : 0f, duration)
            .SetEase(selected ? Ease.OutCubic : Ease.InCubic)
            .OnComplete(() =>
            {
                if (!selected)
                {
                    ringImage.gameObject.SetActive(false);
                }
            });
        
        selectionStates[element] = selected;
    }

    private void OnDestroy()
    {
        foreach (Tween tween in selectionTweens.Values)
        {
            tween?.Kill(false);
        }

        selectionTweens.Clear();
    }

    private void OnStartClicked()
    {
        if (selectedElements.Count != 3)
            return;

        StartingDeckSelection.SetSelection(selectedElements);
        SceneManager.LoadScene(runSceneName);
    }
}