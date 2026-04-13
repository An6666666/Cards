using System.Collections.Generic;
using UnityEngine;

public class MovementSelectionController
{
    private readonly BattleManager battleManager;
    private readonly Player player;
    private readonly Board board;
    private readonly BattleHandUIController handUIController;
    private BattleEncounterLoader encounterLoader;

    private bool isSelectingMovementTile;
    private CardBase currentMovementCard;
    private readonly List<BoardTile> highlightedTiles = new List<BoardTile>();

    public MovementSelectionController(
        BattleManager battleManager,
        Player player,
        Board board,
        BattleHandUIController handUIController,
        BattleEncounterLoader encounterLoader = null)
    {
        this.battleManager = battleManager;
        this.player = player;
        this.board = board;
        this.handUIController = handUIController;
        this.encounterLoader = encounterLoader;
    }

    public void SetEncounterLoader(BattleEncounterLoader loader)
    {
        encounterLoader = loader;
    }

    public void UseMovementCard(CardBase movementCard)
    {
        if (movementCard == null)
        {
            return;
        }

        if (!(battleManager.StateMachine.Current is PlayerTurnState))
        {
            return;
        }

        if (player == null)
        {
            Debug.LogWarning("Player reference not assigned.");
            return;
        }

        if (!player.buffs.CanMove())
        {
            Debug.Log("Cannot use movement: movement is currently restricted.");
            return;
        }

        int previewCost = movementCard.cost + player.GetCardCostModifier(movementCard);
        previewCost += player.buffs.movementCostModify;
        previewCost = Mathf.Max(0, previewCost);

        if (player.energy < previewCost)
        {
            Debug.Log("Not enough energy for movement");
            return;
        }

        if (isSelectingMovementTile)
        {
            Debug.Log("Already selecting movement tile");
            return;
        }

        isSelectingMovementTile = true;
        currentMovementCard = movementCard;

        MovementCardBase movementBase = movementCard as MovementCardBase;
        List<Vector2Int> offsets = (movementBase != null && movementBase.rangeOffsets?.Count > 0)
            ? movementBase.rangeOffsets
            : new List<Vector2Int>
            {
                new Vector2Int(0, 1),
                new Vector2Int(0, -1),
                new Vector2Int(-1, 0),
                new Vector2Int(1, 0)
            };

        highlightedTiles.Clear();
        HighlightTilesWithOffsets(player.position, offsets);
    }

    public void ResetAllTilesSelectable()
    {
        board.ResetAllTilesSelectable();
    }

    public void CancelMovementSelection()
    {
        isSelectingMovementTile = false;
        currentMovementCard = null;

        for (int i = 0; i < highlightedTiles.Count; i++)
        {
            highlightedTiles[i].SetSelectable(false);
        }

        highlightedTiles.Clear();
        board.ResetAllTilesSelectable();
    }

    public bool OnTileClicked(BoardTile tile)
    {
        if (encounterLoader != null && encounterLoader.HandleTileSelection(tile))
        {
            return true;
        }

        if (!(battleManager.StateMachine.Current is PlayerTurnState))
        {
            return false;
        }

        if (!isSelectingMovementTile)
        {
            return false;
        }

        if (!highlightedTiles.Contains(tile))
        {
            CancelMovementSelection();
            return false;
        }

        if (player == null || !player.buffs.CanMove())
        {
            Debug.Log("Cannot move: movement is currently restricted.");
            CancelMovementSelection();
            return false;
        }

        if (!player.CanMoveToPosition(tile.gridPosition, allowOccupiedTileRelic: true))
        {
            Debug.Log("Cannot move: tile occupied by enemy.");
            CancelMovementSelection();
            return false;
        }

        player.FaceTowards(tile.transform);
        PlayMovementCardAnimation(currentMovementCard);
        currentMovementCard.ExecuteOnPosition(player, tile.gridPosition);

        int finalCost = currentMovementCard.cost + player.GetCardCostModifier(currentMovementCard)
                        + player.buffs.movementCostModify;
        finalCost = Mathf.Max(0, finalCost);
        player.UseEnergy(finalCost);

        if (player.Hand.Contains(currentMovementCard))
        {
            player.Hand.Remove(currentMovementCard);

            if (!battleManager.IsGuaranteedMovementCard(currentMovementCard))
            {
                player.discardPile.Add(currentMovementCard);
            }
            else
            {
                battleManager.RemoveGuaranteedMovementCardFromPiles();
            }
        }

        isSelectingMovementTile = false;
        currentMovementCard = null;

        for (int i = 0; i < highlightedTiles.Count; i++)
        {
            highlightedTiles[i].SetSelectable(false);
        }

        highlightedTiles.Clear();
        board.ResetAllTilesSelectable();
        handUIController.UpdateHandMetaUI();
        return true;
    }

    public bool OnEnemyClicked(Enemy enemy)
    {
        if (enemy == null || board == null)
        {
            return false;
        }

        BoardTile occupiedTile = board.GetTileAt(enemy.gridPosition);
        if (occupiedTile == null)
        {
            return false;
        }

        return OnTileClicked(occupiedTile);
    }

    private void PlayMovementCardAnimation(CardBase movementCard)
    {
        if (player == null || movementCard == null)
        {
            return;
        }

        if (movementCard is Move_XingMangPoZhen)
        {
            player.PlayMoveStarAnim();
            player.PlayMoveStarFX();
            return;
        }

        if (movementCard is Move_YiDong || movementCard is Move_SuiFengBu)
        {
            player.PlayMoveCardAnim();
        }
    }

    private void HighlightTilesWithOffsets(Vector2Int centerPos, List<Vector2Int> offsets)
    {
        for (int i = 0; i < offsets.Count; i++)
        {
            Vector2Int tilePos = centerPos + offsets[i];
            BoardTile tile = board.GetTileAt(tilePos);
            if (tile == null)
            {
                continue;
            }

            if (!player.CanMoveToPosition(tilePos, allowOccupiedTileRelic: true))
            {
                continue;
            }

            tile.SetSelectable(true);
            highlightedTiles.Add(tile);
        }
    }
}
