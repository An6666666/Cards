using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "TutorialBattleDefinition", menuName = "Tutorial/Battle Definition")]
public class TutorialBattleDefinition : ScriptableObject
{
    [Header("Player Start")]
    [SerializeField] private bool overridePlayerStartPosition = true;
    [SerializeField] private Vector2Int playerStartPosition = Vector2Int.zero;
    [Header("Opening Dialogue")]
    [SerializeField] private string openingDialogueKey;
    [Header("Enemy Flow")]
    [SerializeField] private bool refreshEnemiesOnStepAdvance = true;
    [SerializeField] private bool clearExistingEnemiesBeforeSpawn = true;

    [Header("Steps")]
    [SerializeField] private List<TutorialBattleStep> steps = new List<TutorialBattleStep>();

    public bool HasSteps => steps != null && steps.Count > 0;
    public int StepCount => steps?.Count ?? 0;
    public bool RefreshEnemiesOnStepAdvance => refreshEnemiesOnStepAdvance;
    public bool ClearExistingEnemiesBeforeSpawn => clearExistingEnemiesBeforeSpawn;

    public bool TryGetOpeningDialogueKey(out string dialogueKey)
    {
        dialogueKey = string.IsNullOrWhiteSpace(openingDialogueKey) ? null : openingDialogueKey.Trim();
        return !string.IsNullOrEmpty(dialogueKey);
    }
    public bool TryGetPlayerStartPosition(out Vector2Int position)
    {
        position = playerStartPosition;
        return overridePlayerStartPosition;
    }

    public TutorialBattleStep GetStep(int index)
    {
        if (steps == null || index < 0 || index >= steps.Count)
            return null;

        return steps[index];
    }
}

[System.Serializable]
public class TutorialBattleStep
{
    [Header("Step Info")]
    public string stepId;

    [Header("Fixed Hand")]
    public List<CardBase> fixedHand = new List<CardBase>();

    [Header("Enemy Spawns")]
    public bool clearExistingEnemiesBeforeSpawn = true;
    public List<TutorialEnemySpawn> enemySpawns = new List<TutorialEnemySpawn>();

    [Header("Progress Condition")]
    public bool requireElementReaction = true;
    public ElementType requiredAttackElement = ElementType.Fire;
    public ElementType requiredTargetElement = ElementType.Wood;

    [Header("Flow Control")]
    public bool lockEndTurnUntilComplete = true;
    public bool clearTileEffectsBeforeSpawn = true;
    public string reactionDialogueKey;
    [Header("Reaction Visual (Optional)")]
    public Sprite reactionImage;
    public VideoClip reactionVideo;
}

[System.Serializable]
public class TutorialEnemySpawn
{
    public Enemy enemyPrefab;
    public Vector2Int gridPosition;
}