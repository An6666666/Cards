using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class HeiGouJing : Enemy
{
    [System.Serializable]
    private struct CloneSettings
    {
        [Min(0)] public int count;
        [Min(1)] public int maxHP;
        [Min(0)] public int baseAttackDamage;
    }

    [Header("黑狗精分身設定")]
    [SerializeField] private CloneSettings cloneSettings = new CloneSettings
    {
        count = 2,
        maxHP = 15,
        baseAttackDamage = 5
    };

    [Header("Animator 參考")]
    [SerializeField] private Animator bodyAnimator; // 指向真正播放 HeiGouJing_idle.controller 的 Animator

    private bool hasSplitTriggered = false;                 // 是否已觸發過分裂
    private bool isClone = false;                           // 是否為分身
    private HeiGouJing originMain = null;                   // 分身指向本體
    private List<HeiGouJing> spawnedClones = new List<HeiGouJing>(); // 本體生成的分身清單
    private bool hasBeenHitFlag = false;                    // 這個個體是否已完成「第一次受擊」狀態
    private bool needPostSpawnHit = false;                  // 分身出生後要在下一幀強制播受擊（蓋掉出場）

    private Coroutine markHitRoutine;

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        cloneSettings.count = Mathf.Max(0, cloneSettings.count);
        cloneSettings.maxHP = Mathf.Max(1, cloneSettings.maxHP);
        cloneSettings.baseAttackDamage = Mathf.Max(0, cloneSettings.baseAttackDamage);
    }
