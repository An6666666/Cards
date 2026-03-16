using System.Collections.Generic;
using UnityEngine;

public static class StartingDeckSelection
{
    public const int DefaultMaxSelections = 3;

    private static readonly List<ElementType> pendingSelectedElements = new List<ElementType>(DefaultMaxSelections);
    private static readonly List<ElementType> runSelectedElements = new List<ElementType>(DefaultMaxSelections);

    public static bool HasSelection => pendingSelectedElements.Count > 0;
    public static bool HasRunSelection => runSelectedElements.Count > 0;

    public static void SetSelection(IEnumerable<ElementType> elements, int maxSelections = DefaultMaxSelections)
    {
        pendingSelectedElements.Clear();
        runSelectedElements.Clear();
        if (elements == null)
            return;

        maxSelections = Mathf.Max(1, maxSelections);
        if (pendingSelectedElements.Capacity < maxSelections)
        {
            pendingSelectedElements.Capacity = maxSelections;
        }

        if (runSelectedElements.Capacity < maxSelections)
        {
            runSelectedElements.Capacity = maxSelections;
        }

        foreach (ElementType element in elements)
        {
            if (pendingSelectedElements.Count >= maxSelections)
                break;

            if (!pendingSelectedElements.Contains(element))
            {
                pendingSelectedElements.Add(element);
                runSelectedElements.Add(element);
            }
        }
    }

    public static bool TryGetSelectedElements(out IReadOnlyList<ElementType> elements)
    {
        if (pendingSelectedElements.Count == 0)
        {
            elements = null;
            return false;
        }

        elements = new List<ElementType>(pendingSelectedElements);
        return true;
    }

    public static bool TryGetRunSelectedElements(out IReadOnlyList<ElementType> elements)
    {
        if (runSelectedElements.Count == 0)
        {
            elements = null;
            return false;
        }

        elements = new List<ElementType>(runSelectedElements);
        return true;
    }

    public static void ClearPendingSelection()
    {
        pendingSelectedElements.Clear();
    }

    public static void ClearSelection()
    {
        pendingSelectedElements.Clear();
        runSelectedElements.Clear();
    }
}
