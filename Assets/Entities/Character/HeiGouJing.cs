using System.Collections.Generic;                 // 引用泛型集合（List等）以管理分身清單
using UnityEngine;                                // 引用 Unity API

public class HeiGouJing : Enemy                  // 黑狗精：繼承自 Enemy（共用血量、攻擊等基礎屬性與行為）
{
    [System.Serializable]                         // 讓內部結構可被序列化，顯示於 Inspector
    private struct CloneSettings                  // 分身設定的資料群組（整理Inspector欄位）
    {
        [Min(0)] public int count;               // 分身數量（最小值0）
        [Min(1)] public int maxHP;               // 分身最大生命（最小值1，避免0血無法存活）
        [Min(0)] public int baseAttackDamage;    // 分身基礎攻擊（最小值0）
    }

    [Header("黑狗精分身設定")]                     // Inspector 分組標題
    [SerializeField] private CloneSettings cloneSettings = new CloneSettings
    {
        count = 2,                               // 預設分身數量：2
        maxHP = 15,                              // 預設分身最大生命：15
        baseAttackDamage = 5                     // 預設分身攻擊：5
    };

    private bool hasSplitTriggered = false;      // 是否已觸發過分裂（避免重複觸發）
    private bool isClone = false;                // 這個實例是否為分身（true 表示分身）
    private HeiGouJing originMain = null;        // 分身指向本體的參考（分身回報/銷毀時用）
    private List<HeiGouJing> spawnedClones = new List<HeiGouJing>(); // 本體生成的分身清單（便於統一管理與回收）

#if UNITY_EDITOR                                  // 僅在編輯器環境下編譯
    protected override void OnValidate()          // Inspector 變更時自動校正數值到安全範圍
    {
        base.OnValidate();                        // 先讓父類確保必要元件存在
        cloneSettings.count = Mathf.Max(0, cloneSettings.count);             // 分身數量下限0
        cloneSettings.maxHP = Mathf.Max(1, cloneSettings.maxHP);             // 分身血量下限1
        cloneSettings.baseAttackDamage = Mathf.Max(0, cloneSettings.baseAttackDamage); // 分身攻擊下限0
    }
#endif

    protected override void Awake()               // Unity 生命週期：初始化（在父類 Enemy.Awake 前後需注意順序）
    {
        enemyName = "黑狗精";                     // 設定敵人顯示名稱
        base.Awake();                             // 呼叫父類 Awake（通常做屬性初始化/註冊等）
    }

    public override void TakeDamage(int dmg)      // 受到一般傷害時的處理
    {
        bool shouldSplit = ShouldTriggerSplit(dmg, false); // 預先判斷此擊是否應觸發分裂（一般傷害）
        base.TakeDamage(dmg);                     // 先套用父類邏輯（包含扣血、格擋等）
        if (shouldSplit && currentHP > 0)         // 若符合分裂條件且未被打死
        {
            TriggerSplit();                       // 觸發分裂（重置血量+瞬移+生分身）
        }
    }

    public override void TakeTrueDamage(int dmg)  // 受到真實傷害時的處理（不經格擋）
    {
        bool shouldSplit = ShouldTriggerSplit(dmg, true);  // 判斷真實傷害是否應觸發分裂
        base.TakeTrueDamage(dmg);                 // 呼叫父類真實傷害處理
        if (shouldSplit && currentHP > 0)         // 若符合分裂條件且仍存活
        {
            TriggerSplit();                       // 觸發分裂
        }
    }

    private bool ShouldTriggerSplit(int dmg, bool isTrueDamage) // 檢查這次受傷是否要觸發分裂
    {
        if (hasSplitTriggered || isClone || dmg <= 0)           // 已分裂過/這是分身/傷害<=0 → 不觸發
            return false;

        int effectiveDamage = isTrueDamage ? dmg : Mathf.Max(0, dmg - block); // 計算實際承受傷害（一般傷害扣掉block）
        if (effectiveDamage <= 0)                             // 若被格擋吸收完，則不觸發
            return false;

        int remainingHP = currentHP - effectiveDamage;        // 扣血後的剩餘生命
        return remainingHP > 0;                               // 僅在「非致死」的情況才觸發分裂
    }

    private void TriggerSplit()                    // 分裂行為本體：標記、回滿血、瞬移與產生分身
    {
        hasSplitTriggered = true;                  // 標記已分裂
        currentHP = maxHP;                         // 回滿血（分裂後恢復戰力）
        SpawnClonesAndTeleport();                  // 進行瞬移並在其他格生出分身
    }

