// Assets/Managers/UIFxController.cs
// 全域 UI 動效控制器（穩定版：滑入+淡入；無任何彈跳）
// 修正：RuleTempOld 一定加入 CanvasGroup、且不阻擋點擊；防連點；面板狀態去重
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

public class UIFxController : MonoBehaviour
{
    public static UIFxController Instance { get; private set; }

    [Header("Panel Defaults (slide + fade, no scale)")]
    [SerializeField] private Vector2 enterOffset = new Vector2(0, -80f);
    [SerializeField] private Vector2 exitOffset  = new Vector2(0, -80f);
    [SerializeField] private float showDur = 0.35f;
    [SerializeField] private float hideDur = 0.28f;
    [SerializeField] private Ease showEase = Ease.OutCubic;
    [SerializeField] private Ease hideEase = Ease.InCubic;
    [SerializeField] private bool unscaledTime = true;

    [Header("Counter swap")]
    [SerializeField] private float swapDur = 0.22f;

    [Header("Rule page")]
    [SerializeField] private float slideDur = 0.28f;
    [SerializeField] private float slidePixels = 420f;
    [SerializeField] private Ease slideEaseOut = Ease.OutQuad;
    [SerializeField] private Ease slideEaseIn  = Ease.OutQuad;

    private readonly Dictionary<RectTransform, Vector2> _originAnchors = new();
    private readonly HashSet<GameObject> _visible = new();   // 面板是否已顯示
    private bool _rulePagingBusy = false;                     // 防連點
    private GameObject _ruleTempOld;                          // 翻頁影分身（僅一個）

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private (RectTransform rt, CanvasGroup cg, Vector2 origin) Prepare(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();

        if (!_originAnchors.TryGetValue(rt, out var origin))
        {
            origin = rt.anchoredPosition;
            _originAnchors[rt] = origin;
        }

        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();

        return (rt, cg, origin);
    }

    // ===== Panel：顯示（滑入 + 淡入） =====
    public void ShowPanel(GameObject panel)
    {
        if (panel == null) return;
        if (_visible.Contains(panel)) return; // 已顯示不重播

        var (rt, cg, origin) = Prepare(panel);
        DOTween.Kill(rt); DOTween.Kill(cg);

        panel.SetActive(true);
        cg.blocksRaycasts = false;
        cg.interactable = false;

        rt.anchoredPosition = origin + enterOffset;
        cg.alpha = 0f;

        DOTween.Sequence().SetUpdate(unscaledTime)
            .Join(rt.DOAnchorPos(origin, showDur).SetEase(showEase))
            .Join(cg.DOFade(1f, showDur))
            .OnComplete(() =>
            {
                cg.blocksRaycasts = true;
                cg.interactable = true;
                _visible.Add(panel);
            });
    }

    // ===== Panel：隱藏（滑出 + 淡出） =====
    public void HidePanel(GameObject panel)
    {
        if (panel == null) return;
        if (!_visible.Contains(panel))
        {
            if (panel.activeSelf) panel.SetActive(false);
            return;
        }

        var (rt, cg, origin) = Prepare(panel);
        DOTween.Kill(rt); DOTween.Kill(cg);

        cg.blocksRaycasts = false;
        cg.interactable = false;

        DOTween.Sequence().SetUpdate(unscaledTime)
            .Join(rt.DOAnchorPos(origin + exitOffset, hideDur).SetEase(hideEase))
            .Join(cg.DOFade(0f, hideDur))
            .OnComplete(() =>
            {
                rt.anchoredPosition = origin;
                panel.SetActive(false);
                _visible.Remove(panel);
            });
    }

    // ===== Counter：淡入淡出切換（顯示 showBtn，隱藏 hideBtn） =====
    public void FadeSwapButtons(Button showBtn, Button hideBtn)
    {
        if (showBtn == null || hideBtn == null) return;

        var sb = showBtn.gameObject;
        var hb = hideBtn.gameObject;

        var scg = sb.GetComponent<CanvasGroup>() ?? sb.AddComponent<CanvasGroup>();
        var hcg = hb.GetComponent<CanvasGroup>() ?? hb.AddComponent<CanvasGroup>();

        DOTween.Kill(scg); DOTween.Kill(hcg);

        sb.SetActive(true);
        hb.SetActive(true);

        // 顯示者從 0 淡到 1；隱藏者從 1 淡到 0
        scg.alpha = 0f; scg.blocksRaycasts = false; scg.interactable = false;
        hcg.alpha = 1f; hcg.blocksRaycasts = true;  hcg.interactable = true;

        scg.DOFade(1f, swapDur).OnComplete(() =>
        {
            scg.blocksRaycasts = true;
            scg.interactable = true;
        });
        hcg.DOFade(0f, swapDur).OnComplete(() =>
        {
            hcg.blocksRaycasts = false;
            hcg.interactable = false;
            hb.SetActive(false);
        });
    }

