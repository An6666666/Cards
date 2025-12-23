using System.Collections.Generic;
using UnityEngine;
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
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color startEnabledColor = new Color(0.2f, 0.6f, 0.2f);
    [SerializeField] private Color startDisabledColor = new Color(0.5f, 0.5f, 0.5f);

    private readonly List<ElementType> selectedElements = new List<ElementType>(3);
    private readonly Dictionary<ElementType, ElementButtonBinding> elementLookup = new Dictionary<ElementType, ElementButtonBinding>();

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
            pair.Value.image.color = selectedElements.Contains(pair.Key) ? selectedColor : normalColor;
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

    private void OnStartClicked()
    {
        if (selectedElements.Count != 3)
            return;

        StartingDeckSelection.SetSelection(selectedElements);
        SceneManager.LoadScene(runSceneName);
    }
}