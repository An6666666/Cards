using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnergyPanelView : MonoBehaviour
{
    [Header("Container")]
    [SerializeField] private Transform content;          // 放圖示的容器（建議有 HorizontalLayoutGroup）
    [Header("Prefab")]
    [SerializeField] private Image iconPrefab;           // 一個「UI Image 當根」的預置體

    [Header("Options")]
    [SerializeField] private bool showMaxAsGray = false; // 若開啟，會先鋪滿 max，再把多餘的設為灰色（空槽）
    [SerializeField] private Color emptyColor = new Color(1f,1f,1f,0.25f);

    private readonly Queue<Image> pool = new Queue<Image>(32);
    private Transform poolRoot;

    private void Awake()
    {
        if (content == null) content = transform;

        poolRoot = new GameObject("[EnergyIconPool]").transform;
        poolRoot.SetParent(transform, false);
        poolRoot.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        EnergyUIBus.Register(this);
    }

    private void OnDisable()
    {
        EnergyUIBus.Unregister(this);
    }

    private Image GetIcon(Transform parent)
    {
        var img = pool.Count > 0 ? pool.Dequeue() : Instantiate(iconPrefab);
        img.transform.SetParent(parent, false);

        var rt = img.transform as RectTransform;
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.anchoredPosition3D = Vector3.zero;
            if (rt.sizeDelta.x <= 1f || rt.sizeDelta.y <= 1f)
                rt.sizeDelta = new Vector2(32, 32); // 保底尺寸
        }

        img.gameObject.SetActive(true);
        return img;
    }

    private void ReturnAll(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var t = parent.GetChild(i);
            var img = t.GetComponent<Image>();
            if (img == null) { Destroy(t.gameObject); continue; }
            img.gameObject.SetActive(false);
            img.transform.SetParent(poolRoot, false);
            pool.Enqueue(img);
        }
    }

    /// <summary>
    /// 更新能量顯示：current = 當前能量，max = 上限
    /// </summary>
    public void Refresh(int current, int max)
    {
        if (content == null || iconPrefab == null) return;

        current = Mathf.Max(0, current);
        max = Mathf.Max(current, max); // 至少要 >= current

        ReturnAll(content);

        if (showMaxAsGray)
        {
            // 先鋪滿 max 個，前面 current 用原色，剩下設置為灰色（空槽）
            for (int i = 0; i < max; i++)
            {
                var icon = GetIcon(content);
                if (i < current)
                {
                    icon.color = Color.white;
                }
                else
                {
                    icon.color = emptyColor;
                }
            }
        }
        else
        {
            // 只生成 current 個
            for (int i = 0; i < current; i++)
            {
                var icon = GetIcon(content);
                icon.color = Color.white;
            }
        }

        // 確保立即排版
        Canvas.ForceUpdateCanvases();
    }
}
