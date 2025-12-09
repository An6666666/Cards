using System;                                // 使用 System 命名空間，支援序列化等基礎功能
using System.Collections;                   // 使用非泛型集合與 IEnumerator（協程等）
using System.Collections.Generic;           // 使用泛型集合（List<T> 等）
using UnityEngine;                          // 使用 Unity 引擎核心 API
using UnityEngine.UI;                       // 使用 Unity UI（Text、Button 等）

[Serializable]                              // 標記此類可序列化，方便在 Inspector 顯示/編輯
public class EnemySpawnConfig               // 敵人生成的設定資料（Prefab + 數量）
{
    public Enemy enemyPrefab;               // 要生成的敵人預製物
    public int count = 1;                   // 生成此敵人數量，預設 1
}

public class BattleManager : MonoBehaviour  // 戰鬥管理器，整場戰鬥的中樞腳本（掛在場景物件）
{
    [Header("Core References")]             // Inspector 區塊標題：核心參考
    public Player player;                   // 場上的玩家物件引用
    [NonSerialized] public List<Enemy> enemies = new List<Enemy>();
    // 場上所有敵人的列表（不序列化，避免編輯器追蹤 runtime 物件）

    public Board board;                     // 棋盤 Board 物件（管理格子）

    [Header("Cards & UI")]                  // Inspector 區塊標題：卡片與 UI
    public GameObject cardPrefab;           // 卡片 UI 的預製物
    public Transform handPanel;             // UI：手牌區域的父物件
    public Transform deckPile;              // UI：牌庫顯示區域（通常只顯示數字）
    public Transform discardPile;           // UI：棄牌堆顯示區域（通常只顯示數字）
    public Text energyText;                 // UI：顯示玩家能量的 Text
    [SerializeField] private Button endTurnButton;
    // UI：「結束回合」按鈕（私有但可在 Inspector 指定）

    [Header("Initial Setup")]               // Inspector 區塊標題：初始設定
    public List<EnemySpawnConfig> enemySpawnConfigs = new List<EnemySpawnConfig>();
    // 初始戰鬥遭遇的敵人生成配置（可在 Inspector 設定）

    public Vector2Int playerStartPos = Vector2Int.zero;
    // 玩家在棋盤上的起始格子座標（預設 0,0）

    private readonly BattleStateMachine stateMachine = new BattleStateMachine();
    // 戰鬥狀態機，負責管理 PlayerTurn/EnemyTurn/Victory/Defeat 等狀態

    [Header("Guaranteed Cards")]            // Inspector 區塊標題：保證卡（例如保證一張移動卡）
    public Move_YiDong guaranteedMovementCard;
    // 保證會給玩家的一張「移動卡」樣板（ScriptableObject）

    private Move_YiDong guaranteedMovementCardInstance;
    // 在這場戰鬥中真正使用的「保證移動卡」實例（從樣板 Clone）

    [Header("Rewards")]                     // Inspector 區塊標題：戰利品/獎勵
    public List<CardBase> allCardPool = new List<CardBase>();
    // 所有可能作為勝利獎勵的卡池列表

    public RewardUI rewardUIPrefab;         // 勝利後顯示獎勵的 UI 預製物

    [Header("Timings")]                     // Inspector 區塊標題：時間相關設定
    public float cardUseDelay = 0f;         // 使用卡片後，解鎖卡片互動的延遲秒數

    private bool battleStarted = false;     // 戰鬥是否已經開始（開始前不觸發勝負判定）

    private BattleEncounterLoader encounterLoader;      // 負責載入戰鬥遭遇/生成敵人/選起始格的控制器
    private BattleTurnController turnController;        // 管理回合流程（玩家回合與敵人回合）的控制器
    private BattleHandUIController handUIController;    // 管理手牌 UI 與能量顯示的控制器
    private MovementSelectionController movementSelectionController;
    // 管理「移動卡」選格與移動邏輯的控制器

