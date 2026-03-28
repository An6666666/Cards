using UnityEngine;
using UnityEngine.EventSystems;

public class TitleMenuSelectionTarget : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    private TitleUI owner;
    private RectTransform targetRect;

    public void Initialize(TitleUI selectionOwner, RectTransform rectTransform)
    {
        owner = selectionOwner;
        targetRect = rectTransform;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.NotifyButtonHighlighted(targetRect);
    }

    public void OnSelect(BaseEventData eventData)
    {
        owner?.NotifyButtonHighlighted(targetRect);
    }
}
