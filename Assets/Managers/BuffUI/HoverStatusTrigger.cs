using UnityEngine;

public class HoverStatusTrigger : MonoBehaviour
{
    private StatusPanel_Text panel;
    private bool hovering;

    private void Awake()
    {
        panel = FindObjectOfType<StatusPanel_Text>(true);
    }

    private void OnMouseEnter()
    {
        if (PointerUiBlocker.IsPointerBlockedByUi())
            return;

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

    private void Update()
    {
        if (!hovering || !PointerUiBlocker.IsPointerBlockedByUi())
            return;

        hovering = false;
        if (panel != null)
            panel.ClearTarget();
    }

    private void OnDisable()
    {
        if (hovering && panel != null)
            panel.ClearTarget();
    }
}
