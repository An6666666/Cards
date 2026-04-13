using System.Collections.Generic;
using UnityEngine;

public partial class BattleManager
{
    public void RefreshRelicUI()
    {
        ClearSpawnedRelicUI();

        if (relicUIPrefab == null || relicUIParent == null)
        {
            return;
        }

        IReadOnlyList<RelicBase> relics = ResolveBattleRelics();
        if (relics == null)
        {
            return;
        }

        for (int i = 0; i < relics.Count; i++)
        {
            RelicBase relic = relics[i];
            if (relic == null)
            {
                continue;
            }

            GameObject relicUiObject = Instantiate(relicUIPrefab, relicUIParent, false);
            ApplyRelicUIData(relicUiObject, relic);
            spawnedRelicUiObjects.Add(relicUiObject);
        }
    }

    private void ApplyRelicUIData(GameObject relicUiObject, RelicBase relic)
    {
        if (relicUiObject == null)
        {
            return;
        }

        BattleRelicUIItem itemView = relicUiObject.GetComponent<BattleRelicUIItem>()
            ?? relicUiObject.GetComponentInChildren<BattleRelicUIItem>(true);

        if (itemView == null)
        {
            itemView = relicUiObject.AddComponent<BattleRelicUIItem>();
        }

        itemView.Bind(relic);
    }

    private IReadOnlyList<RelicBase> ResolveBattleRelics()
    {
        if (player != null && player.relics != null)
        {
            return player.relics;
        }

        return runManager?.CurrentRunSnapshot?.relics;
    }

    private void ClearSpawnedRelicUI()
    {
        for (int i = 0; i < spawnedRelicUiObjects.Count; i++)
        {
            GameObject relicUiObject = spawnedRelicUiObjects[i];
            if (relicUiObject != null)
            {
                Destroy(relicUiObject);
            }
        }

        spawnedRelicUiObjects.Clear();
    }

    private void SubscribeRunSnapshot()
    {
        if (runManager == null)
        {
            runManager = RunManager.Instance;
        }

        if (runManager == null)
        {
            return;
        }

        runManager.RunSnapshotChanged -= HandleRunSnapshotChanged;
        runManager.RunSnapshotChanged += HandleRunSnapshotChanged;
    }

    private void UnsubscribeRunSnapshot()
    {
        if (runManager == null)
        {
            return;
        }

        runManager.RunSnapshotChanged -= HandleRunSnapshotChanged;
    }

    private void HandleRunSnapshotChanged(PlayerRunSnapshot snapshot)
    {
        RefreshRelicUI();
    }
}
