using System.Collections.Generic; // 引入泛型集合（Queue、HashSet、Dictionary 等）
using UnityEngine;                // 引入 Unity API（MonoBehaviour、SerializeField、Vector2Int 等）

public class StoneToad : Enemy      // 繼承自自訂的 Enemy 基底類別（提供回合、傷害、移動等共用邏輯）
{
    public override bool ShouldResetBlockEachTurn => false; // 石蟾蜍的護甲會持續累積

    [Header("Stone Toad Settings")] // 在 Inspector 顯示一個分組標題
    [SerializeField] private int armorGainPerHit = 4;            // 每次被擊中時增加的護甲（block）量
    [SerializeField] private int preferredDistanceInSteps = 2;   // 希望與玩家保持的步數距離（以 BFS 步數衡量）
    [SerializeField] private int maxMovementSteps = 2;           // 單回合最多可移動步數
    [SerializeField] private int armorCap = 999;                 // 護甲上限（避免無限制累積）
    private const int DefaultBaseAttackDamage = 1;               // 石蟾蜍預設的基礎攻擊
    private Vector2Int? previousGridPosition = null;             // 記錄上一次成功移動的格子，用於避免立即折返
    protected override void Awake()  // 物件初始化（覆寫 Enemy.Awake）
    {
        enemyName = "石蟾蜍";        // 設定敵人顯示名稱
        base.Awake();                // 呼叫父類別 Awake（確保基底初始化完成）
        ClampArmor();                // 啟動時做一次護甲上限檢查
    }

    private void Reset()
    {
        BaseAttackDamage = DefaultBaseAttackDamage; // 新增腳本時提供專屬預設值
    }
    
#if UNITY_EDITOR
    protected override void OnValidate()        // 在編輯器中變更序列化欄位時自動呼叫（不會在執行時呼叫）
    {
        base.OnValidate();
        ClampArmor();                // 保證編輯器中修改 armorCap/block 後仍滿足上限
    }
#endif

    public override void TakeDamage(int dmg)  // 覆寫：受到一般傷害
    {
        base.TakeDamage(dmg);                 // 先走基底受傷流程（扣血、死亡判定等）
        GainArmorFromHit();                   // 觸發被動：被打就增加護甲
    }

    public override void TakeTrueDamage(int dmg) // 覆寫：受到真實傷害（無視防禦）
    {
        base.TakeTrueDamage(dmg);               // 先走基底真傷流程
        GainArmorFromHit();                     // 仍然觸發被動：被打就增加護甲（真傷也會加）
    }

    protected internal override int CalculateAttackDamage() // 計算最終攻擊傷害（可被基底呼叫）
    {
        ClampArmor();                            // 攻擊前再次確保護甲不超上限
        int damage = base.CalculateAttackDamage(); // 先拿到父類別計算的基礎值（可能含 Buff/Debuff）
        damage += block;                         // 特色：將目前護甲值疊加為額外傷害
        return Mathf.Max(0, damage);             // 保證不會出現負傷害
    }

    protected internal override void MoveOneStepTowards(Player player) // 單回合的移動決策（朝向目標位置移動）
    {
        if (player == null) return;             // 無玩家目標就不移動

        Board board = FindObjectOfType<Board>(); // 取得棋盤系統（注意：頻繁呼叫會有成本，建議實務上快取）
        if (board == null) return;              // 沒有棋盤就不處理

        HashSet<Vector2Int> blocked = BuildBlockedPositions(player); // 構建不能走的座標集合（玩家格、其他敵人格）
        HashSet<Vector2Int> reachable = GetReachablePositions(       // 取得在「最多步數」內能到達的所有格
            board, gridPosition, maxMovementSteps, blocked);
        reachable.Add(gridPosition);                                  // 把當前位置也視為候選（可能選擇不動）

        Vector2Int bestPos = gridPosition;      // 初始化最佳位置為原地
        int bestScore = int.MaxValue;           // 最佳評分（越小越好）
        float bestEuclid = float.MaxValue;      // 歐氏距離的次要 tie-break（越小越好）

        foreach (var pos in reachable)          // 在所有候選位置中選擇最佳
        {
            int stepDistance = ComputeStepDistance(board, pos, player.position); // 以 BFS 計算 pos 到玩家的步數距離
            if (stepDistance == int.MaxValue) continue; // 不可達（或找不到路徑）則跳過

            int score = Mathf.Abs(stepDistance - preferredDistanceInSteps); // 與偏好距離的差越小越好
            if (previousGridPosition.HasValue && pos == previousGridPosition.Value)
            {
                score += 1; // 為回到上一個位置增加微小懲罰，避免無意義的來回折返
            }
            float euclid = Vector2Int.Distance(pos, player.position);       // 歐氏距離作為次要比較（更貼近視覺距離）

            if (score < bestScore || (score == bestScore && euclid < bestEuclid)) // 先比主分，再比次分
            {
                bestScore = score;      // 更新最佳評分
                bestEuclid = euclid;    // 更新最佳次分
                bestPos = pos;          // 記錄最佳位置
            }
        }

        if (bestPos != gridPosition)    // 若最佳位置不是原地
        {
            Vector2Int oldPosition = gridPosition; // 記錄移動前的位置
            MoveToPosition(bestPos);    // 直接移動到該格（注意：這是「跳到結果格」，非逐步演出）
            if (gridPosition != oldPosition)
            {
                previousGridPosition = oldPosition; // 僅在成功移動後更新前一格紀錄
            }
        }
    }