#endif

    protected override void Awake()
    {
        enemyName = "黑狗精";
        base.Awake();

        if (bodyAnimator == null) bodyAnimator = GetComponent<Animator>();
        if (bodyAnimator == null) bodyAnimator = GetComponentInChildren<Animator>(true);
    }

    private void Start()
    {
        // 只讓分身做一次「出生後強制受擊」
        if (!isClone) return;
        if (!needPostSpawnHit) return;

        needPostSpawnHit = false;
        StartCoroutine(PostSpawnForceFirstHit());
    }

    public override void TakeDamage(int dmg)
    {
        bool shouldSplit = ShouldTriggerSplit(dmg, false);
        int effectiveDamage = Mathf.Max(0, dmg - block);

        base.TakeDamage(dmg);

        if (effectiveDamage > 0 && currentHP > 0)
            PlayHitAnimAndMarkFirst();

        if (shouldSplit && currentHP > 0)
            TriggerSplit();
    }

    public override void TakeTrueDamage(int dmg)
    {
        bool shouldSplit = ShouldTriggerSplit(dmg, true);

        base.TakeTrueDamage(dmg);

        if (dmg > 0 && currentHP > 0)
            PlayHitAnimAndMarkFirst();

        if (shouldSplit && currentHP > 0)
            TriggerSplit();
    }

    private bool ShouldTriggerSplit(int dmg, bool isTrueDamage)
    {
        if (hasSplitTriggered || isClone || dmg <= 0)
            return false;

        int effectiveDamage = isTrueDamage ? dmg : Mathf.Max(0, dmg - block);
        if (effectiveDamage <= 0)
            return false;

        int remainingHP = currentHP - effectiveDamage;
        return remainingHP > 0;
    }

    private void TriggerSplit()
    {
        hasSplitTriggered = true;
        currentHP = maxHP;
        SpawnClonesAndTeleport();
    }

    private void SpawnClonesAndTeleport()
    {
        Board board = FindObjectOfType<Board>();
        BattleManager battleManager = FindObjectOfType<BattleManager>();
        if (board == null || battleManager == null) return;

        List<Vector2Int> availablePositions = board.GetAllPositions();

        Player player = FindObjectOfType<Player>();
        if (player != null)
            availablePositions.Remove(player.position);

        Enemy[] allEnemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in allEnemies)
        {
            if (enemy != null && enemy != this)
                availablePositions.Remove(enemy.gridPosition);
        }

        availablePositions.Remove(gridPosition);

        if (availablePositions.Count == 0) return;

        Vector3 originWorldPos = transform.position;

        Vector2Int newMainPos = PopRandomPosition(availablePositions);
        MoveToPosition(newMainPos);

        int clonesToSpawn = Mathf.Min(Mathf.Max(0, cloneSettings.count), availablePositions.Count);

        for (int i = 0; i < clonesToSpawn; i++)
        {
            Vector2Int clonePos = PopRandomPosition(availablePositions);
            BoardTile tile = board.GetTileAt(clonePos);
            if (tile == null) continue;

            HeiGouJing clone = Instantiate(this, originWorldPos, Quaternion.identity);
            InitializeClone(clone, clonePos, battleManager);

            clone.transform
                .DOMove(tile.transform.position, 0.25f)
                .SetEase(Ease.OutQuad);
        }
    }

    private void InitializeClone(HeiGouJing clone, Vector2Int clonePos, BattleManager battleManager)
    {
        clone.hasSplitTriggered = true;                          // 分身不可再次分裂
        clone.isClone = true;                                    // 標記為分身
        clone.originMain = this;                                 // 指向本體
        clone.enemyName = "黑狗精分身";

        clone.maxHP = Mathf.Max(1, cloneSettings.maxHP);
        clone.currentHP = clone.maxHP;
        clone.BaseAttackDamage = Mathf.Max(0, cloneSettings.baseAttackDamage);
        clone.block = 0;
        clone.gridPosition = clonePos;

        clone.spawnedClones = new List<HeiGouJing>();
        clone.SetHighlight(false);

        // 這裡是關鍵：分身是「新個體」，出生時強制當作「還沒受擊過」
        // 這樣出生那一下 Hit 才會走 once_wounded，而不是 twice_wounded
        clone.hasBeenHitFlag = false;

        // 確保 clone 抓到正確 Animator
        if (clone.bodyAnimator == null) clone.bodyAnimator = clone.GetComponentInChildren<Animator>(true);

        if (clone.bodyAnimator != null)
        {
            clone.bodyAnimator.SetBool("HasBeenHit", false);     // 出生先固定走 once 分支
            clone.bodyAnimator.ResetTrigger("Hit");
            clone.bodyAnimator.ResetTrigger("Appear");           // 避免出場搶狀態
        }

        // 註冊進 battleManager（讓分身進回合/AI/更新流程）
        if (battleManager != null && !battleManager.enemies.Contains(clone))
            battleManager.enemies.Add(clone);

        // 本體記錄分身，方便本體死亡時回收
        RegisterClone(clone);

        // 讓分身在 Start 的下一幀強制播一次受擊（蓋掉通用出場動畫）
        clone.needPostSpawnHit = true;
    }

    private IEnumerator PostSpawnForceFirstHit()
    {
        // 等一幀：讓 Enemy.Start / OnEnable 之類通用流程先跑完（通常會觸發 Appear）
        yield return null;

        if (!isClone) yield break;
        if (bodyAnimator == null) bodyAnimator = GetComponentInChildren<Animator>(true);
        if (bodyAnimator == null) yield break;

        // 分身出生目標：一定要播 once_wounded
        hasBeenHitFlag = false;
        bodyAnimator.SetBool("HasBeenHit", false);

        bodyAnimator.ResetTrigger("Appear");
        bodyAnimator.ResetTrigger("Hit");
        bodyAnimator.SetTrigger("Hit"); // Any State → once_wounded（Hit + HasBeenHit=false）

        // 再等一幀：讓受擊轉場吃到一次之後，把分身標記為已受擊，之後才會播 twice_wounded
        yield return null;

        hasBeenHitFlag = true;
        bodyAnimator.SetBool("HasBeenHit", true);
    }

    private void PlayHitAnimAndMarkFirst()
    {
        if (bodyAnimator == null) return;

        bool wasHitBefore = hasBeenHitFlag;

        bodyAnimator.SetBool("HasBeenHit", wasHitBefore);
        bodyAnimator.ResetTrigger("Hit");
        bodyAnimator.SetTrigger("Hit");

        // 第一次受擊：下一幀才把狀態改成「已受擊」，避免轉場判定混亂
        if (!wasHitBefore)
        {
            if (markHitRoutine != null) StopCoroutine(markHitRoutine);
            markHitRoutine = StartCoroutine(MarkHasBeenHitNextFrame());
        }
    }

    private IEnumerator MarkHasBeenHitNextFrame()
    {
        yield return null;

        hasBeenHitFlag = true;
        if (bodyAnimator != null)
            bodyAnimator.SetBool("HasBeenHit", true);
    }

    private static Vector2Int PopRandomPosition(List<Vector2Int> positions)
    {
        int idx = Random.Range(0, positions.Count);
        Vector2Int pos = positions[idx];
        positions.RemoveAt(idx);
        return pos;
    }

    private void RegisterClone(HeiGouJing clone)
    {
        if (!spawnedClones.Contains(clone))
            spawnedClones.Add(clone);
    }

    private void UnregisterClone(HeiGouJing clone)
    {
        spawnedClones.Remove(clone);
    }

    private void OnDestroy()
    {
        if (isClone)
        {
            if (originMain != null)
                originMain.UnregisterClone(this);
            return;
        }

        foreach (var clone in spawnedClones)
        {
            if (clone != null)
            {
                clone.originMain = null;
                Destroy(clone.gameObject);
            }
        }
        spawnedClones.Clear();
    }
}