    private void SpawnClonesAndTeleport()          // 找可用格 → 本體瞬移 → 依設定生成分身
    {
        Board board = FindObjectOfType<Board>();   // 取得棋盤（用於查詢座標、Tile等）
        BattleManager battleManager = FindObjectOfType<BattleManager>(); // 取得戰鬥管理器（維護敵人清單）
        if (board == null || battleManager == null) // 安全檢查（缺少必要系統就中止）
            return;

        List<Vector2Int> availablePositions = board.GetAllPositions(); // 取得所有格座標（後續會過濾不可用）
        Player player = FindObjectOfType<Player>(); // 找玩家（用於排除玩家所在格）
        if (player != null)
        {
            availablePositions.Remove(player.position);       // 移除玩家當前所在的網格（避免重疊）
        }

        Enemy[] allEnemies = FindObjectsOfType<Enemy>();      // 找場上所有敵人
        foreach (Enemy enemy in allEnemies)
        {
            if (enemy != null && enemy != this)
            {
                availablePositions.Remove(enemy.gridPosition); // 移除其他敵人所占用的格
            }
        }

        availablePositions.Remove(gridPosition);               // 也移除自己目前所在格（避免瞬移回原地）

        if (availablePositions.Count == 0)                     // 若沒有空格可用就中止
            return;

        Vector2Int newMainPos = PopRandomPosition(availablePositions); // 隨機挑一格做為本體的新位置
        MoveToPosition(newMainPos);                           // 本體瞬移到新位置（請確保有更新佔位/動畫）

        int clonesToSpawn = Mathf.Min(Mathf.Max(0, cloneSettings.count), availablePositions.Count); // 依剩餘空格量調整最終生成數
        for (int i = 0; i < clonesToSpawn; i++)
        {
            Vector2Int clonePos = PopRandomPosition(availablePositions); // 為分身挑一個空格
            BoardTile tile = board.GetTileAt(clonePos);                  // 取該格的Tile參考（以便拿世界座標/可走性）
            if (tile == null)
            {
                continue;                                                // 找不到Tile就跳過
            }

            HeiGouJing clone = Instantiate(this, tile.transform.position, Quaternion.identity); // 以「本體」為樣板生成一個分身實例
            InitializeClone(clone, clonePos, battleManager);             // 初始化分身屬性、旗標、註冊到戰鬥
        }
    }

    private void InitializeClone(HeiGouJing clone, Vector2Int clonePos, BattleManager battleManager) // 分身初始化
    {
        clone.hasSplitTriggered = true;                   // 分身不再允許觸發二次分裂
        clone.isClone = true;                             // 標記此實例為分身
        clone.originMain = this;                          // 分身回指向本體（銷毀時可回報）
        clone.enemyName = "黑狗精分身";                  // 分身顯示名稱
        clone.maxHP = Mathf.Max(1, cloneSettings.maxHP);  // 套用 clone 設定的最大生命（含下限保護）
        clone.currentHP = clone.maxHP;                    // 分身生成時回滿血
        clone.BaseAttackDamage = Mathf.Max(0, cloneSettings.baseAttackDamage); // 套用 clone 攻擊（含下限保護）
        clone.block = 0;                                  // 分身初始格擋清零（避免繼承本體當前格擋）
        clone.gridPosition = clonePos;                    // 設定分身的邏輯座標
        clone.spawnedClones = new List<HeiGouJing>();     // 分身自身也擁有獨立清單（避免與本體共用狀態）
        clone.SetHighlight(false);

        if (!battleManager.enemies.Contains(clone))       // 確認未重複註冊
        {
            battleManager.enemies.Add(clone);             // 註冊到戰鬥管理器的敵人清單
        }

        RegisterClone(clone);                              // 本體把此分身加入自己的分身清單（便於回收管理）
    }

    private static Vector2Int PopRandomPosition(List<Vector2Int> positions) // 從清單中隨機取出一個座標並移除（不重複）
    {
        int idx = Random.Range(0, positions.Count);       // 隨機索引
        Vector2Int pos = positions[idx];                  // 取出座標
        positions.RemoveAt(idx);                          // 從候選清單移除
        return pos;                                       // 回傳該座標
    }

    private void RegisterClone(HeiGouJing clone)          // 將分身加入本體的分身清單（去重）
    {
        if (!spawnedClones.Contains(clone))               // 若尚未收錄
        {
            spawnedClones.Add(clone);                     // 加入管理
        }
    }

    private void UnregisterClone(HeiGouJing clone)        // 從本體的分身清單中移除指定分身
    {
        spawnedClones.Remove(clone);                      // 移除（若不存在則無事發生）
    }

    private void OnDestroy()                              // 當此物件被銷毀時（可能是本體或分身）
    {
        if (isClone)                                      // 如果是分身
        {
            if (originMain != null)                       // 且仍有指向本體
            {
                originMain.UnregisterClone(this);         // 通知本體把我從清單移除
            }
            return;                                       // 分身到此結束（不處理其他分身）
        }

        // 若是本體被銷毀：一併回收自己所生成的分身（避免留下孤兒物件）
        foreach (var clone in spawnedClones)
        {
            if (clone != null)
            {
                clone.originMain = null;                  // 斷開分身對本體的引用
                Destroy(clone.gameObject);                // 銷毀分身實體
            }
        }
        spawnedClones.Clear();                            // 清空分身管理清單
    }
}
