using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleTurnController
{
    private readonly BattleManager battleManager;
    private readonly Player player;
    private readonly List<Enemy> enemies;
    private readonly BattleStateMachine stateMachine;
    private readonly BattleHandUIController handUIController;
    private readonly TutorialBattleController tutorialController;
    private bool processingPlayerTurnStart = false;
    private bool processingEnemyTurnStart = false;

    public bool IsProcessingPlayerTurnStart => processingPlayerTurnStart;
    public bool IsProcessingEnemyTurnStart => processingEnemyTurnStart;

    public BattleTurnController(
        BattleManager battleManager,
        Player player,
        List<Enemy> enemies,
        BattleStateMachine stateMachine,
        BattleHandUIController handUIController,
        TutorialBattleController tutorialController)
    {
        this.battleManager = battleManager;
        this.player = player;
        this.enemies = enemies;
        this.stateMachine = stateMachine;
        this.handUIController = handUIController;
        this.tutorialController = tutorialController;
    }

    public void EndPlayerTurn()
    {
        if (!battleManager.BattleStarted || !(stateMachine.Current is PlayerTurnState))
            return;

        handUIController.SetEndTurnButtonInteractable(false);
        battleManager.DiscardAllHand();

        player.EndTurn();
        player.buffs.TickDebuffsOnPlayerTurnEnd(player);

        ApplyPlayerMiasmaDamage();
        ApplyEnemyEndTurnEffects();
        GameEvents.RaiseTurnEnded();
        ApplyGrowthTrapDamage();
        RefreshEnemyIntents();

        if (IsBattleResolutionPending())
            return;

        stateMachine.ChangeState(new EnemyTurnState(battleManager));
    }

    private void ApplyGrowthTrapDamage()
    {
        if (battleManager.board == null)
        {
            return;
        }

        var enemiesSnapshot = new List<Enemy>(enemies);
        foreach (var enemy in enemiesSnapshot)
        {
            if (enemy == null)
            {
                continue;
            }

            var tile = battleManager.board.GetTileAt(enemy.gridPosition);
            tile?.TriggerGrowthTrap(enemy, player);
        }
    }

    private void ApplyPlayerMiasmaDamage()
    {
        if (battleManager.board == null)
        {
            return;
        }

        var tile = battleManager.board.GetTileAt(player.position);
        if (tile == null)
        {
            return;
        }

        int damage = tile.MiasmaDamage;
        if (damage > 0)
        {
            player.TakeDamage(damage);
        }
    }

    private void ApplyEnemyEndTurnEffects()
    {
        var enemiesSnapshot = new List<Enemy>(enemies);
        foreach (var enemy in enemiesSnapshot)
        {
            if (enemy != null)
            {
                enemy.ProcessPlayerTurnEnd();
            }
        }
    }

    public void StartPlayerTurn()
    {
        battleManager.StartCoroutine(StartPlayerTurnRoutine());
    }

    private IEnumerator StartPlayerTurnRoutine()
    {
        if (IsBattleResolutionPending())
            yield break;

        handUIController.LockCardInteraction();
        handUIController.SetEndTurnButtonInteractable(false);

        bool showCentralPlayerTurnHint = tutorialController == null || tutorialController.ConsumePlayerTurnPhaseHintAllowance();
        yield return battleManager.ShowBattlePhaseHintAndWait("玩家回合", -1f, showCentralPlayerTurnHint);

        if (IsBattleResolutionPending())
            yield break;

        if (battleManager.BattleStarted)
        {
            handUIController.SetEndTurnButtonInteractable(true);
        }

        player.energy = player.maxEnergy;
        EnergyUIBus.RefreshAll(player.energy, player.maxEnergy);
        UIEventBus.RaiseEnergyState(new EnergySnapshot(player.energy, player.maxEnergy));
        handUIController.UpdateEnergyUI();

        int turnStartBlockGain = player.buffs.blockGainAtTurnStart;
        if (turnStartBlockGain > 0)
        {
            player.AddBlock(turnStartBlockGain);
        }

        player.NotifyTurnStarted();

        processingPlayerTurnStart = true;

        var enemiesAtTurnStart = new List<Enemy>(enemies);
        foreach (var e in enemiesAtTurnStart)
        {
            if (e != null)
            {
                e.ProcessTurnStart();
            }
        }

        processingPlayerTurnStart = false;

        if (IsBattleResolutionPending())
            yield break;

        bool appliedTutorialHand = false;
        if (tutorialController != null && tutorialController.IsActive)
        {
            appliedTutorialHand = tutorialController.TryApplyFixedHand(player, handUIController, true);
        }

        if (!appliedTutorialHand)
        {
            int drawCount = player.baseHandCardCount + player.buffs.nextTurnDrawChange;
            drawCount = Mathf.Max(0, drawCount);

            player.buffs.nextTurnDrawChange = 0;
            player.DrawNewHand(drawCount);
            battleManager.EnsureMovementCardInHand();
            handUIController.RefreshHandUI(true);
        }
        else
        {
            player.buffs.nextTurnDrawChange = 0;
        }

        handUIController.ApplyInteractableToAllCards(false);
        battleManager.StartCoroutine(handUIController.EnableCardsAfterDelay());
        tutorialController?.ApplyTurnStartOverrides(handUIController, battleManager);
        RefreshEnemyIntents();
    }

    private void RefreshEnemyIntents()
    {
        foreach (var e in enemies)
        {
            if (e != null)
            {
                e.DecideNextIntent(player);
            }
        }
    }

    public IEnumerator EnemyTurnCoroutine()
    {
        if (IsBattleResolutionPending())
            yield break;
        yield return battleManager.ShowBattlePhaseHintAndWait("妖怪回合");

        battleManager.RuntimeContext?.SquadCoordinator?.ClearExecutionPlans();
        processingEnemyTurnStart = true;

        var enemiesAtEnemyTurnStart = new List<Enemy>(enemies);
        foreach (var e in enemiesAtEnemyTurnStart)
        {
            if (e != null)
            {
                e.ProcessTurnStart();
            }
        }

        processingEnemyTurnStart = false;

        if (IsBattleResolutionPending())
            yield break;

        yield return new WaitForSeconds(1f);

        if (IsBattleResolutionPending())
            yield break;

        var enemiesTakingActions = new List<Enemy>(enemies);
        for (int i = 0; i < enemiesTakingActions.Count; i++)
        {
            battleManager.RuntimeContext?.SquadCoordinator?.RebuildExecutionPlans(player, enemiesTakingActions, i);
            Enemy e = enemiesTakingActions[i];
            if (e != null)
            {
                yield return e.EnemyActionRoutine(player);
            }

            if (IsBattleResolutionPending())
                yield break;
        }

        yield return new WaitForSeconds(1f);

        if (IsBattleResolutionPending())
            yield break;

        if (!player.buffs.retainBlockNextTurn)
        {
            player.block = 0;
        }
        else
        {
            player.buffs.retainBlockNextTurn = false;
        }

        var enemiesAtTurnEnd = new List<Enemy>(enemies);
        foreach (var e in enemiesAtTurnEnd)
        {
            if (e != null && e.ShouldResetBlockEachTurn)
            {
                e.block = 0;
            }
        }

        foreach (var e in enemiesAtTurnEnd)
        {
            if (e != null)
            {
                e.ProcessEnemyTurnEnd();
            }
        }

        if (IsBattleResolutionPending())
            yield break;

        battleManager.RuntimeContext?.SquadCoordinator?.ClearExecutionPlans();

        if (IsBattleResolutionPending())
            yield break;

        stateMachine.ChangeState(new PlayerTurnState(battleManager));
        Debug.Log("Player Turn");
    }

    private bool IsBattleResolutionPending()
    {
        if (stateMachine.Current is VictoryState || stateMachine.Current is DefeatState)
        {
            return true;
        }

        if (player != null && player.currentHP <= 0)
        {
            return true;
        }

        bool hasLivingEnemy = false;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy != null && !enemy.IsDead && enemy.currentHP > 0)
            {
                hasLivingEnemy = true;
                break;
            }
        }

        return !hasLivingEnemy;
    }
}
