using System.Collections.Generic;
using UnityEngine;

public class EnemyMouseInteractor : MonoBehaviour
{
    private Enemy enemy;
    private bool isMouseOver = false;
    private readonly List<BoardTile> hoverRangeTiles = new List<BoardTile>();
    private readonly Dictionary<BoardTile, bool> hoverRangePreviousStates = new Dictionary<BoardTile, bool>();

    public void Init(Enemy owner)
    {
        enemy = owner;
    }

    public void HandleOnEnable()
    {
        RefreshHoverIndicator();
    }

    public void HandleOnDisable()
    {
        isMouseOver = false;
        RefreshHoverIndicator();
    }

    public void HandleMouseDown()
    {
        BattleManager bm = FindObjectOfType<BattleManager>();
        bm.OnEnemyClicked(enemy);
    }

    public void HandleMouseEnter()
    {
        isMouseOver = true;
        RefreshHoverIndicator();
    }

    public void HandleMouseExit()
    {
        isMouseOver = false;
        RefreshHoverIndicator();
    }

    public void RefreshHoverIndicator()
    {
        bool shouldShow = isMouseOver && !CardDragHandler.IsAnyCardDragging;

        if (!shouldShow)
        {
            HideHoverEffects();

            return;
        }

        ShowHoverEffects();
    }

    public bool ShouldRefreshHover()
    {
        bool hasActiveEffect =
        hoverRangeTiles.Count > 0;

        return isMouseOver ||
        hasActiveEffect ||
        (CardDragHandler.IsAnyCardDragging && hasActiveEffect);
    }

    private void ShowHoverEffects()
    {
        HighlightAttackRange();
    }

    private void HideHoverEffects()
    {
        ClearAttackRangeHighlights();
    }

    private bool IsHoverEffectActive()
    {
        return hoverRangeTiles.Count > 0;
    }

    private void HighlightAttackRange()
    {
        ClearAttackRangeHighlights();

        if (enemy == null)
            return;

        Board board = FindObjectOfType<Board>();
        if (board == null)
            return;

        List<Vector2Int> offsets = enemy.Movement != null ? enemy.Movement.AttackRangeOffsets : enemy.attackRangeOffsets;
        foreach (Vector2Int offset in offsets)
        {
            BoardTile tile = board.GetTileAt(enemy.gridPosition + offset);
            if (tile == null)
                continue;

            bool wasActive = tile.IsAttackHighlightActive();
            hoverRangePreviousStates[tile] = wasActive;
            tile.SetAttackHighlight(true);
            hoverRangeTiles.Add(tile);
        }
    }

    private void ClearAttackRangeHighlights()
    {
        foreach (BoardTile tile in hoverRangeTiles)
        {
            if (tile == null)
                continue;

            if (hoverRangePreviousStates.TryGetValue(tile, out bool wasActive))
                tile.SetAttackHighlight(wasActive);
            else
                tile.SetAttackHighlight(false);
        }

        hoverRangeTiles.Clear();
        hoverRangePreviousStates.Clear();
    }
}