    private AttackSelectionController attackSelectionController;
    // 管理「攻擊卡」選取敵人目標的控制器

    private BattleRewardController rewardController;
    // 管理敵人死亡計數與勝利獎勵 UI 的控制器

    public bool BattleStarted => battleStarted;
    // 公開唯讀屬性：目前戰鬥是否已開始

    public BattleStateMachine StateMachine => stateMachine;
    // 公開唯讀屬性：提供外部存取戰鬥狀態機（例如狀態判斷）

    public bool IsProcessingEnemyTurnStart => turnController != null && turnController.IsProcessingEnemyTurnStart;
    // 是否正在處理敵方回合開始（提供給其他系統判斷時機用）

    public bool IsCardInteractionLocked => handUIController != null && handUIController.IsCardInteractionLocked;
    // 是否目前卡片互動被鎖定（給 CardUI 同步互動權限用）

    void Awake()
    {
        InitializeControllers();                            // 初始化各種子控制器（Hand、Movement、Attack、Reward、Encounter、Turn）
        handUIController.SetEndTurnButtonInteractable(false);
        // 一開始先把「結束回合」按鈕設為不可點（等戰鬥正式開始才開）

        encounterLoader.LoadEncounterFromRunManager();
        // 從 RunManager 目前節點載入遭遇設定，填 enemySpawnConfigs
    }

    void Start()
    {
        StartCoroutine(encounterLoader.GameStartRoutine());
        // 開始戰鬥起始協程：選玩家起始格 → 生成敵人 → 切換到玩家回合等
    }

    void Update()
    {
        stateMachine.Update();                              // 每幀更新戰鬥狀態機（讓當前狀態有機會執行邏輯）

        if (!battleStarted) return;                         // 若戰鬥尚未開始，不進行勝敗判斷

        enemies.RemoveAll(e => e == null);
        // 移除列表中已被 Destroy 的敵人（避免 Null 引用）

        bool allDead = enemies.Count == 0 || enemies.TrueForAll(e => e.currentHP <= 0);
        // allDead 條件：1) 敵人數量為 0，或 2) 所有敵人 currentHP <= 0

        if (allDead && !(stateMachine.Current is VictoryState))
        {
            stateMachine.ChangeState(new VictoryState(this));
            // 若全部敵人死亡，且目前不是勝利狀態 → 切換到 VictoryState
        }

        if (player.currentHP <= 0 && !(stateMachine.Current is DefeatState))
        {
            stateMachine.ChangeState(new DefeatState(this));
            // 若玩家血量歸 0，且目前不是失敗狀態 → 切換到 DefeatState
        }
    }

    public void StartPlayerTurn()
    {
        turnController.StartPlayerTurn();                   // 委託 BattleTurnController 處理「玩家回合開始」
    }

    public void EndPlayerTurn()
    {
        turnController.EndPlayerTurn();                     // 委託 BattleTurnController 處理「玩家回合結束 → 切到敵人回合」
    }

    public IEnumerator EnemyTurnCoroutine()
    {
        return turnController.EnemyTurnCoroutine();         // 由 BattleTurnController 提供敵人回合協程
    }

    public void UseMovementCard(CardBase movementCard)
    {
        movementSelectionController.UseMovementCard(movementCard);
        // 委託 MovementSelectionController，開始移動卡的選格流程
    }

    public void CancelMovementSelection()
    {
        movementSelectionController.CancelMovementSelection();
        // 取消目前的移動選格流程，關閉高亮與可選標記
    }
    
    public bool OnTileClicked(BoardTile tile)
    {
        return movementSelectionController.OnTileClicked(tile);
        // 棋盤格子被點擊時，交給 MovementSelectionController 處理（包含起始格選擇與移動選格）
    }

    public void StartAttackSelect(CardBase attackCard)
    {
        attackSelectionController.StartAttackSelect(attackCard);
        // 玩家開始使用攻擊卡時，委託 AttackSelectionController 進入「選敵人目標」狀態
    }

