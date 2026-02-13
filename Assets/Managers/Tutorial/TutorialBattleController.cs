using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialBattleController : MonoBehaviour
{
    private static readonly Dictionary<string, string[]> LocalDialogueByKey = new Dictionary<string, string[]>
    {
        { "FR", new[] { "我會教導你所有的元素反應，開始吧", "這是急急如律令火和急急如律令木，試試吧，拖拽到妖怪身上使用" } },
        { "Burn", new[] { "滑鼠移至受到攻擊的妖怪上，可以看到妖怪的狀態", "火+木會產生燃燒的效果，會殘留火元素，燃燒持續5回合，每次造成2點傷害", "妖怪在燃燒狀態下，受到水元素或冰元素傷害，會因此被消除，試試吧" } },
        { "Burntwo", new[] { "滑鼠移至受到攻擊的妖怪上看看，燃燒已經被移除了，妖怪身上則會只保留水元素", "做的好，接下來是蒸發反應", "這是急急如律令火和急急如律令水，試試吧，拖拽到妖怪身上使用" } },
        { "Evaporation", new[] { "火+水的蒸發反應，會讓後者使用的傷害增加1.5倍", "並留下後者攻擊的元素", "接下來是超載反應", "這是急急如律令雷、急急如律令火" } },
        { "Thunderclap", new[] { "超載會對被攻擊的妖怪周圍的敵人，也造成0.5倍的傷害", "被攻擊的妖怪會留下後者的元素", "接下來是生長反應" } },
        { "Growth", new[] { "生長反應會在棋盤上生長出荊棘", "荊棘會在玩家結束回合時，攻擊在荊棘格上的妖怪造成3點傷害", "接下來是導電反應", "先使用急急如律令水，在使用急急如律令雷" } },
        { "Conductive", new[] { "先使用水元素的特性讓鋪設好環境，再用雷元素造成導電的反應","接下來是冰凍反應", "這是急急如律令冰和急急如律令水" } },
        { "Freeze", new[] { "冰+水的冰凍反應，可以冰凍妖怪", "讓妖怪無法移動和攻擊", "接下來是結霜反應", "這是急急如律令冰和急急如律令木" } },
        { "Frost", new[] { "冰+木的結霜反應，會讓接下來造成的傷害額外增加2點傷害", "最多可以疊加6層，每玩家回合開始時會減少1層","接下來是雷擊反應，這是急急如律令雷和急急如律令木" } }, 
        { "Lightning strike", new[] { "雷+木的雷擊反應，會讓接下來造成的傷害變成2倍","接下來是融化反應，這是急急如律令火和急急如律令冰" } }, 
        { "Melt", new[] { "火+冰的融化反應，會讓後者使用的傷害增加1.5倍","被攻擊的妖怪會留下後者的元素" , "接下來是超導反應", "這是急急如律令雷和急急如律令冰" } },
        { "Superconduct", new[] { "雷+冰的超導反應，會讓後攻擊的傷害增加6點", "這是教學的最後一個元素反應了，接下來就靠你自己去探索了！","切記，攻擊一個目標時，他會優先和前一個使用的元素進行反應"} },
        { "Miss", new[] { "使用的順序錯了，再使用一次，先使用急急如律令水，在使用急急如律令雷" } },
        { "Mis", new[] { "急急如律令雷和急急如律令火需要攻擊同一個妖怪，才能正確的觸發元素反應" } },
    };
    private static readonly string[] OpeningDialogueFallbackLines =
    {
        "我會教導你所有的元素反應，開始吧。",
        "這是急急如律令火和急急如律令木，試試吧，拖拽到妖怪身上使用。"
    };
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
    private bool hasTriggeredTutorialExit;
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
        // runtimeEnabled 只由 ConfigureForBattle 決定，避免 Awake 執行順序覆蓋 BattleManager 的配置結果。
        // 需求：一般戰鬥隱藏 GuideNPC，僅教學戰鬥顯示。
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
        hasTriggeredTutorialExit = false;
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

        IReadOnlyList<string> openingDialogueKeys = tutorialDefinition.GetOpeningDialogueKeys();
        if (openingDialogueKeys != null)
        {
            for (int i = 0; i < openingDialogueKeys.Count; i++)
            {
                string dialogueKey = openingDialogueKeys[i];
                if (string.IsNullOrWhiteSpace(dialogueKey))
                    continue;

                if (TryTalkLocalByKey(dialogueKey))
                {
                    waitingForOpeningDialogue = true;
                    return;
                }
            }
        }

        if (tutorialDefinition.TryGetOpeningDialogueKey(out string fallbackKey))
        {
            waitingForOpeningDialogue = TryTalkLocalByKey(fallbackKey);
            if (waitingForOpeningDialogue)
                return;
        }
        guidePresenter.TalkLines(OpeningDialogueFallbackLines);
        waitingForOpeningDialogue = true;
    }
    private bool TryTalkLocalByKey(string key)
    {
        if (guidePresenter == null || string.IsNullOrWhiteSpace(key))
            return false;

        string trimmedKey = key.Trim();
        if (!LocalDialogueByKey.TryGetValue(trimmedKey, out string[] lines) || lines == null || lines.Length == 0)
            return false;

        guidePresenter.TalkLines(lines);
        return true;
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
            TryTalkLocalByKey(step.incorrectOrderDialogueKey);
        }
        if (tutorialDefinition != null && tutorialDefinition.RefreshEnemiesOnIncorrectOrder)
        {
            RespawnEnemiesForCurrentStep();
        }
        if (step.redealHandOnIncorrectOrder)
        {
            TryApplyFixedHand(battleManager != null ? battleManager.player : null, null, true);
        }
    }
    private void RespawnEnemiesForCurrentStep()
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
                playedReactionDialogue = TryTalkLocalByKey(step.reactionDialogueKey);
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
            TryFinishTutorialBattleAndReturnToMap();
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
    private void TryFinishTutorialBattleAndReturnToMap()
    {
        if (hasTriggeredTutorialExit)
            return;

        hasTriggeredTutorialExit = true;

        RunManager runManager = RunManager.Instance;
        if (runManager == null)
            return;

        runManager.HandleBattleVictory();
        runManager.ReturnToRunSceneFromBattle();
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