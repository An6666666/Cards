using System.Collections;
using UnityEngine;

public class EnemyMouseInteractor : MonoBehaviour
{
    private Enemy enemy;
    private bool isMouseOver = false;
    private Coroutine hoverIndicatorCoroutine;

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
        if (enemy.hoverIndicator2D == null)
            return;

        bool shouldShow = isMouseOver && !CardDragHandler.IsAnyCardDragging;

        if (!shouldShow)
        {
            StopHoverIndicatorCoroutine();

            if (enemy.hoverIndicator2D.activeSelf)
                enemy.hoverIndicator2D.SetActive(false);

            return;
        }

        if (enemy.hoverIndicatorDelaySeconds <= 0f)
        {
            if (!enemy.hoverIndicator2D.activeSelf)
                enemy.hoverIndicator2D.SetActive(true);
        }
        else
        {
            if (!enemy.hoverIndicator2D.activeSelf && hoverIndicatorCoroutine == null)
                StartHoverIndicatorCoroutine();
        }
    }

    public bool ShouldRefreshHover()
    {
        return isMouseOver ||
            (enemy.hoverIndicator2D != null && enemy.hoverIndicator2D.activeSelf) ||
            (hoverIndicatorCoroutine != null) ||
            (enemy.hoverIndicator2D != null && CardDragHandler.IsAnyCardDragging);
    }
    private IEnumerator ShowHoverIndicatorAfterDelay()
    {
        if (enemy.hoverIndicatorDelaySeconds > 0f)
            yield return new WaitForSeconds(enemy.hoverIndicatorDelaySeconds);

        hoverIndicatorCoroutine = null;

        if (isMouseOver && enemy.hoverIndicator2D != null && !enemy.hoverIndicator2D.activeSelf && !CardDragHandler.IsAnyCardDragging)
            enemy.hoverIndicator2D.SetActive(true);
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
}
