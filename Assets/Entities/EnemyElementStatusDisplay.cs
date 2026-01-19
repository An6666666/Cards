using System;                                    // 引用 System 命名空間（提供基礎型別、事件等）
using System.Collections.Generic;                // 引用泛型集合（List、Dictionary、HashSet）
using DG.Tweening;                               // 引用 DOTween（用於位置/縮放/淡出等補間動畫）
using UnityEngine;                               // 引用 UnityEngine 相關 API

[DisallowMultipleComponent]                      // 限制同一物件上不可重複掛載此元件
public class EnemyElementStatusDisplay : MonoBehaviour
{
    [Serializable]                               // 允許在 Inspector 中顯示/序列化此內部類
    private class ElementSprite
    {
        public ElementType type;                 // 對應的元素類型（Fire/Water/Thunder/Ice/Wood）
        public Sprite sprite;                    // 此元素對應要顯示的圖示
    }

    [SerializeField] private Enemy enemy;        // 指向要監聽的 Enemy；若未指定會自動找父物件上的 Enemy
    [SerializeField] private Transform iconRoot; // 生成圖示的父節點（放圖示的容器），未指定則用自身 transform
    [SerializeField] private float iconSpacing = 0.6f;      // 圖示彼此的水平間距
    [SerializeField] private float iconScale = 0.12f;       // 圖示縮放比例
    [SerializeField] private string sortingLayerName = "UI two"; // SpriteRenderer 的 Sorting Layer 名稱
    [SerializeField] private int baseSortingOrder = 5;      // 圖示的基礎排序順序（會隨索引 i 疊加）
    [SerializeField] private List<ElementSprite> elementSprites = new List<ElementSprite>(); 
    // Inspector 設定：元素類型 ↔ 對應圖示 的對照表

    [Header("DOTween Settings")]                 // 在 Inspector 分組顯示：DOTween 設定
    [SerializeField] private bool animateWithDOTween = true;      // 是否啟用 DOTween 動畫
    [SerializeField] private float layoutTweenDuration = 0.2f;    // 既有圖示重新排版時的移動/縮放動畫時間
    [SerializeField] private Ease layoutEase = Ease.OutQuad;      // 既有圖示排版的動畫曲線
    [SerializeField] private float spawnTweenDuration = 0.25f;    // 新圖示生成時的進場動畫時間
    [SerializeField] private Ease spawnEase = Ease.OutBack;       // 新圖示進場動畫曲線
    [SerializeField] private float removalTweenDuration = 0.18f;  // 圖示移除時的退場動畫時間
    [SerializeField] private Ease removalEase = Ease.InBack;      // 圖示退場動畫曲線
    [SerializeField] private Vector3 removalMoveOffset = new Vector3(0f, 0.1f, 0f); 
    // 退場時額外位移量（例如往上飄一點再縮小消失）

    private class IconEntry
    {
        public SpriteRenderer Renderer;          // 此元素圖示的 SpriteRenderer
        public Tween MoveTween;                  // 記錄位移補間（便於之後 Kill）
        public Tween ScaleTween;                 // 記錄縮放補間（便於之後 Kill）
    }

    private readonly Dictionary<ElementType, IconEntry> activeIcons = new Dictionary<ElementType, IconEntry>();
    // 目前已生成並顯示中的元素圖示快取（避免重複生成、好做增刪比對）

    private readonly HashSet<GameObject> spawnedIcons = new HashSet<GameObject>();
    // 所有曾生成的圖示物件集合（用於清理時逐一銷毀）

    private Dictionary<ElementType, Sprite> spriteLookup;   // 元素 → 對應圖示 的查表快取
    private bool spriteLookupDirty = true;                  // 當 Inspector 配置改變時，標記需要重建查表
    private Enemy subscribedEnemy;                          // 目前已訂閱事件的 Enemy（用於退訂）

    private void Reset()
    {
        enemy = GetComponentInParent<Enemy>();  // 預設從父物件自動抓 Enemy
        iconRoot = transform;                   // 預設用自身作為圖示容器
    }

    private void Awake()
    {
        if (iconRoot == null)
        {
            iconRoot = transform;               // 若未指定 iconRoot，改用自身
        }

        MarkSpriteLookupDirty();                // 標記圖示查表需要重建
    }

    private void OnEnable()
    {
        SubscribeToEnemy();                     // 啟用時訂閱 Enemy 的元素變化事件
        RefreshIcons();                         // 立即刷新一次圖示（與目前元素狀態同步）
    }

