using System.Collections.Generic;

public static class StartingDeckSelection
{
    private static readonly List<ElementType> selectedElements = new List<ElementType>(3);

    public static bool HasSelection => selectedElements.Count > 0;

    public static void SetSelection(IEnumerable<ElementType> elements)
    {
        selectedElements.Clear();
        if (elements == null)
            return;

        foreach (ElementType element in elements)
        {
            if (selectedElements.Count >= 3)
                break;

            if (!selectedElements.Contains(element))
            {
                selectedElements.Add(element);
            }
        }
    }

    public static bool TryGetSelectedElements(out IReadOnlyList<ElementType> elements)
    {
        if (selectedElements.Count == 0)
        {
            elements = null;
            return false;
        }

        elements = new List<ElementType>(selectedElements);
        return true;
    }

    public static void ClearSelection()
    {
        selectedElements.Clear();
    }
}