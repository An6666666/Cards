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
        Debug.Log($"[RunSceneGuideTrigger] TryPlaySceneEntryDialogue | currentNode={(runManager!=null ? runManager.CurrentNode?.NodeType.ToString() : "null")} | runStartKey={runStartKey}");
        if (hasPlayedSceneEntryDialogue)
        return;
        TryBindPresenter();
        TryBindRunManager();
        if (runManager == null)
        return;

        // 回到 RunScene（例如戰鬥結束）時，優先播放剛完成節點的完成台詞。
        // 只有在尚未進行任何節點時，才播放開場台詞。
        if (runManager.CurrentNode != null)
        {
        // 先嘗試播放「剛完成節點」台詞
        if (TryTalkForNodeCompleted(runManager.CurrentNode))
        {
            hasPlayedSceneEntryDialogue = true;
            return;
        }

        // ✅ 如果完成台詞找不到（key 沒配/資料庫沒收錄），就退回播開場台詞（或通用台詞）
        // 這樣你就不會「常常沒講話」
        }

        if (!string.IsNullOrWhiteSpace(runStartKey))
        {
            hasPlayedSceneEntryDialogue = TryTalk(npcPresenter, runStartKey);
            if (hasPlayedSceneEntryDialogue) return;
        }

        // ✅ 兩者都沒有/都播不了，就直接視為已處理，避免 Update 無限嘗試
        hasPlayedSceneEntryDialogue = true;

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
        TryBindPresenter();
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

    private bool TryTalkForNodeCompleted(MapNodeData node)
    {
        Debug.LogWarning($"[RunSceneGuideTrigger] No completed dialogue key for nodeType={node.NodeType} (or key not found in database).");

        if (node == null)
        return false;

        TryBindPresenter();

        NodeDialogueKey entry = nodeDialogueKeys.Find(e => e != null && e.nodeType == node.NodeType);
        if (entry == null || string.IsNullOrWhiteSpace(entry.onCompletedKey))
        return false;

        return TryTalk(npcPresenter, entry.onCompletedKey);
    }

    private void TryBindPresenter()
    {
        if (npcPresenter == null)
        {
            npcPresenter = FindObjectOfType<GuideNPCPresenter>(true);
        }
    }
}