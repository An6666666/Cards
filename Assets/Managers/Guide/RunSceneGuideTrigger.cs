using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-level guide trigger for RunScene: reacts to RunManager node events and plays dialogue once per key.
/// </summary>
public class RunSceneGuideTrigger : SceneGuideTriggerBase
{
    [System.Serializable]
    public class NodeDialogueKey
    {
        public MapNodeType nodeType;
        public string onEnterKey;
        public string onCompletedKey;
    }

    [Header("References")]
    [SerializeField] private GuideNPCPresenter npcPresenter;
    [SerializeField] private RunManager runManager;

    [Header("Dialogue Keys")]
    [SerializeField] private string runStartKey;
    [SerializeField] private List<NodeDialogueKey> nodeDialogueKeys = new List<NodeDialogueKey>();

    private bool isSubscribed;
    private bool hasPlayedSceneEntryDialogue;

    private void Awake()
    {
        TryBindRunManager();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryBindRunManager();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 場景切換回 RunScene 時，保險地重新綁定一次，避免 Inspector 殘留 Missing 參考。
        TryBindRunManager();
    }

    private void TryBindRunManager()
    {
        if (runManager == null)
        {
            runManager = RunManager.Instance;
        }

        if (runManager == null)
        {
            runManager = FindObjectOfType<RunManager>(true);
        }

        if (runManager != null && !isSubscribed)
        {
            runManager.NodeEntered += HandleNodeEntered;
            runManager.NodeCompleted += HandleNodeCompleted;
            isSubscribed = true;
        }
    }

    private void Start()
    {
        Debug.Log("[RunSceneGuideTrigger] Start()");
        TryPlaySceneEntryDialogue();
    }

    private void Update()
    {
        if (hasPlayedSceneEntryDialogue)
            return;

        TryPlaySceneEntryDialogue();
    }

    private void TryPlaySceneEntryDialogue()
    {
        if (hasPlayedSceneEntryDialogue)
            return;

        TryBindRunManager();
        if (runManager == null)
            return;

        hasPlayedSceneEntryDialogue = true;
        // 回到 RunScene（例如戰鬥結束）時，優先播放剛完成節點的完成台詞。
        // 只有在尚未進行任何節點時，才播放開場台詞。
        if (runManager.CurrentNode != null)
        {
            TryTalkForNodeCompleted(runManager.CurrentNode);
            return;
        }
        if (!string.IsNullOrWhiteSpace(runStartKey))
        {
            TryTalk(npcPresenter, runStartKey);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeRunManager();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnsubscribeRunManager();
    }

    private void UnsubscribeRunManager()
    {
        if (runManager != null && isSubscribed)
        {
            runManager.NodeEntered -= HandleNodeEntered;
            runManager.NodeCompleted -= HandleNodeCompleted;
            isSubscribed = false;
        }
    }

    private void HandleNodeEntered(MapNodeData node)
    {
        if (node == null)
            return;

        NodeDialogueKey entry = nodeDialogueKeys.Find(e => e != null && e.nodeType == node.NodeType);
        if (entry == null || string.IsNullOrWhiteSpace(entry.onEnterKey))
            return;

        TryTalk(npcPresenter, entry.onEnterKey);
    }

    private void HandleNodeCompleted(MapNodeData node)
    {
        if (node == null)
            return;

        TryTalkForNodeCompleted(node);
    }

    private void TryTalkForNodeCompleted(MapNodeData node)
    {
        if (node == null)
            return;

        NodeDialogueKey entry = nodeDialogueKeys.Find(e => e != null && e.nodeType == node.NodeType);
        if (entry == null || string.IsNullOrWhiteSpace(entry.onCompletedKey))
            return;

        TryTalk(npcPresenter, entry.onCompletedKey);
    }
}