    private void GainArmorFromHit()     // 被擊中後增加護甲的共用函式
    {
        if (armorGainPerHit <= 0) return; // 沒有設定正值就不處理
        if (currentHP <= 0) return;       // 已經死亡就不處理
        block += armorGainPerHit;         // 增加護甲
        ClampArmor();                     // 立刻套用上限
    }

    private void ClampArmor()            // 將護甲限制在 0 ~ armorCap 之間
    {
        if (armorCap < 0)                // 若上限被設成負數，強制修正為 0
        {
            armorCap = 0;
        }

        if (block > armorCap)            // 若當前護甲超過上限，壓回上限
        {
            block = armorCap;
        }
    }

    private HashSet<Vector2Int> BuildBlockedPositions(Player player) // 建構不可進入的座標集合
    {
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>(); // 新建集合
        if (player != null)
        {
            blocked.Add(player.position); // 玩家所在格不可進入（避免重疊）
        }

        Enemy[] allEnemies = FindObjectsOfType<Enemy>(); // 場上所有敵人（注意：執行期搜尋開銷較大）
        foreach (var enemy in allEnemies)
        {
            if (enemy == null || enemy == this) continue; // 忽略自己與空參考
            blocked.Add(enemy.gridPosition);               // 其他敵人的格子也視為被佔用
        }

        return blocked; // 回傳封鎖集合
    }

    private HashSet<Vector2Int> GetReachablePositions( // 取得「在最多步數內」可達的所有座標
        Board board, Vector2Int start, int maxSteps, HashSet<Vector2Int> blocked)
    {
        HashSet<Vector2Int> reachable = new HashSet<Vector2Int>();      // 可達集合
        Queue<(Vector2Int pos, int steps)> pending = new Queue<(Vector2Int pos, int steps)>(); // BFS 佇列（含當前步數）
        HashSet<Vector2Int> visited = new HashSet<Vector2Int> { start }; // 已訪集合（避免重複）

        pending.Enqueue((start, 0)); // 從當前位置開始，步數=0

        while (pending.Count > 0)    // 標準 BFS 展開
        {
            var current = pending.Dequeue(); // 取出節點
            if (current.steps >= maxSteps) continue; // 超過可移動步數上限就不再展開

            foreach (var tile in board.GetAdjacentTiles(current.pos)) // 走訪所有鄰接格
            {
                Vector2Int next = tile.gridPosition;        // 下一個候選格
                if (!visited.Add(next)) continue;            // 已拜訪過則跳過
                if (blocked.Contains(next)) continue;        // 若是封鎖格（玩家/敵人）則跳過

                reachable.Add(next);                         // 記錄為「本回合可達」的候選格
                pending.Enqueue((next, current.steps + 1));  // 繼續展開下一層
            }
        }

        return reachable; // 回傳可達集合
    }

    private int ComputeStepDistance(Board board, Vector2Int start, Vector2Int target) // 計算兩點間的最短步數距離（BFS）
    {
        if (start == target) return 0; // 同格距離為 0

        Queue<(Vector2Int pos, int dist)> pending = new Queue<(Vector2Int pos, int dist)>(); // BFS 佇列：格子 + 已走步數
        HashSet<Vector2Int> visited = new HashSet<Vector2Int> { start };                     // 已訪集合

        pending.Enqueue((start, 0)); // 從起點開始，距離=0

        while (pending.Count > 0)    // 標準 BFS
        {
            var current = pending.Dequeue(); // 取出節點
            int nextDist = current.dist + 1; // 下一層的距離（步數）

            foreach (var tile in board.GetAdjacentTiles(current.pos)) // 檢視所有鄰接格
            {
                Vector2Int next = tile.gridPosition; // 下一格
                if (!visited.Add(next)) continue;     // 已訪問過則略過
                if (next == target) return nextDist;  // 若抵達目標，回傳最短步數

                pending.Enqueue((next, nextDist));    // 繼續展開
            }
        }

        return int.MaxValue; // 找不到路徑時回傳極大值（視為不可達）
    }
}
