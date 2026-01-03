using UnityEngine;
using UnityEngine.UI;

public class RunHUD_Text : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text hpText;
    [SerializeField] private Text goldText;

    [Header("Options")]
    [SerializeField] private bool showHpAsCurrentSlashMax = true;

    private Player player;
    private RunManager runManager;

    private void Awake()
    {
        runManager = FindObjectOfType<RunManager>();
        player = FindObjectOfType<Player>();
        Refresh();
    }

    private void Update()
    {
        if (runManager == null) runManager = FindObjectOfType<RunManager>();
        if (player == null) player = FindObjectOfType<Player>();

        Refresh();
    }

    private void Refresh()
    {
        // 1) 有 Player 就用 Player（戰鬥場景等）
        if (player != null)
        {
            SetHP(player.currentHP, player.maxHP);
            SetGold(player.gold);
            return;
        }

        // 2) RunScene 沒 Player → 用 RunManager 的 snapshot
        if (runManager == null) return;

        var snap = runManager.CurrentRunSnapshot;
        if (snap == null) return;

        SetHP(snap.currentHP, snap.maxHP);
        SetGold(snap.gold);
    }

    private void SetHP(int current, int max)
    {
        if (hpText == null) return;
        hpText.text = showHpAsCurrentSlashMax ? $"{current}/{max}" : $"HP {current}";
    }

    private void SetGold(int gold)
    {
        if (goldText == null) return;
        goldText.text = $"{gold}";
    }
}
