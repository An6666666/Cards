using UnityEngine;

public class EnemyMouseInteractor : MonoBehaviour
{
    private Enemy enemy;
    private bool isMouseOver = false;

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
        if (enemy.hoverIndicator2D != null)
        {
            enemy.hoverIndicator2D.SetActive(isMouseOver && !CardDragHandler.IsAnyCardDragging);
        }
    }

    public bool ShouldRefreshHover()
    {
        return isMouseOver ||
            (enemy.hoverIndicator2D != null && enemy.hoverIndicator2D.activeSelf) ||
            (enemy.hoverIndicator2D != null && CardDragHandler.IsAnyCardDragging);
    }
}
