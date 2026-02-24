using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime battle references shared by combat subsystems.
/// Keeps hot paths away from scene-wide Find calls.
/// </summary>
public sealed class BattleRuntimeContext
{
    public static BattleRuntimeContext Active { get; private set; }

    public BattleManager Manager { get; }
    public Player Player { get; }
    public Board Board { get; }
    public IReadOnlyList<Enemy> Enemies => enemies;

    private readonly List<Enemy> enemies;

    public BattleRuntimeContext(BattleManager manager, Player player, Board board, List<Enemy> enemies)
    {
        Manager = manager;
        Player = player;
        Board = board;
        this.enemies = enemies ?? new List<Enemy>();
    }

    public void Activate()
    {
        Active = this;
    }

    public void DeactivateIfOwner(BattleManager owner)
    {
        if (Active != null && Active.Manager == owner)
        {
            Active = null;
        }
    }

    public bool IsAliveEnemy(Enemy enemy)
    {
        return enemy != null && enemy.currentHP > 0 && !enemy.IsDead;
    }
}

