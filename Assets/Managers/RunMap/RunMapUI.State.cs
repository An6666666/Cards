using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class RunMapUI : MonoBehaviour
{
    private void RefreshNodeStates()
    {
        if (runManager == null)
            return;

        foreach (KeyValuePair<MapNodeData, Button> pair in nodeButtons)
        {
            MapNodeData node = pair.Key;
            Button button = pair.Value;
            if (button == null)
                continue;

            bool isSelectable = runManager.IsNodeSelectable(node);
            button.interactable = isSelectable;
            if (!isSelectable)
                SetNodeHoverScale(node, false);

            if (!(button.targetGraphic is Image image))
                continue;

            if (runManager.CurrentNode == node)
                image.color = currentColor;
            else if (isSelectable && runManager.AllowAnyNodeEntry)
                image.color = defaultColor;
            else if (node.IsCompleted)
                image.color = completedColor;
            else if (isSelectable)
                image.color = defaultColor;
            else
                image.color = lockedColor;
        }
    }

    private void RefreshLegendPanel()
    {
        if (legendPanel == null || runManager == null)
            return;

        bool isInEvent = runManager.ActiveNode != null && runManager.ActiveNode.NodeType == MapNodeType.Event;
        legendPanel.SetActive(!isInEvent);
    }

    private void OnNodeClicked(MapNodeData node)
    {
        if (runManager == null || node == null)
            return;

        runManager.TryEnterNode(node);
    }

    private void OnNodePointerEnter(MapNodeData node)
    {
        if (runManager == null || node == null || !runManager.IsNodeSelectable(node))
            return;

        SetNodeHoverScale(node, true);
    }

    private void OnNodePointerExit(MapNodeData node)
    {
        SetNodeHoverScale(node, false);
    }

    private void SetNodeHoverScale(MapNodeData node, bool hovered)
    {
        if (node == null || !nodeRects.TryGetValue(node, out RectTransform rect) || rect == null)
            return;

        Vector3 baseScale = nodeBaseScales.TryGetValue(node, out Vector3 storedScale)
            ? storedScale
            : Vector3.one;

        rect.localScale = hovered
            ? baseScale * selectableNodeHoverScale
            : baseScale;
    }
}
