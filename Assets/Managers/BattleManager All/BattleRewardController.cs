using System.Collections.Generic;
using UnityEngine;

public class BattleRewardController
{
    private readonly BattleManager battleManager;
    private readonly Player player;
    private readonly List<CardBase> allCardPool;
    private readonly RewardUI rewardUIPrefab;
    private readonly Transform handPanel;

    private int defeatedEnemyCount;
    private int totalGoldReward;
    private RewardUI rewardUIInstance;

    public BattleRewardController(
        BattleManager battleManager,
        Player player,
        List<CardBase> allCardPool,
        RewardUI rewardUIPrefab,
        Transform handPanel)
    {
        this.battleManager = battleManager;
        this.player = player;
        this.allCardPool = allCardPool;
        this.rewardUIPrefab = rewardUIPrefab;
        this.handPanel = handPanel;
    }

    public void OnEnemyDefeated(Enemy enemy)
    {
        defeatedEnemyCount++;
        totalGoldReward += Mathf.Max(0, enemy != null ? enemy.GoldReward : 0);
    }

    public void ShowVictoryRewards()
    {
        int goldReward = totalGoldReward;
        player.AddGold(goldReward);

        List<CardBase> cardChoices = RunCardPoolSelector.GetRewardChoices(allCardPool, 3);
        Canvas canvas = handPanel != null ? handPanel.GetComponentInParent<Canvas>() : Object.FindObjectOfType<Canvas>();

        if (rewardUIInstance == null)
            rewardUIInstance = Object.Instantiate(rewardUIPrefab, canvas.transform);

        rewardUIInstance.Show(battleManager, goldReward, cardChoices);
    }
}
