using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Run/Encounter Definition", fileName = "RunEncounter")]
public class RunEncounterDefinition : ScriptableObject
{
    [SerializeField] private string encounterId;
    [SerializeField] private List<EnemySpawnConfig> enemyGroups = new List<EnemySpawnConfig>();
    [SerializeField] private bool useTutorialBattle;
    [SerializeField] private TutorialBattleDefinition tutorialBattleDefinition;

    [Header("Audio")]
    [SerializeField] private AudioClip bgmOverride;

    public string EncounterId => string.IsNullOrEmpty(encounterId) ? name : encounterId;
    public IReadOnlyList<EnemySpawnConfig> EnemyGroups => enemyGroups;
    public bool UseTutorialBattle => useTutorialBattle && tutorialBattleDefinition != null;
    public TutorialBattleDefinition TutorialBattleDefinition => tutorialBattleDefinition;
    public AudioClip BgmOverride => bgmOverride;
}
