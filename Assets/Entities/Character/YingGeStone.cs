using UnityEngine;                                         // 使用 UnityEngine 命名空間，提供 MonoBehaviour、Transform 等功能

public class YingGeStone : Enemy                           // 定義一個繼承自 Enemy 的類別，名稱為 YingGeStone（鷹鴿的石頭）
{
    [SerializeField] private int baseTurnsToWait = 2;      // 預設要等待的回合數，若外部沒傳進來就用這個數字

    private YingGe owner;                                  // 記錄是哪一隻 YingGe 召喚了這顆石頭，用來回呼通知
    private BattleManager battleManager;                   // 戰鬥管理器的參考，用來判斷現在是不是敵人回合開始
    private int turnsRemaining;                            // 剩下還要等待幾個回合才算完成
    private bool notifiedOwnerOnDeath = false;             // 用來避免重複通知鷹鴿「我被打爆了」

    protected override void Awake()                        // 覆寫 Enemy 的 Awake，在物件生成時做初始化
    {
        enemyName = "鶯歌石";                              // 設定這個敵人的名稱（顯示用）
        base.Awake();                                      // 呼叫父類別 Enemy 的 Awake，執行既有初始化流程
    }

    public void ConfigureFromOwner(                        // 由 YingGe 召喚時呼叫的設定函式，負責把位置、血量、等待回合數設定進來
        YingGe stoneOwner,                                 // 呼叫者（擁有者）是哪一隻 YingGe
        Vector2Int gridPos,                                // 棋盤上的格子位置
        Vector3 worldPos,                                  // 世界座標位置
        int waitTurns,                                     // 要等待的回合數（如果 <= 0 會改用預設值）
        int hp)                                            // 要設定的血量
    {
        owner = stoneOwner;                                // 記錄是哪一隻 YingGe 召喚了這顆石頭
        turnsRemaining = Mathf.Max(                        // 計算這顆石頭真正要等待的回合數
            0,                                             // 至少是 0
            waitTurns > 0 ? waitTurns : baseTurnsToWait);  // 如果外面有傳正數就用外面的，否則用內建的 baseTurnsToWait
        maxHP = Mathf.Max(1, hp);                          // 設定這顆石頭的最大血量，至少是 1
        currentHP = maxHP;                                 // 把目前血量補滿
        BaseAttackDamage = 0;                              // 再次保險：石頭不攻擊
        block = 0;                                         // 一開始沒有護甲
        transform.position = worldPos;                     // 把物件的世界座標放到指定位置
        gridPosition = gridPos;                            // 把棋盤格位置也同步到指定的格子
    }

    public void DetachOwner()                              // 當 YingGe 復活之後，會呼叫這個把石頭跟主人拆開
    {
        owner = null;                                      // 不再持有 YingGe 的參考，之後就不會再通知它
    }

    public override void EnemyAction(Player player)        // 覆寫敵人的行動邏輯
    {
        ProcessCrowdControl();                             // 這顆石頭本身沒有攻擊行為，只會處理凍結/暈眩的回合扣減
    }

    public override void ProcessTurnStart()                // 每回合開始時會被呼叫
    {
        base.ProcessTurnStart();                           // 先讓父類別做原本的回合開始處理（例如狀態更新）

        if (battleManager == null)                         // 如果還沒拿到 BattleManager
        {
            battleManager = FindObjectOfType<BattleManager>(); // 嘗試在場景中找一個 BattleManager
        }

        if (battleManager == null ||                       // 如果找不到管理器（就用舊行為），
            battleManager.IsProcessingEnemyTurnStart)       // 或者現在的確是在進行敵人回合開始
        {
            AdvanceCountdown();                            // 才會推進這顆石頭的回合倒數
        }
    }

    private bool ProcessCrowdControl()                     // 處理被冰凍或暈眩的情況，回傳這回合是否被控制
    {
        if (frozenTurns > 0)                               // 如果還有凍結回合
        {
            frozenTurns--;                                 // 扣掉一回合
            return true;                                   // 表示這回合被凍住了
        }

        if (buffs.stun > 0)                                // 如果還有暈眩回合
        {
            buffs.stun--;                                  // 扣掉一回合
            return true;                                   // 表示這回合被暈眩了
        }

        return false;                                      // 沒有被控場，這回合算是正常
    }

    protected internal override void Die()                          // 覆寫死亡流程
    {
        if (!notifiedOwnerOnDeath && owner != null)        // 如果還沒通知過，而且還有主人（YingGe）
        {
            notifiedOwnerOnDeath = true;                   // 標記已經通知過了，避免重複通知
            owner.OnStoneDestroyed(this);                  // 告訴 YingGe：這顆石頭被打爆了，復活失敗
        }

        base.Die();                                        // 呼叫父類別的死亡流程，做真正的死亡/回收
    }

    private void AdvanceCountdown()                        // 推進這顆石頭的回合倒數
    {
        if (turnsRemaining > 0)                            // 如果還有要等的回合
        {
            turnsRemaining--;                              // 就扣掉一回合
        }

        if (turnsRemaining <= 0 && owner != null)          // 如果已經等完了，而且還有主人
        {
            owner.HandleStoneReady(this);                  // 通知主人：我等完了，你可以復活了
        }
    }
}