    private void OnDisable()
    {
        UnsubscribeFromEnemy();                 // 停用時退訂事件
        ClearIcons();                           // 清空所有生成的圖示
    }

    private void OnDestroy()
    {
        UnsubscribeFromEnemy();                 // 銷毀前確保退訂，避免事件殘留
    }

    private void OnValidate()
    {
        if (iconRoot == null)
        {
            iconRoot = transform;               // 編輯器中修改時也要維持預設
        }

        // 參數下限保護，避免負值造成異常
        iconSpacing = Mathf.Max(0f, iconSpacing);
        iconScale = Mathf.Max(0f, iconScale);
        layoutTweenDuration = Mathf.Max(0f, layoutTweenDuration);
        spawnTweenDuration = Mathf.Max(0f, spawnTweenDuration);
        removalTweenDuration = Mathf.Max(0f, removalTweenDuration);
        MarkSpriteLookupDirty();                // Inspector 有改變 → 之後重建圖示查表
    }

    private void SubscribeToEnemy()
    {
        Enemy target = enemy != null ? enemy : GetComponentInParent<Enemy>(); // 取得目標 Enemy
        if (target == subscribedEnemy)
        {
            return;                             // 已訂閱同一個就不重複處理
        }

        UnsubscribeFromEnemy();                 // 先退訂舊的（若有）

        enemy = target;                         // 同步欄位
        if (enemy != null)
        {
            enemy.ElementTagsChanged += HandleEnemyElementTagsChanged; // 當元素標籤變化時回呼
            subscribedEnemy = enemy;            // 記錄目前已訂閱對象
        }
    }

    private void UnsubscribeFromEnemy()
    {
        if (subscribedEnemy != null)
        {
            subscribedEnemy.ElementTagsChanged -= HandleEnemyElementTagsChanged; // 解除事件訂閱
            subscribedEnemy = null;
        }
    }

    private void HandleEnemyElementTagsChanged(Enemy changedEnemy)
    {
        if (changedEnemy == enemy)              // 確認事件來源就是我們關注的那個 Enemy
        {
            RefreshIcons();                     // 重新計算並更新圖示
        }
    }

    private void MarkSpriteLookupDirty()
    {
        spriteLookupDirty = true;               // 標記查表過期，待下次使用時重建
    }

    private void RebuildSpriteLookupIfNeeded()
    {
        if (!spriteLookupDirty)
        {
            return;                             // 若沒有被標記為髒，就不用重建
        }

        spriteLookupDirty = false;              // 立即清掉髒標記（開始重建）

        if (spriteLookup == null)
        {
            spriteLookup = new Dictionary<ElementType, Sprite>(); // 初始化查表
        }
        else
        {
            spriteLookup.Clear();               // 已存在就清空重建
        }

        if (elementSprites == null)
        {
            elementSprites = new List<ElementSprite>(); // 保底，避免 Null
        }

        // 依照 Inspector 配置建立 元素→圖示 的快取對照
        foreach (ElementSprite mapping in elementSprites)
        {
            if (mapping == null || mapping.sprite == null)
            {
                continue;                       // 跳過未配置的項目
            }

            spriteLookup[mapping.type] = mapping.sprite; // 設定對應圖示
        }
    }

