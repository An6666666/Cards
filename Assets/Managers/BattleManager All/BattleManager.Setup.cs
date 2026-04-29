using UnityEngine;

public partial class BattleManager
{
    private void ResolveTutorialController()
    {
        if (tutorialController != null)
        {
            return;
        }

        tutorialController = GetComponentInChildren<TutorialBattleController>(true);
        if (tutorialController == null)
        {
            tutorialController = FindObjectOfType<TutorialBattleController>();
        }
    }

    private void ConfigureTutorialForActiveEncounter()
    {
        ResolveTutorialController();

        if (tutorialController == null)
        {
            return;
        }

        RunEncounterDefinition encounter = RunManager.Instance?.ActiveNode?.Encounter;
        bool enableTutorial = encounter != null && encounter.UseTutorialBattle;
        TutorialBattleDefinition definition = enableTutorial ? encounter.TutorialBattleDefinition : null;

        tutorialController.ConfigureForBattle(enableTutorial, definition);
    }

    private void InitializeControllers()
    {
        runtimeContext = new BattleRuntimeContext(this, player, board, enemies);
        runtimeContext.Activate();
        enemyQueryService = new BattleEnemyQueryService(runtimeContext);

        handUIController = new BattleHandUIController(
            this,
            player,
            cardPrefab,
            handPanel,
            deckPile,
            discardPile,
            allDeckPile,
            energyText,
            endTurnButton,
            cardUseDelay);

        movementSelectionController = new MovementSelectionController(
            this,
            player,
            board,
            handUIController);

        attackSelectionController = new AttackSelectionController(
            player,
            board,
            handUIController,
            this,
            enemyQueryService);

        rewardController = new BattleRewardController(
            this,
            player,
            allCardPool,
            rewardUIPrefab,
            handPanel,
            normalBattleRelicRewardChance,
            eliteBattleRelicRewardChance,
            normalBattleRelicChoiceCount);

        encounterLoader = new BattleEncounterLoader(
            this,
            board,
            player,
            enemies,
            enemySpawnConfigs,
            stateMachine,
            tutorialController);

        movementSelectionController.SetEncounterLoader(encounterLoader);

        turnController = new BattleTurnController(
            this,
            player,
            enemies,
            stateMachine,
            handUIController,
            tutorialController);

        playerDeckController = player != null ? player.GetComponent<PlayerDeckController>() : null;
        if (playerDeckController != null)
        {
            playerDeckController.ConfigureBattleRuntime(this, runtimeContext);
            playerDeckController.HandChanged -= HandlePlayerHandChanged;
            playerDeckController.HandChanged += HandlePlayerHandChanged;
            playerDeckController.DeckChanged -= HandlePlayerDeckChanged;
            playerDeckController.DeckChanged += HandlePlayerDeckChanged;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeRunSnapshot();
        runtimeContext?.DeactivateIfOwner(this);

        if (player == null)
        {
            return;
        }

        if (playerDeckController == null)
        {
            playerDeckController = player.GetComponent<PlayerDeckController>();
        }

        if (playerDeckController != null)
        {
            playerDeckController.HandChanged -= HandlePlayerHandChanged;
            playerDeckController.DeckChanged -= HandlePlayerDeckChanged;
            playerDeckController.ClearBattleRuntime(this);
        }
    }

    private void HandlePlayerHandChanged()
    {
        handUIController?.RefreshHandUI();
    }

    private void HandlePlayerDeckChanged()
    {
        handUIController?.UpdateDeckDiscardUI();
    }
}
