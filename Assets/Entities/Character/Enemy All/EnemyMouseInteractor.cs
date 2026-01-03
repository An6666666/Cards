using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMouseInteractor : MonoBehaviour
{
    private Enemy enemy;
    private bool isMouseOver = false;
    private Coroutine hoverIndicatorCoroutine;
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
        StopHoverIndicatorCoroutine();
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
        StartHoverIndicatorCoroutine();
        RefreshHoverIndicator();
    }

    public void HandleMouseExit()
    {
        isMouseOver = false;
        StopHoverIndicatorCoroutine();
        RefreshHoverIndicator();
    }

    public void RefreshHoverIndicator()
    {
        bool shouldShow = isMouseOver && !CardDragHandler.IsAnyCardDragging;

        if (!shouldShow)
        {
            StopHoverIndicatorCoroutine();

            HideHoverEffects();

            return;
        }

        if (enemy.hoverIndicatorDelaySeconds <= 0f)
        ShowHoverEffects();
        else if (!IsHoverEffectActive() && hoverIndicatorCoroutine == null)
        StartHoverIndicatorCoroutine();
    }

    public bool ShouldRefreshHover()
    {
        bool hasActiveEffect =
        (enemy.hoverIndicator2D != null && enemy.hoverIndicator2D.activeSelf) ||
        hoverRangeTiles.Count > 0;

        return isMouseOver ||
        hoverIndicatorCoroutine != null ||
        hasActiveEffect ||
        (CardDragHandler.IsAnyCardDragging && hasActiveEffect);
    }
    private IEnumerator ShowHoverIndicatorAfterDelay()
    {
        if (enemy.hoverIndicatorDelaySeconds > 0f)
            yield return new WaitForSeconds(enemy.hoverIndicatorDelaySeconds);

        hoverIndicatorCoroutine = null;

        if (isMouseOver && !CardDragHandler.IsAnyCardDragging)
        ShowHoverEffects();
    }

    private void StartHoverIndicatorCoroutine()
    {
        StopHoverIndicatorCoroutine();
        hoverIndicatorCoroutine = StartCoroutine(ShowHoverIndicatorAfterDelay());
    }

    private void StopHoverIndicatorCoroutine()
    {
        if (hoverIndicatorCoroutine != null)
        {
            StopCoroutine(hoverIndicatorCoroutine);
            hoverIndicatorCoroutine = null;
        }
    }

    private void ShowHoverEffects()
    {
        if (enemy.hoverIndicator2D != null && !enemy.hoverIndicator2D.activeSelf)
            enemy.hoverIndicator2D.SetActive(true);

        HighlightAttackRange();
    }

    private void HideHoverEffects()
    {
        if (enemy.hoverIndicator2D != null && enemy.hoverIndicator2D.activeSelf)
            enemy.hoverIndicator2D.SetActive(false);

        ClearAttackRangeHighlights();
    }

    private bool IsHoverEffectActive()
    {
        return (enemy.hoverIndicator2D != null && enemy.hoverIndicator2D.activeSelf) ||
            hoverRangeTiles.Count > 0;
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