    public bool OnEnemyClicked(Enemy e)
    {
        return attackSelectionController.OnEnemyClicked(e);
        // 敵人被點擊時，交由 AttackSelectionController 判斷是否為有效攻擊目標並執行效果
    }

    public void UpdateAttackHover(Vector2 worldPosition)
    {
        attackSelectionController.UpdateAttackHover(worldPosition);
        // 拖曳攻擊卡時，更新滑鼠瞄準目標的高亮與狀態
    }

    public void EndAttackSelect()
    {
        attackSelectionController.EndAttackSelect();
        // 外部可呼叫強制結束攻擊選取（取消高亮）
    }

    public void OnEnemyDefeated(Enemy e)
    {
        rewardController.OnEnemyDefeated(e);
        // 某個敵人死亡時呼叫，讓 BattleRewardController 累積擊殺數與金幣
    }

    public void ShowVictoryRewards()
    {
        rewardController.ShowVictoryRewards();
        // 戰鬥勝利時呼叫，顯示獎勵 UI（加金幣 + 選卡獎勵）
    }

    /// <summary>
    /// 玩卡：處理費用、執行效果、棄牌、更新 UI
    /// </summary>
    public bool PlayCard(CardBase cardData)
    {
        if (!(stateMachine.Current is PlayerTurnState)) return false;
        // 只有在玩家回合才能出牌，否則失敗

        if (cardData == null) return false;                 // 傳入卡片資料為空，直接失敗
        if (player == null || player.Hand == null) return false;
        // 玩家或手牌列表不存在，直接失敗

        if (!player.Hand.Contains(cardData)) return false;
        // 這張卡不在手牌內（可能來自別的地方），直接失敗

        // 計算最終費用 (包含 Buff 修改)
        int finalCost = cardData.cost + player.GetCardCostModifier(cardData);
        // 基礎 cost + 玩家針對這張卡的額外費用修正

        if (cardData.cardType == CardType.Attack && player.buffs.nextAttackCostModify != 0)
        {
            finalCost += player.buffs.nextAttackCostModify;
            // 若是攻擊卡，且有「下一次攻擊費用修正」，則加上或減去
        }
        if (cardData.cardType == CardType.Movement && player.buffs.movementCostModify != 0)
        {
            finalCost += player.buffs.movementCostModify;
            // 若是移動卡，且有「移動卡費用修正」，則加上或減去
        }
        finalCost = Mathf.Max(0, finalCost);               // 確保費用不會小於 0

        if (player.energy < finalCost)
        {
            Debug.Log("Not enough energy");                // 能量不足，無法使用此卡
            return false;
        }

        if (cardData is Skill_ZhiJiao && !player.HasExhaustableCardInHand(cardData))
        {
            Debug.Log("No exhaustable cards in hand for 擲筊");
            return false;
        }

        Enemy target = enemies.Find(e => e != null && e.currentHP > 0);
        // 這裡簡單選擇第一個仍存活的敵人作為目標（若卡片沒自己處理目標）

        cardData.ExecuteEffect(player, target);
        // 執行卡片效果，由卡本身實作（增加護甲、造成傷害、上狀態等）

        if (cardData.cardType == CardType.Attack && player.buffs.nextAttackPlus > 0)
        {
            player.buffs.nextAttackPlus = 0;               // 用掉一次攻擊加成後，把 nextAttackPlus 歸零
        }

        // 若手牌中仍含此卡，則移至棄牌堆
        bool isGuaranteedMovement = IsGuaranteedMovementCard(cardData);
        // 判斷這張卡是否為「保證移動卡」

        bool removedFromHand = player.Hand.Remove(cardData);
        // 先從手牌中移除這張卡（若存在）

        if (removedFromHand)
        {
            player.ClearCardCostModifier(cardData);
            // 有成功從手牌移除才清除此卡的費用修正紀錄

            if (isGuaranteedMovement)
            {
                RemoveGuaranteedMovementCardFromPiles();
                // 若此卡是保證移動卡，從牌庫與棄牌堆中移除所有 YiDong（避免重複）
            }
            else if (cardData.exhaustOnUse)
            {
                player.ExhaustCard(cardData);
                // 一次性卡：使用後進 Exhaust 區（戰鬥中不再返回）
            }
            else
            {
                player.discardPile.Add(cardData);
                // 一般卡：使用後加入玩家棄牌堆
            }
        }
        else if (isGuaranteedMovement)
        {
            RemoveGuaranteedMovementCardFromPiles();
            // 極端狀況：手牌裡沒有找到，但又是保證移動卡 → 保險再清牌庫/棄牌中的 YiDong
        }

        player.UseEnergy(finalCost);                       // 扣除玩家能量
        GameEvents.RaiseCardPlayed(cardData);              // 觸發「卡片已被打出」事件，方便其他系統監聽
        handUIController.RefreshHandUI();                  // 重新整理手牌 UI（顯示棄牌後狀態）
        return true;                                       // 成功打出卡片
    }

