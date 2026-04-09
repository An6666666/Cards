using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        SyncSelectionFromHover();
        owner?.NotifyButtonHighlighted(targetRect);
    }

    public void OnSelect(BaseEventData eventData)
    {
        owner?.NotifyButtonHighlighted(targetRect);
    }

    private void SyncSelectionFromHover()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return;
        }

        Selectable selectable = GetComponent<Selectable>();
        if (selectable == null || !selectable.IsInteractable() || !selectable.gameObject.activeInHierarchy)
        {
            return;
        }

        if (eventSystem.currentSelectedGameObject != selectable.gameObject)
        {
            eventSystem.SetSelectedGameObject(selectable.gameObject);
        }
    }
}
