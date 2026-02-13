using System.Collections;
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
    private bool waitingForOpeningDialogue;
    private bool waitingForReactionDialogue;
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
        UpdateGuideNpcVisibility();
    }

    public void ConfigureForBattle(bool enabled, TutorialBattleDefinition definition)
    {
        runtimeEnabled = enabled;
        tutorialDefinition = definition;
        currentStepIndex = 0;
        hasCompletedTutorial = false;
        stepCompleted = false;
        waitingForOpeningDialogue = false;
        waitingForReactionDialogue = false;
        // 每次進入新戰鬥都重置步驟狀態，避免沿用上一場教學進度
        UpdateGuideNpcVisibility();
    }

    private void UpdateGuideNpcVisibility()
    {
        if (guidePresenter == null)
            return;

        bool shouldShowGuideNpc = runtimeEnabled && tutorialDefinition != null && tutorialDefinition.HasSteps;
        if (shouldShowGuideNpc)
        {
            guidePresenter.Show();
            return;
        }

        guidePresenter.Hide();
    }

    private void OnEnable()
    {
        GameEvents.CardPlayedWithContext += HandleCardPlayed;
        if (guidePresenter != null)
        {
            guidePresenter.DialogueLinesFinished += HandleGuideDialogueFinished;
        }
    }

    private void OnDisable()
    {
        GameEvents.CardPlayedWithContext -= HandleCardPlayed;
        if (guidePresenter != null)
        {
            guidePresenter.DialogueLinesFinished -= HandleGuideDialogueFinished;
        }
    }

    public bool IsWaitingForOpeningDialogue => waitingForOpeningDialogue;

    private void HandleGuideDialogueFinished()
    {
        waitingForOpeningDialogue = false;
        if (!waitingForReactionDialogue)
            return;

        waitingForReactionDialogue = false;
        AdvanceStep();
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
        if (!IsActive || player == null)
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

        if (handUIController != null)
        {
            handUIController.RefreshHandUI(playDrawAnimation);
        }
        else
        {
            battleManager?.RefreshHandUI(playDrawAnimation);
        }

        return true;
    }

    public bool ShouldLockEndTurnForCurrentStep()
    {
        if (!IsActive)
            return false;

        TutorialBattleStep step = GetCurrentStep();
        return step != null && step.lockEndTurnUntilComplete;
    }

    public void ApplyTurnStartOverrides(BattleHandUIController handUIController, BattleManager manager)
    {
        if (!IsActive || handUIController == null || manager == null)
            return;

        if (ShouldLockEndTurnForCurrentStep())
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

        bool hasAnySpawn = false;
        foreach (TutorialEnemySpawn spawn in step.enemySpawns)
        {
            if (spawn != null && spawn.enemyPrefab != null)
            {
                hasAnySpawn = true;
                break;
            }
        }

        if (hasAnySpawn && ShouldClearEnemiesBeforeSpawn(step))
        {
            ClearExistingEnemies();
        }
        if (step.clearTileEffectsBeforeSpawn)
        {
            board.ClearAllTileEffects();
        }

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
    private bool ShouldClearEnemiesBeforeSpawn(TutorialBattleStep step)
    {
        if (step == null)
            return false;

        return step.clearExistingEnemiesBeforeSpawn;
    }
    public void HandleBattleStarted()
    {
        if (!IsActive)
            return;

        stepCompleted = false;
        TryPlayOpeningDialogue();
    }

    private void TryPlayOpeningDialogue()
    {
        waitingForOpeningDialogue = false;
        waitingForReactionDialogue = false;
        if (guidePresenter == null || tutorialDefinition == null)
            return;

        if (tutorialDefinition.TryGetOpeningDialogueKey(out string dialogueKey))
        {
            waitingForOpeningDialogue = guidePresenter.Talk(dialogueKey);
        }
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

        bool requireOrderedApplication = step.requireOrderedElementApplication;
        if (!DidTriggerRequiredReaction(step, context.TargetElementsBefore, attackElement, requireOrderedApplication, out bool incorrectOrder))
        {
            if (incorrectOrder)
            {
                HandleIncorrectOrder(step);
            }
            else
            {
                StartCoroutine(CheckFailureAfterCardResolved(step));
            }
            return;
        }

        CompleteStep(step);
    }
    private IEnumerator CheckFailureAfterCardResolved(TutorialBattleStep step)
    {
        yield return null;

        if (!IsActive || stepCompleted || step == null)
            yield break;

        if (GetCurrentStep() != step)
            yield break;

        if (battleManager == null || battleManager.player == null || battleManager.player.Hand == null)
            yield break;

        if (battleManager.player.Hand.Count > 0)
            yield break;

        HandleIncorrectOrder(step);
    }
    private static bool DidTriggerRequiredReaction(
        TutorialBattleStep step,
        IReadOnlyList<ElementType> targetElementsBefore,
        ElementType attackElement,
        bool requireOrderedApplication,
        out bool incorrectOrder)
    {
        incorrectOrder = false;
        if (step == null || targetElementsBefore == null)
            return false;

        bool isRequiredAttack = attackElement == step.requiredAttackElement;
        bool isRequiredTarget = attackElement == step.requiredTargetElement;
        bool hasRequiredAttack = ContainsElement(targetElementsBefore, step.requiredAttackElement);
        bool hasRequiredTarget = ContainsElement(targetElementsBefore, step.requiredTargetElement);

        if (isRequiredAttack && hasRequiredTarget)
        {
            return true;
        }

        if (isRequiredTarget && hasRequiredAttack)
        {
            incorrectOrder = requireOrderedApplication;
            return !requireOrderedApplication;
        }

        return false;
    }

    private void HandleIncorrectOrder(TutorialBattleStep step)
    {
        if (!IsActive || step == null)
            return;

        if (guidePresenter != null && !string.IsNullOrWhiteSpace(step.incorrectOrderDialogueKey))
        {
            guidePresenter.Talk(step.incorrectOrderDialogueKey.Trim());
        }

        if (step.redealHandOnIncorrectOrder)
        {
            TryApplyFixedHand(battleManager != null ? battleManager.player : null, null, true);
        }
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
        waitingForReactionDialogue = false;
        bool playedReactionDialogue = false;
        if (guidePresenter != null)
        {
            if (!string.IsNullOrWhiteSpace(step.reactionDialogueKey))
            {
                playedReactionDialogue = guidePresenter.Talk(step.reactionDialogueKey);
            }

            guidePresenter.ShowReactionVisual(step.reactionImage, step.reactionVideo);
        }
        if (playedReactionDialogue)
        {
            waitingForReactionDialogue = true;
            return;
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

         if (tutorialDefinition != null && tutorialDefinition.RefreshEnemiesOnStepAdvance)
        {
            SpawnEnemiesForCurrentStep();
        }

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