    public void RefreshHandUI(bool playDrawAnimation = false)
    {
        handUIController.RefreshHandUI(playDrawAnimation);
        // 封裝給外部用：呼叫 HandUIController 重新整理手牌（可選是否播抽牌動畫）
    }

    internal void EnsureMovementCardInHand()
    {
        if (player == null) return;                        // 沒有玩家就不用處理

        Move_YiDong movementCard = GetGuaranteedMovementCardInstance();
        // 拿到保證移動卡的實例（若還沒建立會 Instantiate 一個）

        if (movementCard == null) return;                  // 若沒有配置樣板，直接返回

        RemoveGuaranteedMovementCardFromPiles();
        // 先從牌庫與棄牌堆移除所有 YiDong（避免重複）

        int removedDuplicateCount = 0;
        for (int i = player.Hand.Count - 1; i >= 0; i--)
        {
            CardBase card = player.Hand[i];
            if (card is Move_YiDong && !ReferenceEquals(card, movementCard))
            {
                player.Hand.RemoveAt(i);                   // 移除手牌中其它 YiDong 實例（只保留唯一那張）
                removedDuplicateCount++;                   // 記錄被移除的數量
            }
        }

        if (!player.Hand.Contains(movementCard))
        {
            player.Hand.Add(movementCard);                 // 若手牌中沒有這張保證卡，就放進手牌
        }

        if (removedDuplicateCount > 0)
        {
            player.DrawCards(removedDuplicateCount);
            // 若為了去重而移除了多張 YiDong，就補抽同數量的其他牌給玩家
        }
    }

    internal void DiscardAllHand()
    {
        Move_YiDong movementCard = guaranteedMovementCardInstance;
        // 取得保證移動卡的實例（若有）

        bool hadGuaranteedCard = false;                    // 記錄手牌裡是否原本有保證移動卡

        if (movementCard != null)
        {
            hadGuaranteedCard = player.Hand.Remove(movementCard);
            // 先從手牌中移除保證移動卡（如果存在）

            player.discardPile.Remove(movementCard);
            // 確保棄牌堆中不會留著這張保證卡
        }

        player.discardPile.AddRange(player.Hand);
        // 把剩餘手牌全部丟到棄牌堆中

        player.Hand.Clear();
        // 清空手牌列表

        if (hadGuaranteedCard)
        {
            player.Hand.Add(movementCard);
            // 若原本有保證卡，結束時再重新放回手牌（使其不會被棄掉）
        }

        RemoveGuaranteedMovementCardFromPiles();
        // 重新清理牌庫與棄牌堆中所有 YiDong（防止殘留）

        handUIController.RefreshHandUI();
        // 更新 UI，顯示新的手牌狀態
    }

    internal void SetBattleStarted(bool value)
    {
        battleStarted = value;                             // 由外部設定戰鬥是否已開始
    }

