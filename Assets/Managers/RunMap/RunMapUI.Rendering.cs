using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class RunMapUI : MonoBehaviour
{
    private void ClearMap()
    {
        nodeButtons.Clear();
        nodeRects.Clear();
        nodeBaseScales.Clear();
        hasBuilt = false;

        if (mapContainer == null)
            return;

        if (nodesRoot != null)
        {
            for (int i = nodesRoot.childCount - 1; i >= 0; i--)
                Destroy(nodesRoot.GetChild(i).gameObject);
        }

        if (linesRoot != null)
        {
            for (int i = linesRoot.childCount - 1; i >= 0; i--)
                Destroy(linesRoot.GetChild(i).gameObject);
        }

        if (nodesRoot != null || linesRoot != null)
            return;

        for (int i = mapContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = mapContainer.GetChild(i);
            if (bgRect != null && child == bgRect)
                continue;

            Destroy(child.gameObject);
        }
    }

    private Sprite GetIcon(MapNodeType type)
    {
        return type switch
        {
            MapNodeType.Battle => battleIcon,
            MapNodeType.EliteBattle => eliteIcon,
            MapNodeType.Shop => shopIcon,
            MapNodeType.Rest => restIcon,
            MapNodeType.Event => eventIcon,
            MapNodeType.Boss => bossIcon,
            _ => null
        };
    }

    private void ResizeContentAndBG(float minYAfterShift)
    {
        if (mapContainer == null)
            return;

        float contentHeight = (-minYAfterShift) + bottomPadding;
        if (viewport != null)
            contentHeight = Mathf.Max(contentHeight, viewport.rect.height);

        mapContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

        if (bgRect == null)
            return;

        bgRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.SetAsFirstSibling();
    }

    private void ClearLines()
    {
        if (linesRoot != null)
        {
            for (int i = linesRoot.childCount - 1; i >= 0; i--)
                Destroy(linesRoot.GetChild(i).gameObject);

            return;
        }

        if (mapContainer == null)
            return;

        for (int i = mapContainer.childCount - 1; i >= 0; i--)
        {
            GameObject go = mapContainer.GetChild(i).gameObject;
            if (go.name.StartsWith("Line_"))
                Destroy(go);
        }
    }

    private void ApplyPaddingAndResize(float minY, float maxY)
    {
        builtMinY = minY;
        builtMaxY = maxY;

        float shiftY = (-topPadding) - maxY;

        if (nodesRoot != null)
            nodesRoot.anchoredPosition += new Vector2(0f, shiftY);
        else
            ShiftAllNodes(shiftY);

        if (linesRoot != null)
            linesRoot.anchoredPosition += new Vector2(0f, shiftY);

        if (nodesRoot == null || linesRoot == null)
        {
            ClearLines();
            CreateConnections();
        }

        ResizeContentAndBG(minY + shiftY);
    }

    private void CreateConnections()
    {
        if (connectionLinePrefab == null)
            return;

        foreach (KeyValuePair<MapNodeData, Button> pair in nodeButtons)
        {
            MapNodeData node = pair.Key;
            if (!nodeRects.TryGetValue(node, out RectTransform startRect))
                continue;

            IReadOnlyList<MapNodeData> nextNodes = node.NextNodes;
            if (nextNodes == null)
                continue;

            for (int i = 0; i < nextNodes.Count; i++)
            {
                MapNodeData nextNode = nextNodes[i];
                if (!nodeRects.TryGetValue(nextNode, out RectTransform endRect))
                    continue;

                Transform lineParent = linesRoot != null ? linesRoot : mapContainer;
                Image lineInstance = Instantiate(connectionLinePrefab, lineParent);
                lineInstance.name = $"Line_{node.NodeId}_to_{nextNode.NodeId}";

                RectTransform lineRect = lineInstance.rectTransform;
                lineRect.anchorMin = new Vector2(0.5f, 1f);
                lineRect.anchorMax = new Vector2(0.5f, 1f);
                lineRect.pivot = new Vector2(0.5f, 0.5f);

                Vector2 start = GetConnectionAnchorPosition(node, startRect);
                Vector2 end = GetConnectionAnchorPosition(nextNode, endRect);

                Vector2 direction = end - start;
                float distance = direction.magnitude;

                lineRect.sizeDelta = new Vector2(distance, connectionThickness);
                lineRect.anchoredPosition = start + direction * 0.5f;
                lineRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
        }
    }

    private void ConfigureButtonVisuals(Button buttonInstance, MapNodeData node)
    {
        if (buttonInstance == null || node == null)
            return;

        Image frameImage = buttonInstance.targetGraphic as Image;
        if (frameImage != null)
            frameImage.color = defaultColor;

        Image[] images = buttonInstance.GetComponentsInChildren<Image>(true);
        Image iconImage = null;

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == frameImage)
                continue;

            iconImage = images[i];
            break;
        }

        if (iconImage == null)
            return;

        iconImage.sprite = node.IconOverride != null ? node.IconOverride : GetIcon(node.NodeType);
        iconImage.preserveAspect = true;
        iconImage.enabled = iconImage.sprite != null;
    }

    private Vector2 GetConnectionAnchorPosition(MapNodeData node, RectTransform rect)
    {
        if (rect == null)
            return Vector2.zero;

        Vector2 anchor = rect.anchoredPosition;
        if (connectionAnchorJitter <= 0f || node == null)
            return anchor;

        anchor.y += GetStableNodeJitter(node.NodeId) * connectionAnchorJitter;
        return anchor;
    }

    private float GetStableNodeJitter(string seed)
    {
        if (string.IsNullOrEmpty(seed))
            return 0f;

        uint hash = 2166136261u;
        for (int i = 0; i < seed.Length; i++)
        {
            hash ^= seed[i];
            hash *= 16777619u;
        }

        return (hash / (float)uint.MaxValue) * 2f - 1f;
    }

    private void ShiftAllNodes(float shiftY)
    {
        foreach (KeyValuePair<MapNodeData, RectTransform> pair in nodeRects)
        {
            if (pair.Value == null)
                continue;

            pair.Value.anchoredPosition += new Vector2(0f, shiftY);
        }
    }
}
