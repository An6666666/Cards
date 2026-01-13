using System.Collections;                       // 用於 IEnumerator（協程）
using System.Collections.Generic;               // 使用 List<T> 等泛型集合
using UnityEngine;                              // 使用 Unity 引擎 API（Debug、WaitForSeconds 等）

public class BattleTurnController               // 回合流程控制器：玩家回合 / 敵人回合
{
    private readonly BattleManager battleManager;          // 戰鬥管理器（切換狀態、控制整體流程）
    private readonly Player player;                        // 玩家物件
    private readonly List<Enemy> enemies;                  // 場上的敵人列表（由 BattleManager 傳入）
    private readonly BattleStateMachine stateMachine;      // 戰鬥狀態機（PlayerTurnState / EnemyTurnState 等）
    private readonly BattleHandUIController handUIController;
    // 管理手牌 UI、能量顯示與卡片互動的控制器

    private bool processingPlayerTurnStart = false;        // 是否正在處理「玩家回合開始」的旗標
    private bool processingEnemyTurnStart = false;         // 是否正在處理「敵人回合開始」的旗標

    public bool IsProcessingPlayerTurnStart => processingPlayerTurnStart;
    // 對外暴露：目前是否在跑玩家回合開始流程

    public bool IsProcessingEnemyTurnStart => processingEnemyTurnStart;
    // 對外暴露：目前是否在跑敵人回合開始流程

    public BattleTurnController(
        BattleManager battleManager,
        Player player,
        List<Enemy> enemies,
        BattleStateMachine stateMachine,
        BattleHandUIController handUIController)
    {
        this.battleManager = battleManager;                // 儲存 BattleManager
        this.player = player;                              // 儲存 Player
        this.enemies = enemies;                            // 儲存敵人列表
        this.stateMachine = stateMachine;                  // 儲存狀態機
        this.handUIController = handUIController;          // 儲存手牌 UI 控制器
    }

    public void EndPlayerTurn()
    {
        // 若戰鬥尚未開始，或目前狀態不是 PlayerTurnState，就不處理
        if (!battleManager.BattleStarted || !(stateMachine.Current is PlayerTurnState))
            return;

        handUIController.SetEndTurnButtonInteractable(false);
        // 關閉「結束回合」按鈕避免連按

        battleManager.DiscardAllHand();
        // 將手牌全部丟入棄牌堆，並處理保證移動卡邏輯

        player.EndTurn();
        // 呼叫玩家自身的回合結束邏輯（重置 buff、處理結算等）

        ApplyPlayerMiasmaDamage();
        // 玩家回合結束時：若仍站在瘴氣格上，承受瘴氣傷害

        ApplyEnemyEndTurnEffects();
        // 玩家回合結束時：觸發敵人身上的回合結束效果（例如燃燒）

        GameEvents.RaiseTurnEnded();
        // 發送「回合結束」事件給其他系統（例如計數、遞減狀態等）

        ApplyGrowthTrapDamage();
        // 玩家回合結束時：處理水+木尖刺陷阱對敵人的傷害

        stateMachine.ChangeState(new EnemyTurnState(battleManager));
        // 狀態機切換到 EnemyTurnState，開始敵人回合
    }

    private void ApplyGrowthTrapDamage()
    {
        if (battleManager.board == null) return;

        var enemiesSnapshot = new List<Enemy>(enemies);
        foreach (var enemy in enemiesSnapshot)
        {
            if (enemy == null) continue;

            var tile = battleManager.board.GetTileAt(enemy.gridPosition);
            tile?.TriggerGrowthTrap(enemy);
        }
    }
    
    private void ApplyPlayerMiasmaDamage()
    {
        if (battleManager.board == null) return;

        var tile = battleManager.board.GetTileAt(player.position);
        if (tile == null) return;

        int damage = tile.MiasmaDamage;
        if (damage > 0)
        {
            player.TakeDamage(damage);
        }
    }

    private void ApplyEnemyEndTurnEffects()
    {
        var enemiesSnapshot = new List<Enemy>(enemies);
        foreach (var enemy in enemiesSnapshot)
        {
            if (enemy != null)
            {
                enemy.ProcessPlayerTurnEnd();
            }
        }
    }

