using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class RunManager
{
    private bool TryResumeSavedRunIfRequested()
    {
        if (!RunProgressPersistence.ConsumePendingResumeRequest(out RunProgressSaveData data) || data == null)
        {
            return false;
        }

        RestoreSavedRun(data);
        return true;
    }

    private void RestoreSavedRun(RunProgressSaveData data)
    {
        suppressAutosave = false;
        tutorialRun = data.tutorialRun;
        runCompleted = data.runCompleted;
        runSequenceId = Mathf.Max(0, data.runSequenceId);

        guideFlags.Clear();
        if (data.guideFlags != null)
        {
            for (int i = 0; i < data.guideFlags.Count; i++)
            {
                string flag = data.guideFlags[i];
                if (!string.IsNullOrWhiteSpace(flag))
                {
                    guideFlags.Add(flag.Trim());
                }
            }
        }

        suppressDefaultShopEntryDialogueOnce = data.suppressDefaultShopEntryDialogueOnce;
        RestoreSelectedElements(data.selectedElements);

        initialPlayerSnapshot = ResolveSnapshot(data.initialPlayerSnapshot);
        currentRunSnapshot = ResolveSnapshot(data.currentRunSnapshot) ?? initialPlayerSnapshot?.Clone();

        eventResolver.Player = player;
        eventResolver.InitialPlayerSnapshot = initialPlayerSnapshot;
        eventResolver.CurrentRunSnapshot = currentRunSnapshot;

        Dictionary<string, MapNodeData> nodesById = RestoreMapNodes(data.nodes);
        ApplyConfigNodeOverrides();
        currentNode = ResolveNodeById(nodesById, data.currentNodeId);
        activeNode = ResolveNodeById(nodesById, data.activeNodeId);

        MapGenerated?.Invoke(mapFloors);
        RaiseRunSnapshotChanged();
        MapStateChanged?.Invoke();

        string resumeScene = DetermineResumeSceneName(data);
        string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!string.IsNullOrWhiteSpace(resumeScene) &&
            !string.Equals(activeSceneName, resumeScene, StringComparison.Ordinal))
        {
            StartCoroutine(ResumeSavedSceneNextFrame(resumeScene));
            return;
        }

        TryResumePendingNodeTransitionInCurrentScene();
    }

    private IEnumerator ResumeSavedSceneNextFrame(string sceneName)
    {
        yield return null;

        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            SceneTransitionLoader.LoadScene(sceneName);
        }
    }

    private void TryResumePendingNodeTransitionInCurrentScene()
    {
        if (activeNode == null)
        {
            return;
        }

        if (activeNode.NodeType != MapNodeType.Event && activeNode.NodeType != MapNodeType.Rest)
        {
            return;
        }

        if (pendingNodeTransitionCoroutine != null)
        {
            StopCoroutine(pendingNodeTransitionCoroutine);
        }

        pendingNodeTransitionCoroutine = StartCoroutine(HandleNodeTransition(activeNode));
    }

    private void SaveCurrentProgress()
    {
        if (tutorialRun || suppressAutosave || runCompleted || mapFloors.Count == 0)
        {
            return;
        }

        RunProgressSaveData data = BuildSaveData();
        if (data != null)
        {
            RunProgressPersistence.Save(data);
        }
    }

    private RunProgressSaveData BuildSaveData()
    {
        RunProgressSaveData data = new RunProgressSaveData
        {
            bootstrapRunSceneName = string.IsNullOrWhiteSpace(runSceneName)
                ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                : runSceneName,
            resumeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            runSequenceId = runSequenceId,
            tutorialRun = tutorialRun,
            runCompleted = runCompleted,
            currentNodeId = currentNode != null ? currentNode.NodeId : null,
            activeNodeId = activeNode != null ? activeNode.NodeId : null,
            suppressDefaultShopEntryDialogueOnce = suppressDefaultShopEntryDialogueOnce
        };

        if (StartingDeckSelection.TryGetRunSelectedElements(out IReadOnlyList<ElementType> selectedElements) &&
            selectedElements != null)
        {
            for (int i = 0; i < selectedElements.Count; i++)
            {
                data.selectedElements.Add((int)selectedElements[i]);
            }
        }

        foreach (string flag in guideFlags)
        {
            if (!string.IsNullOrWhiteSpace(flag))
            {
                data.guideFlags.Add(flag);
            }
        }

        for (int floorIndex = 0; floorIndex < mapFloors.Count; floorIndex++)
        {
            List<MapNodeData> floor = mapFloors[floorIndex];
            if (floor == null)
            {
                continue;
            }

            for (int nodeIndex = 0; nodeIndex < floor.Count; nodeIndex++)
            {
                MapNodeData node = floor[nodeIndex];
                if (node == null)
                {
                    continue;
                }

                RunProgressNodeData nodeData = new RunProgressNodeData
                {
                    nodeId = node.NodeId,
                    floorIndex = node.FloorIndex,
                    nodeType = (int)node.NodeType,
                    isCompleted = node.IsCompleted,
                    encounterId = node.Encounter != null ? node.Encounter.EncounterId : null,
                    eventId = node.Event != null ? node.Event.EventId : null,
                    shopName = node.ShopInventory != null ? node.ShopInventory.name : null,
                    shopOffersGenerated = node.ShopOffersGenerated
                };

                if (node.ShopOffersGenerated)
                {
                    AddAssetReferences(node.ShopCardOffers, nodeData.shopCardOffers);
                    AddAssetReferences(node.ShopRelicOffers, nodeData.shopRelicOffers);
                }

                IReadOnlyList<MapNodeData> nextNodes = node.NextNodes;
                if (nextNodes != null)
                {
                    for (int nextIndex = 0; nextIndex < nextNodes.Count; nextIndex++)
                    {
                        MapNodeData nextNode = nextNodes[nextIndex];
                        if (nextNode != null && !string.IsNullOrWhiteSpace(nextNode.NodeId))
                        {
                            nodeData.nextNodeIds.Add(nextNode.NodeId);
                        }
                    }
                }

                data.nodes.Add(nodeData);
            }
        }

        PlayerRunSnapshot snapshotToSave = player != null
            ? PlayerRunSnapshot.Capture(player)
            : currentRunSnapshot;

        data.initialPlayerSnapshot = BuildSnapshotData(initialPlayerSnapshot);
        data.currentRunSnapshot = BuildSnapshotData(snapshotToSave);
        return data;
    }

    private void RestoreSelectedElements(IEnumerable<int> selectedElementValues)
    {
        if (selectedElementValues == null)
        {
            StartingDeckSelection.ClearSelection();
            return;
        }

        List<ElementType> selectedElements = new List<ElementType>();
        foreach (int value in selectedElementValues)
        {
            if (!Enum.IsDefined(typeof(ElementType), value))
            {
                continue;
            }

            ElementType element = (ElementType)value;
            if (!selectedElements.Contains(element))
            {
                selectedElements.Add(element);
            }
        }

        if (selectedElements.Count == 0)
        {
            StartingDeckSelection.ClearSelection();
            return;
        }

        StartingDeckSelection.SetSelection(selectedElements, selectedElements.Count);
    }

    private string DetermineResumeSceneName(RunProgressSaveData data)
    {
        string resumeScene = !string.IsNullOrWhiteSpace(data.resumeSceneName)
            ? data.resumeSceneName
            : runSceneName;

        if (!string.Equals(resumeScene, runSceneName, StringComparison.Ordinal))
        {
            return resumeScene;
        }

        if (activeNode == null)
        {
            return runSceneName;
        }

        switch (activeNode.NodeType)
        {
            case MapNodeType.Battle:
            case MapNodeType.EliteBattle:
            case MapNodeType.Boss:
                return battleSceneName;
            case MapNodeType.Shop:
                return shopSceneName;
            default:
                return runSceneName;
        }
    }
}
