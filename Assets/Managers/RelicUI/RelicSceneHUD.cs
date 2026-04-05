using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 在 Battle / Run / Shop 場景自動建立的遺物 HUD，顯示目前持有的遺物圖示。
/// </summary>
public class RelicSceneHUD : MonoBehaviour
{
    private const string HudObjectName = "RelicSceneHUD";
    private const string RunSceneName = "RunScene";
    private const string ShopSceneName = "ShopScene";

    private static readonly HashSet<string> SupportedScenes = new HashSet<string>();

    private readonly List<GameObject> spawnedSlots = new List<GameObject>();
    private static Sprite defaultUiSprite;

    private RunManager runManager;
    private RectTransform contentRoot;
    private Text titleText;
    private Text emptyText;
    private bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!SupportedScenes.Contains(scene.name))
            return;

        if (TryFindExisting(scene) != null)
            return;

        GameObject hudObject = new GameObject(HudObjectName, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(hudObject, scene);
        hudObject.AddComponent<RelicSceneHUD>();
    }

    private static RelicSceneHUD TryFindExisting(Scene scene)
    {
        if (!scene.IsValid())
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            RelicSceneHUD existing = roots[i].GetComponentInChildren<RelicSceneHUD>(true);
            if (existing != null)
                return existing;
        }

        return null;
    }

    private void Awake()
    {
        Canvas canvas = ResolveTargetCanvas();
        if (canvas == null)
        {
            enabled = false;
            return;
        }

        BuildLayout(canvas);
        RefreshFromAvailableData();
    }

    private void OnEnable()
    {
        ResolveRunManager();
        Subscribe();
        RefreshFromAvailableData();
    }

    private void Start()
    {
        RefreshFromAvailableData();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (runManager == RunManager.Instance && subscribed)
            return;

        ResolveRunManager();
        Subscribe();
        RefreshFromAvailableData();
    }

    private void ResolveRunManager()
    {
        runManager = RunManager.Instance;
    }

    private void Subscribe()
    {
        if (subscribed && runManager == null)
        {
            subscribed = false;
            return;
        }

        if (runManager == null || subscribed)
            return;

        runManager.RunSnapshotChanged -= HandleRunSnapshotChanged;
        runManager.RunSnapshotChanged += HandleRunSnapshotChanged;
        runManager.MapStateChanged -= HandleMapStateChanged;
        runManager.MapStateChanged += HandleMapStateChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (runManager == null)
        {
            subscribed = false;
            return;
        }

        runManager.RunSnapshotChanged -= HandleRunSnapshotChanged;
        runManager.MapStateChanged -= HandleMapStateChanged;
        subscribed = false;
    }

    private void HandleRunSnapshotChanged(PlayerRunSnapshot snapshot)
    {
        Refresh(snapshot);
    }

    private void HandleMapStateChanged()
    {
        RefreshFromAvailableData();
    }

    private void RefreshFromAvailableData()
    {
        Refresh(runManager != null ? runManager.CurrentRunSnapshot : null);
    }

    private void Refresh(PlayerRunSnapshot snapshot)
    {
        if (contentRoot == null || titleText == null || emptyText == null)
            return;

        List<RelicBase> relics = ResolveRelics(snapshot);
        ClearSlots();

        int relicCount = relics != null ? relics.Count : 0;
        titleText.text = $"遺物 {relicCount}";
        emptyText.gameObject.SetActive(relicCount == 0);
        emptyText.text = relicCount == 0 ? "尚無遺物" : string.Empty;

        if (relics == null)
            return;

        for (int i = 0; i < relics.Count; i++)
        {
            RelicBase relic = relics[i];
            if (relic == null)
                continue;

            spawnedSlots.Add(CreateRelicSlot(relic));
        }
    }

    private List<RelicBase> ResolveRelics(PlayerRunSnapshot snapshot)
    {
        if (snapshot != null && snapshot.relics != null)
            return snapshot.relics;

        if (runManager != null && runManager.RegisteredPlayer != null && runManager.RegisteredPlayer.relics != null)
            return runManager.RegisteredPlayer.relics;

        return new List<RelicBase>();
    }

    private void ClearSlots()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            GameObject slot = spawnedSlots[i];
            if (slot != null)
                Destroy(slot);
        }

        spawnedSlots.Clear();
    }

    private GameObject CreateRelicSlot(RelicBase relic)
    {
        GameObject slotObject = new GameObject(
            string.IsNullOrWhiteSpace(relic.cardName) ? "RelicSlot" : $"RelicSlot_{relic.cardName}",
            typeof(RectTransform),
            typeof(Image));

        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        slotRect.SetParent(contentRoot, false);
        slotRect.localScale = Vector3.one;

        Image slotFrame = slotObject.GetComponent<Image>();
        ConfigurePanelImage(slotFrame, new Color(1f, 1f, 1f, 0.12f));

        Outline outline = slotObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(slotRect, false);
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(68f, 68f);
        iconRect.anchoredPosition = Vector2.zero;

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = relic.cardImage;
        iconImage.preserveAspect = true;
        iconImage.enabled = relic.cardImage != null;
        iconImage.raycastTarget = false;

        return slotObject;
    }

    private Canvas ResolveTargetCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.gameObject.scene != gameObject.scene)
                continue;

            if (canvas.isRootCanvas)
                return canvas;
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.gameObject.scene == gameObject.scene)
                return canvas;
        }

        return null;
    }

    private void BuildLayout(Canvas canvas)
    {
        RectTransform root = GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        root.SetAsLastSibling();
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = ResolveAnchoredPositionForScene();
        root.sizeDelta = new Vector2(560f, 196f);
        root.localScale = Vector3.one;

        Image rootBackground = gameObject.GetComponent<Image>();
        if (rootBackground == null)
            rootBackground = gameObject.AddComponent<Image>();

        ConfigurePanelImage(rootBackground, new Color(0f, 0f, 0f, 0.4f));

        Outline rootOutline = gameObject.GetComponent<Outline>();
        if (rootOutline == null)
            rootOutline = gameObject.AddComponent<Outline>();

        rootOutline.effectColor = new Color(0f, 0f, 0f, 0.65f);
        rootOutline.effectDistance = new Vector2(2f, -2f);
        rootOutline.useGraphicAlpha = true;

        titleText = CreateText("Title", root, 18, FontStyle.Bold, TextAnchor.UpperLeft);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(14f, -38f);
        titleRect.offsetMax = new Vector2(-14f, -10f);

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
        contentRoot = contentObject.GetComponent<RectTransform>();
        contentRoot.SetParent(root, false);
        contentRoot.anchorMin = new Vector2(0f, 0f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.offsetMin = new Vector2(14f, 14f);
        contentRoot.offsetMax = new Vector2(-14f, -44f);

        GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(76f, 76f);
        grid.spacing = new Vector2(10f, 10f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 2;

        emptyText = CreateText("Empty", root, 16, FontStyle.Italic, TextAnchor.MiddleCenter);
        RectTransform emptyRect = emptyText.rectTransform;
        emptyRect.anchorMin = new Vector2(0f, 0f);
        emptyRect.anchorMax = new Vector2(1f, 1f);
        emptyRect.offsetMin = new Vector2(14f, 14f);
        emptyRect.offsetMax = new Vector2(-14f, -44f);
    }

    private Vector2 ResolveAnchoredPositionForScene()
    {
        return new Vector2(20f, -20f);
    }

    private Text CreateText(string objectName, RectTransform parent, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }

    private void ConfigurePanelImage(Image image, Color color)
    {
        if (image == null)
            return;

        image.sprite = GetDefaultUiSprite();
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
    }

    private Sprite GetDefaultUiSprite()
    {
        if (defaultUiSprite != null)
            return defaultUiSprite;

        Texture2D texture = Texture2D.whiteTexture;
        defaultUiSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        return defaultUiSprite;
    }
}
