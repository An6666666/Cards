using UnityEngine;
using UnityEngine.UI;

public class RulePanelController : MonoBehaviour
{
    [Header("Rule UI")]
    [SerializeField] private GameObject rulePanel;
    [SerializeField] private Image ruleImage;
    [SerializeField] private Sprite[] rulePages;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button closeButton;

    private int _currentIndex;
    private UIFxController _fx;

    public void Initialize(UIFxController fx)
    {
        _fx = fx;
        WireButtons();
        if (rulePanel) rulePanel.SetActive(false);
        if (rulePages != null && rulePages.Length > 0 && ruleImage != null)
        {
            _currentIndex = 0;
            ruleImage.sprite = rulePages[_currentIndex];
        }
    }

    private void WireButtons()
    {
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

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    public void Open()
    {
        if (rulePanel) _fx?.ShowPanel(rulePanel);
    }

    public void Close()
    {
        if (rulePanel) _fx?.HidePanel(rulePanel);
    }

    public void NextPage()
    {
        if (rulePages == null || rulePages.Length == 0 || ruleImage == null) return;
        int next = (_currentIndex + 1) % rulePages.Length;
        _fx?.CrossSlideRulePage(ruleImage, rulePages[next], toRight: true);
        _currentIndex = next;
    }

    public void PrevPage()
    {
        if (rulePages == null || rulePages.Length == 0 || ruleImage == null) return;
        int next = (_currentIndex - 1 + rulePages.Length) % rulePages.Length;
        _fx?.CrossSlideRulePage(ruleImage, rulePages[next], toRight: false);
        _currentIndex = next;
    }
}