using UnityEngine;

public class HoverStatusTrigger : MonoBehaviour
{
    [SerializeField] private StatusPanel_Text panel;

    private void Awake()
    {
        if (panel == null)
            panel = FindObjectOfType<StatusPanel_Text>(true); // true = 包含 inactive
    }

    private void OnMouseEnter()
    {
        if (panel != null)
            panel.SetTarget(gameObject);
    }

    private void OnMouseExit()
    {
        if (panel != null)
            panel.ClearTarget();
    }
}
