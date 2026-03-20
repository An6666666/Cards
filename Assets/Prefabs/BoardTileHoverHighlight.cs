using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 偵測滑鼠停留並高亮格子
/// </summary>
public class BoardTileHoverHighlight : MonoBehaviour
{
    public float hoverDelay = 0.1f; // 滑鼠停留多久後顯示高亮

    private float hoverTimer = 0f;
    private BoardTile tile;
    private bool hovering = false;

    private void Awake()
    {
        tile = GetComponent<BoardTile>();
    }

    private void OnMouseEnter()
    {
        if (PointerUiBlocker.IsPointerBlockedByUi())
            return;

        hovering = true;
        hoverTimer = 0f;
    }

    private void OnMouseExit()
    {
        hovering = false;
        hoverTimer = 0f;
        if (tile) tile.SetHighlight(false);
    }

    private void Update()
    {
        if (!hovering) return;

        if (PointerUiBlocker.IsPointerBlockedByUi())
        {
            hovering = false;
            hoverTimer = 0f;
            if (tile) tile.SetHighlight(false);
            return;
        }

        hoverTimer += Time.deltaTime;
        if (hoverTimer >= hoverDelay)
        {
            if (tile) tile.SetHighlight(true);
        }
    }
}