    private void RefreshIcons()
    {
        RebuildSpriteLookupIfNeeded();          // 必要時重建圖示查表

        Enemy target = enemy ?? subscribedEnemy; // 取得目前關注的 Enemy
        if (target == null)
        {
            ClearIcons();                       // 若沒有對象，清空顯示
            return;
        }

        // 取得目前 Enemy 擁有的元素標籤（外部需提供 GetElementTags）
        List<ElementType> tags = new List<ElementType>(target.GetElementTags());
        if (tags.Count == 0)
        {
            ClearIcons();                       // 沒有任何元素 → 清空圖示
            return;
        }

        HashSet<ElementType> desired = new HashSet<ElementType>(tags); // 期望顯示的集合

        bool animate = animateWithDOTween && Application.isPlaying; // 僅在遊戲執行中啟用動畫

        // 先處理需要移除的圖示（activeIcons - desired）
        if (activeIcons.Count > 0)
        {
            List<KeyValuePair<ElementType, IconEntry>> removalBuffer = null;
            foreach (KeyValuePair<ElementType, IconEntry> kvp in activeIcons)
            {
                if (!desired.Contains(kvp.Key)) // 若現有圖示的元素不在期望清單中
                {
                    if (removalBuffer == null)
                    {
                        removalBuffer = new List<KeyValuePair<ElementType, IconEntry>>();
                    }

                    removalBuffer.Add(kvp);     // 記錄待移除
                }
            }

            if (removalBuffer != null)
            {
                foreach (KeyValuePair<ElementType, IconEntry> kvp in removalBuffer)
                {
                    RemoveIcon(kvp.Value, animate); // 播退場或立即移除
                    activeIcons.Remove(kvp.Key);     // 從快取移除
                }
            }
        }

        // 計算水平排版的起始偏移（使整排圖示置中）
        float startOffset = tags.Count > 1 ? -iconSpacing * (tags.Count - 1) * 0.5f : 0f;

        // 逐一確保每個需要顯示的元素都有圖示，並更新其位置/縮放/排序
        for (int i = 0; i < tags.Count; i++)
        {
            ElementType type = tags[i];
            bool isNew = false;                 // 標記是否為新生成的圖示

            if (!activeIcons.TryGetValue(type, out IconEntry entry) || entry == null || entry.Renderer == null)
            {
                entry = CreateIcon(type);       // 尚未生成 → 建立圖示
                if (entry == null)
                {
                    if (activeIcons.ContainsKey(type))
                    {
                        activeIcons.Remove(type);// 建立失敗則清理殘留快取
                    }
                    continue;
                }

                activeIcons[type] = entry;      // 放入快取
                isNew = true;                   // 標記為新圖示（用進場動畫）
            }

            Vector3 targetPosition = new Vector3(startOffset + iconSpacing * i, 0f, 0f); // 排版位置
            UpdateIconEntry(entry, targetPosition, iconScale, i, isNew, animate);        // 應用動畫與排序
        }
    }

    private IconEntry CreateIcon(ElementType type)
    {
        RebuildSpriteLookupIfNeeded();          // 確保查表可用
        if (spriteLookup == null || !spriteLookup.TryGetValue(type, out Sprite sprite) || sprite == null)
        {
            return null;                        // 無對應圖示則不建立
        }

        Transform parent = iconRoot != null ? iconRoot : transform; // 父節點（容器）
        GameObject iconObject = new GameObject(type + " Icon");     // 新建圖示物件
        iconObject.transform.SetParent(parent, false);              // 設定為容器子物件（不保留世界座標）
        iconObject.transform.localPosition = Vector3.zero;          // 初始位置（之後由排版更新）
        iconObject.transform.localScale = Vector3.zero;             // 初始縮放 0（方便做進場動畫）

        SpriteRenderer renderer = iconObject.AddComponent<SpriteRenderer>(); // 加上渲染元件
        renderer.sprite = sprite;                                     // 指定圖示
        renderer.sortingLayerName = sortingLayerName;                 // 設定 Sorting Layer
        renderer.sortingOrder = baseSortingOrder;                     // 設定基礎排序
        renderer.enabled = true;                                      // 啟用渲染

        spawnedIcons.Add(iconObject);                                 // 記錄生成物件（清理用）

        return new IconEntry
        {
            Renderer = renderer                                        // 回傳封裝的條目
        };
    }

    private void ClearIcons()
    {
        if (activeIcons.Count == 0)
        {
            return;                             // 無圖示可清
        }

        foreach (IconEntry entry in activeIcons.Values)
        {
            DestroyIcon(entry, true);           // 立刻銷毀（不播放退場動畫）
        }

        activeIcons.Clear();                    // 清空快取

        if (spawnedIcons.Count > 0)
        {
            List<GameObject> iconsToClear = new List<GameObject>(spawnedIcons);
            foreach (GameObject icon in iconsToClear)
            {
                DestroyIconObject(icon);        // 逐一銷毀殘留的物件
            }

            spawnedIcons.Clear();               // 清空生成紀錄
        }
    }

