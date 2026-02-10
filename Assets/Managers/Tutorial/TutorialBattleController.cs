using System.Collections.Generic;
using UnityEngine;

public class TutorialBattleController : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private TutorialBattleDefinition tutorialDefinition;

    [Header("Dialogue")]
    [SerializeField] private GuideNPCPresenter guidePresenter;

    private BattleManager battleManager;
    private Board board;
    private int currentStepIndex;
    private bool hasCompletedTutorial;
    private bool stepCompleted;
    private bool runtimeEnabled;// 執行時開關：同一場景中動態決定是否啟用教學流程

    public bool IsActive =>
    runtimeEnabled &&
    !hasCompletedTutorial &&
    tutorialDefinition != null &&
    tutorialDefinition.HasSteps;
    // 只有「執行時已啟用 + 定義存在 + 尚未完成」才會覆寫一般戰鬥流程

    private void Awake()
    {
        battleManager = FindObjectOfType<BattleManager>();
        if (battleManager != null)
        {
            board = battleManager.board;
        }
        runtimeEnabled = tutorialDefinition != null;
        // 相容舊配置：若在 Inspector 先指定了 definition，預設允許啟用
    }

    public void ConfigureForBattle(bool enabled, TutorialBattleDefinition definition)
    {
        runtimeEnabled = enabled;
        tutorialDefinition = definition;
        currentStepIndex = 0;
        hasCompletedTutorial = false;
        stepCompleted = false;
        // 每次進入新戰鬥都重置步驟狀態，避免沿用上一場教學進度
    }

    private void OnEnable()
    {
        GameEvents.CardPlayedWithContext += HandleCardPlayed;
    }

    private void OnDisable()
    {
        GameEvents.CardPlayedWithContext -= HandleCardPlayed;
    }

    public bool TryGetPlayerStartPosition(out Vector2Int position)
    {
        position = default;
        if (!IsActive)
            return false;

        return tutorialDefinition.TryGetPlayerStartPosition(out position);
    }

    public bool TryGetEnemySpawns(out List<TutorialEnemySpawn> spawns)
    {
        spawns = null;
        if (!IsActive)
            return false;

        TutorialBattleStep step = GetCurrentStep();
        if (step == null || step.enemySpawns == null || step.enemySpawns.Count == 0)
            return false;

        spawns = step.enemySpawns;
        return true;
    }

    public bool TryApplyFixedHand(Player player, BattleHandUIController handUIController, bool playDrawAnimation)
    {
        if (!IsActive || player == null || handUIController == null)
            return false;

        TutorialBattleStep step = GetCurrentStep();
        if (step == null || step.fixedHand == null || step.fixedHand.Count == 0)
            return false;

        player.Hand.Clear();
        foreach (CardBase card in step.fixedHand)
        {
            if (card == null) continue;
            player.Hand.Add(card);
        }

        handUIController.RefreshHandUI(playDrawAnimation);
        return true;
    }

    public void ApplyTurnStartOverrides(BattleHandUIController handUIController, BattleManager manager)
    {
        if (!IsActive || handUIController == null || manager == null)
            return;

        TutorialBattleStep step = GetCurrentStep();
        if (step == null)
            return;

        if (step.lockEndTurnUntilComplete)
        {
            manager.SetEndTurnButtonInteractable(false);
        }
    }

    public void SpawnEnemiesForCurrentStep()
    {
        if (!IsActive || board == null)
            return;

        TutorialBattleStep step = GetCurrentStep();
        if (step == null)
            return;

        ClearExistingEnemies();

        foreach (TutorialEnemySpawn spawn in step.enemySpawns)
        {
            if (spawn == null || spawn.enemyPrefab == null)
                continue;

            BoardTile tile = board.GetTileAt(spawn.gridPosition);
            Vector3 spawnPosition = tile != null ? tile.transform.position : new Vector3(spawn.gridPosition.x, spawn.gridPosition.y, 0f);

            Enemy enemy = Instantiate(spawn.enemyPrefab, spawnPosition, Quaternion.identity);
            enemy.gridPosition = spawn.gridPosition;
        }

        battleManager?.RefreshEnemiesFromScene();
    }

    public void HandleBattleStarted()
    {
        if (!IsActive)
            return;

        stepCompleted = false;
    }

    private void HandleCardPlayed(CardPlayContext context)
    {
        if (!IsActive || stepCompleted)
            return;

        TutorialBattleStep step = GetCurrentStep();
        if (step == null || !step.requireElementReaction)
            return;

        if (context == null || !context.TryGetElementType(out ElementType attackElement))
            return;

        if (!DidTriggerRequiredReaction(step, context.TargetElementsBefore, attackElement))
            return;

        CompleteStep(step);
    }

    private static bool DidTriggerRequiredReaction(
        TutorialBattleStep step,
        IReadOnlyList<ElementType> targetElementsBefore,
        ElementType attackElement)
    {
        if (step == null || targetElementsBefore == null)
            return false;

        // 教學步驟只要求玩家觸發指定元素反應，不限制哪個元素必須先附著。
        if (attackElement == step.requiredAttackElement)
        {
            return ContainsElement(targetElementsBefore, step.requiredTargetElement);
        }

        if (attackElement == step.requiredTargetElement)
        {
            return ContainsElement(targetElementsBefore, step.requiredAttackElement);
        }

        return false;
    }

    private static bool ContainsElement(IReadOnlyList<ElementType> elements, ElementType target)
    {
        if (elements == null)
            return false;

        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i] == target)
                return true;
        }

        return false;
    }

    private void CompleteStep(TutorialBattleStep step)
    {
        stepCompleted = true;

        if (guidePresenter != null && !string.IsNullOrWhiteSpace(step.reactionDialogueKey))
        {
            guidePresenter.Talk(step.reactionDialogueKey);
        }

        AdvanceStep();
    }

    private void AdvanceStep()
    {
        currentStepIndex++;
        stepCompleted = false;

        if (tutorialDefinition == null || currentStepIndex >= tutorialDefinition.StepCount)
        {
            hasCompletedTutorial = true;
            if (battleManager != null)
            {
                battleManager.SetEndTurnButtonInteractable(true);
            }
            return;
        }

        SpawnEnemiesForCurrentStep();

        if (battleManager != null)
        {
            battleManager.DiscardAllHand();
            battleManager.StateMachine.ChangeState(new PlayerTurnState(battleManager));
        }
    }

    private TutorialBattleStep GetCurrentStep()
    {
        return tutorialDefinition != null ? tutorialDefinition.GetStep(currentStepIndex) : null;
    }

    private void ClearExistingEnemies()
    {
        Enemy[] existingEnemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in existingEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy.gameObject);
            }
        }

        battleManager?.enemies?.Clear();
    }
}