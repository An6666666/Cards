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
    private StatusPanel_Text statusPanel;
    private GameObject currentStatusTarget;
    private EnemyMouseInteractor currentEnemyHoverTarget;
    private bool hovering = false;

    private void Awake()
    {
        tile = GetComponent<BoardTile>();
        statusPanel = FindObjectOfType<StatusPanel_Text>(true);
    }

    private void OnMouseEnter()
    {
        if (PointerUiBlocker.IsPointerBlockedByUi())
            return;

        hovering = true;
        hoverTimer = 0f;
        UpdateStatusPanelTarget();
    }

    private void OnMouseExit()
    {
        hovering = false;
        hoverTimer = 0f;
        if (tile) tile.SetHighlight(false);
        ClearStatusPanelTarget();
    }

    private void Update()
    {
        if (!hovering) return;

        if (PointerUiBlocker.IsPointerBlockedByUi())
        {
            hovering = false;
            hoverTimer = 0f;
            if (tile) tile.SetHighlight(false);
            ClearStatusPanelTarget();
            return;
        }

        bool hasStatusTarget = UpdateStatusPanelTarget();

        hoverTimer += Time.deltaTime;
        if (hoverTimer >= hoverDelay && ShouldShowEmptyTileHoverHighlight())
        {
            if (tile) tile.SetHighlight(!hasStatusTarget);
        }
    }

    private void OnDisable()
    {
        if (tile) tile.SetHighlight(false);
        ClearStatusPanelTarget();
    }

    private bool UpdateStatusPanelTarget()
    {
        if (statusPanel == null)
        {
            statusPanel = FindObjectOfType<StatusPanel_Text>(true);
        }

        GameObject target = ResolveOccupantObject();
        if (currentStatusTarget == target)
        {
            if (currentStatusTarget != null)
            {
                UpdateEnemyTileHover(currentStatusTarget.GetComponent<Enemy>());
            }

            return currentStatusTarget != null;
        }

        currentStatusTarget = target;
        if (currentStatusTarget != null)
        {
            if (statusPanel != null)
            {
                statusPanel.SetTarget(currentStatusTarget);
            }

            if (tile) tile.SetHighlight(false);
            UpdateEnemyTileHover(currentStatusTarget.GetComponent<Enemy>());
        }
        else
        {
            if (statusPanel != null)
            {
                statusPanel.ClearTarget();
            }

            UpdateEnemyTileHover(null);
        }

        return currentStatusTarget != null;
    }

    private void ClearStatusPanelTarget()
    {
        if (currentStatusTarget == null)
        {
            UpdateEnemyTileHover(null);
            return;
        }

        currentStatusTarget = null;
        UpdateEnemyTileHover(null);
        if (statusPanel != null)
        {
            statusPanel.ClearTarget();
        }
    }

    private GameObject ResolveOccupantObject()
    {
        if (tile == null)
        {
            return null;
        }

        BattleRuntimeContext context = BattleRuntimeContext.Active;
        IReadOnlyList<Enemy> enemies = context != null ? context.Enemies : null;
        if (enemies == null)
        {
            Enemy[] sceneEnemies = FindObjectsOfType<Enemy>();
            for (int i = 0; i < sceneEnemies.Length; i++)
            {
                Enemy enemy = sceneEnemies[i];
                if (IsAliveEnemyOnTile(enemy))
                {
                    return enemy.gameObject;
                }
            }
        }
        else
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (IsAliveEnemyOnTile(enemy))
                {
                    return enemy.gameObject;
                }
            }
        }

        Player player = context != null ? context.Player : FindObjectOfType<Player>();
        if (IsPlayerOnTile(player, context))
        {
            return player.gameObject;
        }

        return null;
    }

    private bool IsAliveEnemyOnTile(Enemy enemy)
    {
        return enemy != null
            && enemy.gridPosition == tile.gridPosition
            && enemy.currentHP > 0
            && !enemy.IsDead;
    }

    private bool IsPlayerOnTile(Player player, BattleRuntimeContext context)
    {
        if (player == null || tile == null)
        {
            return false;
        }

        BattleManager manager = context != null ? context.Manager : null;
        if (manager == null || !manager.BattleStarted)
        {
            return false;
        }

        if (player.position == tile.gridPosition)
        {
            return true;
        }

        Board board = context != null ? context.Board : FindObjectOfType<Board>();
        if (board == null)
        {
            return false;
        }

        BoardTile logicalTile = board.GetTileAt(player.position);
        if (logicalTile == tile)
        {
            return true;
        }

        return false;
    }

    private bool ShouldShowEmptyTileHoverHighlight()
    {
        if (tile == null)
        {
            return false;
        }

        BattleRuntimeContext context = BattleRuntimeContext.Active;
        BattleManager manager = context != null ? context.Manager : null;
        if (manager != null && manager.BattleStarted)
        {
            return false;
        }

        return tile.GetComponent<BoardTileSelectable>() != null;
    }

    private void UpdateEnemyTileHover(Enemy enemy)
    {
        EnemyMouseInteractor next = null;
        if (enemy != null)
        {
            next = enemy.MouseInteractor != null
                ? enemy.MouseInteractor
                : enemy.GetComponent<EnemyMouseInteractor>();
        }

        if (currentEnemyHoverTarget == next)
        {
            return;
        }

        if (currentEnemyHoverTarget != null)
        {
            currentEnemyHoverTarget.SetTileMouseOver(false);
        }

        currentEnemyHoverTarget = next;
        if (currentEnemyHoverTarget != null)
        {
            currentEnemyHoverTarget.SetTileMouseOver(true);
        }
    }
}
