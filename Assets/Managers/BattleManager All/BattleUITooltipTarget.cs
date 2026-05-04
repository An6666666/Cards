using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class BattleUITooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tooltip")]
    [SerializeField] private BattleUITooltipController tooltipController;
    [SerializeField] private string title;
    [TextArea(2, 6)]
    [SerializeField] private string description;

    [Header("Position")]
    [SerializeField] private Vector2 positionOffset = new Vector2(-260f, 60f);

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        ResolveController();
        EnsureRaycastTarget();
    }

    private void OnValidate()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }
    }

    private void OnDisable()
    {
        tooltipController?.Hide();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ResolveController();
        tooltipController?.Show(rectTransform, title, description, positionOffset);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltipController?.Hide();
    }

    private void ResolveController()
    {
        if (tooltipController != null)
        {
            return;
        }

        tooltipController = GetComponentInParent<BattleUITooltipController>(true);
        if (tooltipController == null)
        {
            tooltipController = FindObjectOfType<BattleUITooltipController>(true);
        }
    }

    private void EnsureRaycastTarget()
    {
        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = true;
        }
    }
}
