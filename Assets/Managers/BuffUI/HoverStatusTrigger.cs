using UnityEngine;

public class HoverStatusTrigger : MonoBehaviour
{
    private StatusPanel_Text panel;
    private bool hovering = false;

    private void Awake()
    {
        panel = FindObjectOfType<StatusPanel_Text>(true);
    }

    private void OnMouseEnter()
    {
        hovering = true;
        if (panel != null)
            panel.SetTarget(gameObject);
    }

    private void OnMouseExit()
    {
        hovering = false;
        if (panel != null)
            panel.ClearTarget();
    }

    private void OnDisable()
    {
        // 防止物件被 Destroy 或 Disable 時狀態欄卡住
        if (hovering && panel != null)
            panel.ClearTarget();
    }
}