    public void StartPlayerTurn()
    {
        handUIController.LockCardInteraction();
        // 一開始先鎖住卡片互動，避免在起始流程中就被操作

        if (battleManager.BattleStarted)
            handUIController.SetEndTurnButtonInteractable(true);
        // 若戰鬥已開始，則啟用「結束回合」按鈕

        player.energy = player.maxEnergy;// 回合開始：重置能量到最大值
        EnergyUIBus.RefreshAll(player.energy, player.maxEnergy);

        UIEventBus.RaiseEnergyState(new EnergySnapshot(player.energy, player.maxEnergy));
        // 確保能量 UI 在新回合時刷新到最新數值與透明度

        handUIController.UpdateEnergyUI();
        // 更新 UI 上顯示的能量數字

        player.buffs.TickDebuffsOnPlayerTurnStart();
        // 玩家回合開始：扣減 debuff 回合數，避免敵人意圖與實際行為不同步

        processingPlayerTurnStart = true;
        // 標記：開始執行「玩家回合開始」流程

        var enemiesAtTurnStart = new List<Enemy>(enemies);
        // 建立一份敵人列表快照（避免迴圈中列表被修改）

        foreach (var e in enemiesAtTurnStart)
        {
            if (e != null)
                e.ProcessTurnStart();
            // 讓每個敵人執行自己的回合開始邏輯（刷新 buff、冷卻、被動效果等）
        }

        processingPlayerTurnStart = false;
        // 玩家回合開始流程結束

        int drawCount = player.baseHandCardCount + player.buffs.nextTurnDrawChange;
        // 計算要抽幾張牌 = 基礎手牌數 + buff 修改值

        drawCount = Mathf.Max(0, drawCount);
        // 至少為 0，避免負數

        player.buffs.nextTurnDrawChange = 0;
        // 用掉這次的抽牌修正後，清零

        player.DrawNewHand(drawCount);
        // 讓玩家重新抽一整手牌（清手牌後抽指定數量）

        battleManager.EnsureMovementCardInHand();
        // 確保手牌裡存在保證移動卡（且不重複）

        handUIController.RefreshHandUI(true);
        // 重整手牌 UI，並播放抽牌動畫（playDrawAnimation = true）

        handUIController.ApplyInteractableToAllCards(false);
        // 先把所有卡片互動關閉（等待動畫）

        battleManager.StartCoroutine(handUIController.EnableCardsAfterDelay());
        // 由 BattleManager 啟動協程，一段延遲後再開啟卡片互動

        foreach (var e in enemies)
        {
            if (e != null)
            {
                e.DecideNextIntent(player);
                // 每個敵人根據目前情況決定「下一回合意圖」（攻擊、移動、技能等）
            }
        }
    }

    public IEnumerator EnemyTurnCoroutine()
    {
        processingEnemyTurnStart = true;
        // 標記正在處理敵人回合開始

        var enemiesAtEnemyTurnStart = new List<Enemy>(enemies);
        // 建立敵人列表快照（避免中途清除或改動）

        foreach (var e in enemiesAtEnemyTurnStart)
        {
            if (e != null)
                e.ProcessTurnStart();
            // 每個敵人在敵方回合開始時，執行自己的回合開始邏輯
        }

        processingEnemyTurnStart = false;
        // 敵方回合開始流程完成

        yield return new WaitForSeconds(1f);
        // 等待 1 秒（給動畫或玩家觀察敵人狀態的時間）

        var enemiesTakingActions = new List<Enemy>(enemies);
        // 再建一份列表，用於實際執行 EnemyAction

        foreach (var e in enemiesTakingActions)
        {
            if (e != null)
                e.EnemyAction(player);
            // 讓每個敵人對玩家執行一次行動（攻擊 / 走位 / 技能等）
        }

        yield return new WaitForSeconds(1f);
        // 再等 1 秒，給玩家觀察敵人動作完成的時間

        if (!player.buffs.retainBlockNextTurn)
        {
            player.block = 0;
            // 敵方回合結束時：清除玩家 block（護盾）
        }
        else
        {
            // 本回合觸發保留護甲，消耗標記以免持續生效到後續回合
            player.buffs.retainBlockNextTurn = false;
        }

        var enemiesAtTurnEnd = new List<Enemy>(enemies);
        // 再建快照，用於回合結束邏輯

        foreach (var e in enemiesAtTurnEnd)
        {
            if (e != null && e.ShouldResetBlockEachTurn) e.block = 0;
            // 若敵人設定為「每回合重置 block」，則把敵人護盾也清零
        }

        stateMachine.ChangeState(new PlayerTurnState(battleManager));
        // 回合結束後，切回 PlayerTurnState，重新輪到玩家

        Debug.Log("Player Turn");
        // 在 Console 顯示「Player Turn」，方便 debug
    }
}
