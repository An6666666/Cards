using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class RunAllDeckCounterButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RunAllDeckPanelController panelController;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoverSprite;

    private Button button;
    private Image buttonImage;

    public static RunAllDeckCounterButton Attach(RectTransform target)
    {
        if (target == null)
        {
            return null;
        }

        RunAllDeckCounterButton controller = target.GetComponent<RunAllDeckCounterButton>();
        if (controller == null)
        {
            controller = target.gameObject.AddComponent<RunAllDeckCounterButton>();
        }

        controller.EnsureWired();
        return controller;
    }

    private void Awake()
    {
        EnsureWired();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OpenAllDeck);
        }
    }

    private void EnsureWired()
    {
        ResolveReferences();

        button = GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(OpenAllDeck);
        button.onClick.AddListener(OpenAllDeck);
        buttonImage = button.targetGraphic as Image;
        ApplyNormalSprite();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (buttonImage != null && hoverSprite != null)
        {
            buttonImage.sprite = hoverSprite;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ApplyNormalSprite();
    }

    private void OpenAllDeck()
    {
        ResolveReferences();
        panelController?.Open();
    }

    private void ResolveReferences()
    {
        if (panelController != null)
        {
            return;
        }

        panelController = FindObjectOfType<RunAllDeckPanelController>(true);
        if (panelController != null)
        {
            return;
        }

        GameObject panel = FindSceneGameObject("AllDeck Panel");
        if (panel == null)
        {
            panel = CreatePanel();
        }

        if (panel != null)
        {
            panelController = panel.GetComponent<RunAllDeckPanelController>();
            if (panelController == null)
            {
                panelController = panel.AddComponent<RunAllDeckPanelController>();
            }
        }
    }

    private void ApplyNormalSprite()
    {
        if (buttonImage != null && normalSprite != null)
        {
            buttonImage.sprite = normalSprite;
        }
    }

    private static GameObject FindSceneGameObject(string objectName)
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t != null && t.name == objectName)
            {
                return t.gameObject;
            }
        }

        return null;
    }

    private GameObject CreatePanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return null;
        }

        GameObject root = new GameObject("AllDeck Panel", typeof(RectTransform), typeof(Image), typeof(RunAllDeckPanelController));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        Button closeButton = CreateButton(root.transform, "Button", "離開", new Vector2(0f, -380f), new Vector2(160f, 44f));

        GameObject scrollView = new GameObject("Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollView.transform.SetParent(root.transform, false);
        RectTransform scrollRectTransform = (RectTransform)scrollView.transform;
        scrollRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        scrollRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRectTransform.pivot = new Vector2(0.5f, 0.5f);
        scrollRectTransform.sizeDelta = new Vector2(760f, 520f);
        scrollRectTransform.anchoredPosition = new Vector2(0f, 20f);
        scrollView.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollView.transform, false);
        RectTransform viewportRect = (RectTransform)viewport.transform;
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(18f, 18f);
        viewportRect.offsetMax = new Vector2(-18f, -18f);
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        RunAllDeckPanelController controller = root.GetComponent<RunAllDeckPanelController>();
        controller.SetReferences(scrollRect, content.transform, closeButton);
        root.SetActive(false);
        return root;
    }

    private static Button CreateButton(Transform parent, string name, string text, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        buttonObject.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f, 1f);
        Button button = buttonObject.GetComponent<Button>();

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text label = textObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = 18;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        return button;
    }
}
