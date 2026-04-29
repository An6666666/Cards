using System;
using System.Collections.Generic;
using UnityEngine;

public class BattleRewardController
{
    private readonly BattleManager battleManager;
    private readonly Player player;
    private readonly List<CardBase> allCardPool;
    private readonly RewardUI rewardUIPrefab;
    private readonly Transform handPanel;
    private readonly float normalBattleRelicRewardChance;
    private readonly float eliteBattleRelicRewardChance;
    private readonly int normalBattleRelicChoiceCount;

    private int defeatedEnemyCount;
    private int totalGoldReward;
    private RewardUI rewardUIInstance;
    private bool victoryRewardsShown;

    public BattleRewardController(
        BattleManager battleManager,
        Player player,
        List<CardBase> allCardPool,
        RewardUI rewardUIPrefab,
        Transform handPanel,
        float normalBattleRelicRewardChance,
        float eliteBattleRelicRewardChance,
        int normalBattleRelicChoiceCount)
    {
        this.battleManager = battleManager;
        this.player = player;
        this.allCardPool = allCardPool;
        this.rewardUIPrefab = rewardUIPrefab;
        this.handPanel = handPanel;
        this.normalBattleRelicRewardChance = Mathf.Clamp01(normalBattleRelicRewardChance);
        this.eliteBattleRelicRewardChance = Mathf.Clamp01(eliteBattleRelicRewardChance);
        this.normalBattleRelicChoiceCount = Mathf.Max(1, normalBattleRelicChoiceCount);
    }

    public void OnEnemyDefeated(Enemy enemy)
    {
        defeatedEnemyCount++;
        totalGoldReward += Mathf.Max(0, enemy != null ? enemy.GoldReward : 0);
    }

    public void ShowVictoryRewards()
    {
        if (victoryRewardsShown)
        {
            Debug.LogWarning("BattleRewardController: duplicate ShowVictoryRewards call ignored.");
            return;
        }

        victoryRewardsShown = true;
        int goldReward = totalGoldReward;
        player.AddGold(goldReward);
        BattleEndSummaryStore.RegisterGoldEarned(goldReward);

        List<CardBase> cardChoices = RunCardPoolSelector.GetRewardChoices(allCardPool, 3);
        List<RelicBase> relicChoices = BuildRelicRewardChoices();
        Canvas canvas = handPanel != null ? handPanel.GetComponentInParent<Canvas>() : UnityEngine.Object.FindObjectOfType<Canvas>();

        if (rewardUIInstance == null)
            rewardUIInstance = UnityEngine.Object.Instantiate(rewardUIPrefab, canvas.transform);

        rewardUIInstance.Show(battleManager, goldReward, cardChoices, relicChoices);
    }

    public int GetResolvedGoldReward()
    {
        return Mathf.Max(0, totalGoldReward);
    }

    private List<RelicBase> BuildRelicRewardChoices()
    {
        RunManager runManager = RunManager.Instance;
        MapNodeData activeNode = runManager != null ? runManager.ActiveNode : null;
        if (activeNode == null)
        {
            return null;
        }

        float rewardChance = GetRelicRewardChance(activeNode.NodeType);
        if (rewardChance <= 0f)
        {
            return null;
        }

        if (ShouldSuppressRelicRewards(activeNode, runManager))
        {
            return null;
        }

        if (UnityEngine.Random.value > rewardChance)
        {
            return null;
        }

        IReadOnlyList<RelicBase> sourcePool = runManager.DefaultShopInventory != null
            ? runManager.DefaultShopInventory.PurchasableRelics
            : null;

        if (sourcePool == null || sourcePool.Count == 0)
        {
            return null;
        }

        List<RelicBase> availablePool = new List<RelicBase>();
        HashSet<string> addedKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < sourcePool.Count; i++)
        {
            RelicBase relic = sourcePool[i];
            if (relic == null)
            {
                continue;
            }

            string key = GetRelicKey(relic);
            if (string.IsNullOrEmpty(key) || !addedKeys.Add(key))
            {
                continue;
            }

            availablePool.Add(relic);
        }

        if (availablePool.Count == 0)
        {
            return null;
        }

        int choiceCount = Mathf.Min(normalBattleRelicChoiceCount, availablePool.Count);
        List<RelicBase> selections = new List<RelicBase>(choiceCount);
        for (int i = 0; i < choiceCount; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, availablePool.Count);
            selections.Add(availablePool[randomIndex]);
            availablePool.RemoveAt(randomIndex);
        }

        return selections;
    }

    private float GetRelicRewardChance(MapNodeType nodeType)
    {
        switch (nodeType)
        {
            case MapNodeType.Battle:
                return normalBattleRelicRewardChance;
            case MapNodeType.EliteBattle:
                return eliteBattleRelicRewardChance;
            default:
                return 0f;
        }
    }

    private static bool ShouldSuppressRelicRewards(MapNodeData activeNode, RunManager runManager)
    {
        if (activeNode == null)
        {
            return true;
        }

        // 教學流程中的菁英戰仍然要保留法器獎勵；只有非菁英教學戰鬥才隱藏法器。
        if (activeNode.NodeType == MapNodeType.EliteBattle)
        {
            return false;
        }

        return (runManager != null && runManager.IsTutorialRun)
            || (activeNode.Encounter != null && activeNode.Encounter.UseTutorialBattle);
    }

    private static string GetRelicKey(RelicBase relic)
    {
        if (relic == null)
        {
            return null;
        }

        string relicName = relic.name;
        if (string.IsNullOrWhiteSpace(relicName))
        {
            relicName = relic.GetType().FullName;
        }

        const string cloneSuffix = "(Clone)";
        if (relicName.EndsWith(cloneSuffix, StringComparison.Ordinal))
        {
            relicName = relicName.Substring(0, relicName.Length - cloneSuffix.Length).TrimEnd();
        }

        return relicName;
    }
}
