using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class RunProgressAssetReference
{
    public string assetName;
    public string typeName;
}

[Serializable]
public class RunProgressSnapshotData
{
    public int maxHP;
    public int currentHP;
    public int gold;
    public List<RunProgressAssetReference> deck = new List<RunProgressAssetReference>();
    public List<RunProgressAssetReference> relics = new List<RunProgressAssetReference>();
    public List<RunProgressAssetReference> exhaustPile = new List<RunProgressAssetReference>();
}

[Serializable]
public class RunProgressNodeData
{
    public string nodeId;
    public int floorIndex;
    public int nodeType;
    public bool isCompleted;
    public string encounterId;
    public string eventId;
    public string shopName;
    public bool shopOffersGenerated;
    public List<RunProgressAssetReference> shopCardOffers = new List<RunProgressAssetReference>();
    public List<RunProgressAssetReference> shopRelicOffers = new List<RunProgressAssetReference>();
    public List<string> nextNodeIds = new List<string>();
}

[Serializable]
public class RunProgressSaveData
{
    public int version = 1;
    public string bootstrapRunSceneName;
    public string resumeSceneName;
    public int runSequenceId;
    public bool tutorialRun;
    public bool runCompleted;
    public string currentNodeId;
    public string activeNodeId;
    public List<int> selectedElements = new List<int>();
    public List<string> guideFlags = new List<string>();
    public bool suppressDefaultShopEntryDialogueOnce;
    public List<RunProgressNodeData> nodes = new List<RunProgressNodeData>();
    public RunProgressSnapshotData initialPlayerSnapshot;
    public RunProgressSnapshotData currentRunSnapshot;
}

public static class RunProgressPersistence
{
    private const string SaveFileName = "run_progress.json";

    private static bool pendingResumeRequested;
    private static RunProgressSaveData pendingResumeData;

    public static bool HasSavedProgress()
    {
        return TryLoadSavedProgress(out _);
    }

    public static bool TryLoadSavedProgress(out RunProgressSaveData data)
    {
        data = null;

        string path = GetSaveFilePath();
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            data = JsonUtility.FromJson<RunProgressSaveData>(json);
            if (data == null)
            {
                return false;
            }

            if (data.tutorialRun)
            {
                return false;
            }

            data.selectedElements ??= new List<int>();
            data.guideFlags ??= new List<string>();
            data.nodes ??= new List<RunProgressNodeData>();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"RunProgressPersistence: failed to load saved progress. {ex.Message}");
            return false;
        }
    }

    public static bool TryPrepareResumeFromTitle(out string bootstrapRunSceneName)
    {
        bootstrapRunSceneName = "RunScene";

        if (!TryLoadSavedProgress(out RunProgressSaveData data))
        {
            return false;
        }

        pendingResumeRequested = true;
        pendingResumeData = data;
        if (!string.IsNullOrWhiteSpace(data.bootstrapRunSceneName))
        {
            bootstrapRunSceneName = data.bootstrapRunSceneName;
        }

        return true;
    }

    public static bool ConsumePendingResumeRequest(out RunProgressSaveData data)
    {
        if (!pendingResumeRequested)
        {
            data = null;
            return false;
        }

        pendingResumeRequested = false;
        data = pendingResumeData;
        pendingResumeData = null;
        return data != null;
    }

    public static void Save(RunProgressSaveData data)
    {
        if (data == null || data.tutorialRun)
        {
            return;
        }

        try
        {
            string path = GetSaveFilePath();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"RunProgressPersistence: failed to save progress. {ex.Message}");
        }
    }

    public static void ClearSavedProgress()
    {
        pendingResumeRequested = false;
        pendingResumeData = null;

        try
        {
            string path = GetSaveFilePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"RunProgressPersistence: failed to clear saved progress. {ex.Message}");
        }
    }

    private static string GetSaveFilePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }
}
