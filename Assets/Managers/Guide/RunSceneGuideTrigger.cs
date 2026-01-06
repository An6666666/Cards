using System.Collections.Generic;
using UnityEngine;

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

    private void Awake()
    {
        if (runManager == null)
        {
            runManager = RunManager.Instance;
        }

        if (runManager != null)
        {
            runManager.NodeEntered += HandleNodeEntered;
            runManager.NodeCompleted += HandleNodeCompleted;
        }
    }

    private void Start()
    {
        Debug.Log("[RunSceneGuideTrigger] Start()");
        if (!string.IsNullOrWhiteSpace(runStartKey))
        {
            TryTalk(npcPresenter, runStartKey);
        }
        npcPresenter.Talk("Run_Start");
    }

    private void OnDestroy()
    {
        if (runManager != null)
        {
            runManager.NodeEntered -= HandleNodeEntered;
            runManager.NodeCompleted -= HandleNodeCompleted;
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

        NodeDialogueKey entry = nodeDialogueKeys.Find(e => e != null && e.nodeType == node.NodeType);
        if (entry == null || string.IsNullOrWhiteSpace(entry.onCompletedKey))
            return;

        TryTalk(npcPresenter, entry.onCompletedKey);
    }
}