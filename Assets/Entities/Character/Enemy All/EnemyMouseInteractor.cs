using System.Collections.Generic;
using UnityEngine;

public class EnemyMouseInteractor : MonoBehaviour
{
    private Enemy enemy;
    private bool isMouseOver;
    private bool isTileMouseOver;
    private readonly List<BoardTile> hoverRangeTiles = new List<BoardTile>();

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
        isTileMouseOver = false;
        RefreshHoverIndicator();
    }

    public void HandleMouseDown()
    {
        if (PointerUiBlocker.IsPointerBlockedByUi())
            return;

        BattleManager manager = ResolveBattleManager();
        if (manager != null && enemy != null)
        {
            manager.OnEnemyClicked(enemy);
        }
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

    public void SetTileMouseOver(bool value)
    {
        if (isTileMouseOver == value)
        {
            return;
        }

        isTileMouseOver = value;
        RefreshHoverIndicator();
    }

    public void RefreshHoverIndicator()
    {
        bool shouldShow = (isMouseOver || isTileMouseOver) &&
            !CardDragHandler.IsAnyCardDragging &&
            !PointerUiBlocker.IsPointerBlockedByUi();

        if (!shouldShow)
        {
            HideHoverEffects();
            return;
        }

        ShowHoverEffects();
    }

    public bool ShouldRefreshHover()
    {
        bool hasActiveEffect = hoverRangeTiles.Count > 0;
        return isMouseOver || isTileMouseOver || hasActiveEffect || (CardDragHandler.IsAnyCardDragging && hasActiveEffect);
    }

    private void ShowHoverEffects()
    {
        HighlightAttackRange();
    }

    private void HideHoverEffects()
    {
        ClearAttackRangeHighlights();
    }

    private void HighlightAttackRange()
    {
        ClearAttackRangeHighlights();
        if (enemy == null)
        {
            return;
        }

        Board board = ResolveBoard();
        if (board == null)
        {
            return;
        }

        List<Vector2Int> offsets = enemy.Movement != null ? enemy.Movement.AttackRangeOffsets : enemy.attackRangeOffsets;
        for (int i = 0; i < offsets.Count; i++)
        {
            BoardTile tile = board.GetTileAt(enemy.gridPosition + offsets[i]);
            if (tile == null)
            {
                continue;
            }

            tile.SetAttackHighlight(true);
            hoverRangeTiles.Add(tile);
        }
    }

    private void ClearAttackRangeHighlights()
    {
        for (int i = 0; i < hoverRangeTiles.Count; i++)
        {
            BoardTile tile = hoverRangeTiles[i];
            if (tile == null)
            {
                continue;
            }

            tile.SetAttackHighlight(false);
        }

        hoverRangeTiles.Clear();
    }

    private static BattleManager ResolveBattleManager()
    {
        BattleRuntimeContext context = BattleRuntimeContext.Active;
        return context != null ? context.Manager : null;
    }

    private static Board ResolveBoard()
    {
        BattleRuntimeContext context = BattleRuntimeContext.Active;
        return context != null ? context.Board : null;
    }
}