    internal void SetEndTurnButtonInteractable(bool value)
    {
        handUIController.SetEndTurnButtonInteractable(value);
        // 透過 HandUIController 控制「結束回合」按鈕是否可互動
    }

    internal bool IsGuaranteedMovementCard(CardBase card)
    {
        if (card == null)
            return false;                                  // 空卡一定不是

        Move_YiDong instance = GetGuaranteedMovementCardInstance();
        if (instance == null)
            return false;                                  // 若根本沒設定保證卡樣板，也直接判 false

        if (ReferenceEquals(card, instance))
            return true;                                   // 若就是那個實例，則為保證移動卡

        if (guaranteedMovementCard != null && ReferenceEquals(card, guaranteedMovementCard))
            return true;                                   // 有時候也可能直接引用樣板本身

        return false;                                     // 其他情況都不是保證移動卡
    }

    internal void RemoveGuaranteedMovementCardFromPiles()
    {
        if (player == null) return;                       // 沒玩家就不用處理

        player.deck.RemoveAll(card => card is Move_YiDong);
        // 從牌庫移除所有 YiDong 類型的卡

        player.discardPile.RemoveAll(card => card is Move_YiDong);
        // 從棄牌堆移除所有 YiDong 類型的卡
    }

    private Move_YiDong GetGuaranteedMovementCardInstance()
    {
        if (guaranteedMovementCardInstance == null)
        {
            if (guaranteedMovementCard == null)
            {
                Debug.LogWarning("Guaranteed movement card template is not assigned.");
                // 若沒有在 Inspector 指定樣板，就警告一下
                return null;
            }

            guaranteedMovementCardInstance = Instantiate(guaranteedMovementCard);
            // 以樣板 Instantiate 出一個實際戰鬥用的 ScriptableObject 實例
        }

        return guaranteedMovementCardInstance;            // 回傳此實例（之後都共用這一份）
    }

    private void InitializeControllers()
    {
        handUIController = new BattleHandUIController(
            this,                                          // 傳入 BattleManager 本身
            player,                                        // 傳入玩家
            cardPrefab,                                    // 卡片 UI 預製物
            handPanel,                                     // 手牌 UI 容器
            deckPile,                                      // 牌庫顯示區
            discardPile,                                   // 棄牌堆顯示區
            energyText,                                    // 能量文字
            endTurnButton,                                 // 結束回合按鈕
            cardUseDelay);                                 // 使用卡片後延遲秒數

        movementSelectionController = new MovementSelectionController(
            this,                                          // BattleManager
            player,                                        // 玩家
            board,                                         // 棋盤
            handUIController);                             // 用來在移動時更新 UI 或鎖卡互動

        attackSelectionController = new AttackSelectionController(
            player,                                        // 玩家
            board,                                         // 棋盤（攻擊範圍高亮）
            handUIController);                             // 用來在攻擊後更新手牌 UI

        rewardController = new BattleRewardController(
            this,                                          // BattleManager
            player,                                        // 玩家（加金幣）
            allCardPool,                                   // 所有獎勵卡池
            rewardUIPrefab,                                // 獎勵 UI 預製物
            handPanel);                                    // 以手牌所在 Canvas 為父層生成 UI

        encounterLoader = new BattleEncounterLoader(
            this,                                          // BattleManager
            board,                                         // 棋盤
            player,                                        // 玩家
            enemies,                                       // 敵人列表（之後會被填入場上敵人）
            enemySpawnConfigs,                             // 生成敵人的設定列表
            stateMachine);                                 // 戰鬥狀態機（開局後切到 PlayerTurn）

        movementSelectionController.SetEncounterLoader(encounterLoader);
        // 告訴 MovementSelectionController：初期還要處理「起始格選擇」的邏輯

        turnController = new BattleTurnController(
            this,                                          // BattleManager
            player,                                        // 玩家
            enemies,                                       // 敵人列表
            stateMachine,                                  // 戰鬥狀態機
            handUIController);                             // 需要控制手牌 UI（例如鎖卡、抽牌、開關按鈕）
    }
}