    // ===== Rule：滑動交叉翻頁（修正 CanvasGroup & 不阻擋點擊 & 防連點） =====
    public void CrossSlideRulePage(Image hostImage, Sprite nextSprite, bool toRight)
    {
        // 基本防呆
        if (hostImage == null || nextSprite == null) return;

        // 若目標圖跟現有圖相同，直接跳過，避免「重複顯示第一張」
        if (hostImage.sprite == nextSprite) return;

        // 防連點
        if (_rulePagingBusy) return;
        _rulePagingBusy = true;

        // 確保 Host 具備 CanvasGroup（供淡入用）
        var hostGO = hostImage.gameObject;
        var hostCg = EnsureCanvasGroup(hostGO, initialAlpha: 0f); // 先設為 0，進場再淡入
        var hostRT = hostImage.rectTransform;
        var origin = hostRT.anchoredPosition;

        // 事先清掉任何殘留的影分身
        ClearRuleTempOld();

        // 建立影分身（舊圖）：一定加 CanvasGroup，且不阻擋點擊
        _ruleTempOld = new GameObject("RuleTempOld", typeof(RectTransform), typeof(Image));
        var oldRT = (RectTransform)_ruleTempOld.transform;
        oldRT.SetParent(hostRT.parent, false);
        oldRT.anchorMin = hostRT.anchorMin;
        oldRT.anchorMax = hostRT.anchorMax;
        oldRT.pivot = hostRT.pivot;
        oldRT.sizeDelta = hostRT.sizeDelta;
        oldRT.anchoredPosition = origin;

        var oldImg = _ruleTempOld.GetComponent<Image>();
        oldImg.sprite = hostImage.sprite;             // 這裡先抓「舊的」
        oldImg.preserveAspect = hostImage.preserveAspect;
        oldImg.raycastTarget = false;                 // 不擋按鈕

        var oldCg = EnsureCanvasGroup(_ruleTempOld, initialAlpha: 1f);
        oldCg.blocksRaycasts = false;
        oldCg.interactable = false;

        // Host 換新圖，準備從反方向滑入
        hostImage.sprite = nextSprite;

        // 方向與位移
        var dir = toRight ? 1f : -1f;
        var startX = -dir * slidePixels;
        var endX = dir * slidePixels;

        // 殺掉相關 Tween（以目標為 id）
        DOTween.Kill(hostRT, false);
        DOTween.Kill(hostCg, false);
        DOTween.Kill(oldRT, false);
        DOTween.Kill(oldCg, false);

        // 設定起始狀態
        hostRT.anchoredPosition = origin + new Vector2(startX, 0);
        hostCg.alpha = 0f; // 新圖一開始是隱形

        // 正式動畫
        DOTween.Sequence().SetUpdate(unscaledTime)
            // 新圖滑入 + 淡入
            .Join(hostRT.DOAnchorPos(origin, slideDur).SetEase(slideEaseOut))
            .Join(hostCg.DOFade(1f, slideDur))
            // 舊圖滑出 + 淡出
            .Join(oldRT.DOAnchorPos(origin + new Vector2(endX, 0), slideDur).SetEase(slideEaseIn))
            .Join(oldCg.DOFade(0f, slideDur))
            .OnComplete(() =>
            {
                // 清理
                ClearRuleTempOld();

                // 復位 Host
                hostRT.anchoredPosition = origin;
                hostCg.alpha = 1f;

                _rulePagingBusy = false;
            });
    }
    
    // 小工具：保證有 CanvasGroup（沒有就加），並回傳
    private CanvasGroup EnsureCanvasGroup(GameObject go, float initialAlpha = 1f)
    {
        if (go == null) return null;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = initialAlpha;
        return cg;
}

    // 小工具：清理舊的影分身
    private void ClearRuleTempOld()
    {
        if (_ruleTempOld != null)
        {
            DOTween.Kill(_ruleTempOld.transform, complete:false);
            DOTween.Kill(_ruleTempOld.GetComponent<CanvasGroup>(), complete:false);
            Destroy(_ruleTempOld);
            _ruleTempOld = null;
        }
    }
}
