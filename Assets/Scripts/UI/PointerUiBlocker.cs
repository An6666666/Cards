using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public static class PointerUiBlocker
{
    private static readonly List<RaycastResult> UiHits = new List<RaycastResult>(8);

    public static bool IsPointerBlockedByUi()
    {
        if (EventSystem.current == null)
            return false;

        if (EventSystem.current.IsPointerOverGameObject())
            return true;

        var pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        UiHits.Clear();
        EventSystem.current.RaycastAll(pointerEventData, UiHits);
        return UiHits.Count > 0;
    }

    public static bool IsPointerBlockedByOtherUi(Transform allowedRoot)
    {
        if (allowedRoot == null || EventSystem.current == null)
            return false;

        var pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        UiHits.Clear();
        EventSystem.current.RaycastAll(pointerEventData, UiHits);
        if (UiHits.Count == 0)
            return false;

        var topHit = UiHits[0].gameObject;
        if (topHit == null)
            return false;

        return !topHit.transform.IsChildOf(allowedRoot);
    }
}
