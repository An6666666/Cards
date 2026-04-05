using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在地圖與商店場景中，以戰鬥場相同的遺物 prefab 顯示目前持有的遺物。
/// 由場景手動放置，元件不會自動建立自己，也不會覆寫根物件的位置與尺寸。
/// </summary>
public class SceneRelicBarUI : MonoBehaviour
{
    [Header("Optional References")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private GameObject relicIconPrefab;
    [SerializeField] private bool hideContentRootWhenEmpty = false;

    [Header("Auto Content Defaults")]
    [SerializeField] private bool createContentRootIfMissing = true;
    [SerializeField] private Vector2 autoContentSize = new Vector2(40f, 20f);
    [SerializeField] private Vector3 autoContentScale = new Vector3(4.8525f, 4.8525f, 4.8525f);
    [SerializeField] private Vector2 autoCellSize = new Vector2(10f, 10f);
    [SerializeField] private Vector2 autoSpacing = new Vector2(-23.8f, -42.05f);

    private readonly List<GameObject> spawnedRelicObjects = new List<GameObject>();

    private RunManager runManager;
    private GridLayoutGroup layoutGroup;
    private bool subscribed;

    private void Awake()
    {
        EnsureContentRoot();
        RefreshFromAvailableData();
    }

    private void OnEnable()
    {
        ResolveRunManager();
        Subscribe();
        EnsureContentRoot();
        RefreshFromAvailableData();
    }

    private void Start()
    {
        EnsureContentRoot();
        RefreshFromAvailableData();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        ClearSpawnedRelics();
    }

    private void Update()
    {
        bool needsRefresh = false;

        if (runManager != RunManager.Instance)
        {
            Unsubscribe();
            ResolveRunManager();
            Subscribe();
            needsRefresh = true;
        }

        if (contentRoot == null)
        {
            EnsureContentRoot();
            needsRefresh = contentRoot != null || needsRefresh;
        }

        if (relicIconPrefab == null)
        {
            ResolveRelicIconPrefab();
            needsRefresh = relicIconPrefab != null || needsRefresh;
        }

        if (!needsRefresh && spawnedRelicObjects.Count == 0 && relicIconPrefab != null)
        {
            List<RelicBase> relics = ResolveRelics(runManager != null ? runManager.CurrentRunSnapshot : null);
            needsRefresh = relics != null && relics.Count > 0;
        }

        if (needsRefresh)
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
        if (contentRoot == null)
            return;

        ResolveRelicIconPrefab();

        List<RelicBase> relics = ResolveRelics(snapshot);
        bool hasRelics = relics != null && relics.Count > 0 && relicIconPrefab != null;

        ClearSpawnedRelics();
        UpdateContentVisibility(hasRelics);

        if (!hasRelics)
            return;

        for (int i = 0; i < relics.Count; i++)
        {
            RelicBase relic = relics[i];
            if (relic == null)
                continue;

            GameObject relicObject = Instantiate(relicIconPrefab, contentRoot, false);
            relicObject.name = string.IsNullOrWhiteSpace(relic.cardName)
                ? $"RelicUI_{i}"
                : $"RelicUI_{relic.cardName}";
            relicObject.SetActive(true);

            BattleRelicUIItem relicUiItem = relicObject.GetComponent<BattleRelicUIItem>() ??
                                            relicObject.GetComponentInChildren<BattleRelicUIItem>(true);
            if (relicUiItem != null)
                relicUiItem.Bind(relic);

            spawnedRelicObjects.Add(relicObject);
        }
    }

    private void EnsureContentRoot()
    {
        if (contentRoot == null)
        {
            Transform existing = transform.Find("Content");
            if (existing != null)
                contentRoot = existing as RectTransform;
        }

        if (contentRoot == null && createContentRootIfMissing)
        {
            GameObject contentObject = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(GridLayoutGroup));

            contentRoot = contentObject.GetComponent<RectTransform>();
            contentRoot.SetParent(transform, false);
            contentRoot.anchorMin = new Vector2(0f, 1f);
            contentRoot.anchorMax = new Vector2(0f, 1f);
            contentRoot.pivot = new Vector2(0f, 1f);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.sizeDelta = autoContentSize;
            contentRoot.localScale = autoContentScale;
        }

        if (contentRoot == null)
            return;

        layoutGroup = contentRoot.GetComponent<GridLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = contentRoot.gameObject.AddComponent<GridLayoutGroup>();
            layoutGroup.padding = new RectOffset(0, 0, 0, 0);
            layoutGroup.childAlignment = TextAnchor.UpperRight;
            layoutGroup.startCorner = GridLayoutGroup.Corner.UpperRight;
            layoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
            layoutGroup.cellSize = autoCellSize;
            layoutGroup.spacing = autoSpacing;
            layoutGroup.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            layoutGroup.constraintCount = 1;
        }
    }

    private void ResolveRelicIconPrefab()
    {
        if (relicIconPrefab != null)
            return;

        ShopUIManager shopUiManager = FindObjectOfType<ShopUIManager>(true);
        if (shopUiManager != null && shopUiManager.RelicIconPrefab != null)
        {
            relicIconPrefab = shopUiManager.RelicIconPrefab;
            return;
        }

        RunMapUI runMapUi = FindObjectOfType<RunMapUI>(true);
        if (runMapUi != null && runMapUi.RelicIconPrefab != null)
            relicIconPrefab = runMapUi.RelicIconPrefab;
    }

    private List<RelicBase> ResolveRelics(PlayerRunSnapshot snapshot)
    {
        if (snapshot != null && snapshot.relics != null)
            return snapshot.relics;

        if (runManager != null && runManager.RegisteredPlayer != null && runManager.RegisteredPlayer.relics != null)
            return runManager.RegisteredPlayer.relics;

        return new List<RelicBase>();
    }

    private void ClearSpawnedRelics()
    {
        for (int i = 0; i < spawnedRelicObjects.Count; i++)
        {
            GameObject relicObject = spawnedRelicObjects[i];
            if (relicObject != null)
                Destroy(relicObject);
        }

        spawnedRelicObjects.Clear();
    }

    private void UpdateContentVisibility(bool hasRelics)
    {
        if (contentRoot == null)
            return;

        GameObject contentObject = contentRoot.gameObject;
        if (contentObject == null)
            return;

        if (!hideContentRootWhenEmpty)
        {
            if (!contentObject.activeSelf)
                contentObject.SetActive(true);

            return;
        }

        if (contentObject == gameObject)
            return;

        if (contentObject.activeSelf != hasRelics)
            contentObject.SetActive(hasRelics);
    }
}