    private void UpdateIconEntry(IconEntry entry, Vector3 targetPosition, float targetScale, int sortingIndex, bool isNew, bool animate)
    {
        if (entry == null || entry.Renderer == null)
        {
            return;                             // 防禦：若 renderer 遺失則不處理
        }

        SpriteRenderer renderer = entry.Renderer;
        Transform rendererTransform = renderer.transform;
        if (rendererTransform == null)
        {
            return;                             // 防禦：變換遺失
        }

        ApplySorting(renderer, sortingIndex);   // 依索引調整 sortingOrder（避免重疊）

        Vector3 scaleVector = Vector3.one * targetScale; // 目標縮放向量
        float moveDuration = isNew ? spawnTweenDuration : layoutTweenDuration; // 新圖示用進場時間
        float scaleDuration = isNew ? spawnTweenDuration : layoutTweenDuration; // 新圖示用進場時間
        Ease moveEase = isNew ? spawnEase : layoutEase;   // 新舊圖示用不同曲線
        Ease scaleEase = isNew ? spawnEase : layoutEase;

        KillTweens(entry);                      // 先停止舊有補間，避免衝突

        if (animate && moveDuration > 0f)
        {
            entry.MoveTween = rendererTransform.DOLocalMove(targetPosition, moveDuration) // 位置補間
                .SetEase(moveEase)
                .SetTarget(renderer.gameObject); // 將 tween 綁定到物件，便於 Kill
        }
        else
        {
            rendererTransform.localPosition = targetPosition; // 不動畫：直接設定座標
        }

        if (animate && scaleDuration > 0f)
        {
            entry.ScaleTween = rendererTransform.DOScale(scaleVector, scaleDuration)      // 縮放補間
                .SetEase(scaleEase)
                .SetTarget(renderer.gameObject);
        }
        else
        {
            rendererTransform.localScale = scaleVector; // 不動畫：直接設定縮放
        }

        renderer.gameObject.SetActive(true);    // 確保物件啟用
    }

    private void RemoveIcon(IconEntry entry, bool animate)
    {
        DestroyIcon(entry, !animate);           // animate = true → 播退場；false → 立即毀
    }

    private void DestroyIcon(IconEntry entry, bool immediate)
    {
        if (entry == null)
        {
            return;
        }

        SpriteRenderer renderer = entry.Renderer;
        Transform rendererTransform = renderer != null ? renderer.transform : null;

        KillTweens(entry);                      // 停止任何進行中的補間

        if (renderer == null)
        {
            return;                             // 已被外部毀掉就不處理
        }

        // 若允許播放退場動畫、且在執行狀態中
        if (!immediate && rendererTransform != null && removalTweenDuration > 0f && Application.isPlaying)
        {
            Vector3 startPosition = rendererTransform.localPosition;     // 當前位置
            Vector3 endPosition = startPosition + removalMoveOffset;     // 退場位移後的位置
            if (removalMoveOffset != Vector3.zero)
            {
                entry.MoveTween = rendererTransform.DOLocalMove(endPosition, removalTweenDuration)
                    .SetEase(removalEase)
                    .SetTarget(renderer.gameObject)
                    .OnComplete(() => entry.MoveTween = null);           // 播完把記錄清空
            }
            entry.ScaleTween = rendererTransform.DOScale(Vector3.zero, removalTweenDuration)
                .SetEase(removalEase)
                .SetTarget(renderer.gameObject)
                .OnComplete(() =>
                {
                    entry.ScaleTween = null;                              // 清空縮放 tween 記錄
                    DestroyIconObject(renderer.gameObject);               // 動畫結束後再銷毀物件
                });
        }
        else
        {
            DestroyIconObject(renderer.gameObject);  // 立即銷毀（不播放動畫）
        }
    }

    private void DestroyIconObject(GameObject iconObject)
    {
        if (iconObject == null)
        {
            return;
        }

        DOTween.Kill(iconObject);               // 殺掉綁在此物件上的所有 tween，避免殘留回呼

        if (Application.isPlaying)
        {
            Destroy(iconObject);                // 遊戲中使用 Destroy
        }
        else
        {
            DestroyImmediate(iconObject);       // 編輯器模式下立刻銷毀
        }

        spawnedIcons.Remove(iconObject);        // 從生成紀錄中移除
    }

    private static void KillTweens(IconEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        if (entry.MoveTween != null)
        {
            entry.MoveTween.Kill();             // 停掉位移補間
            entry.MoveTween = null;
        }

        if (entry.ScaleTween != null)
        {
            entry.ScaleTween.Kill();            // 停掉縮放補間
            entry.ScaleTween = null;
        }
    }

    private void ApplySorting(SpriteRenderer renderer, int index)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sortingLayerName = sortingLayerName; // 指定 Sorting Layer
        renderer.sortingOrder = baseSortingOrder + index; // 依索引遞增，確保左右圖示不互蓋
    }
}
