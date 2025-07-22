using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BattleState
{
    protected BattleManager manager;
    public BattleState(BattleManager m)
    {
        manager = m;
    }
    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update() { }
}

public class PlayerTurnState : BattleState
{
    public PlayerTurnState(BattleManager m) : base(m) { }
    public override void Enter()
    {
        manager.StartPlayerTurn();
    }
    public override void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            manager.EndPlayerTurn();
        }
    }
}

public class EnemyTurnState : BattleState
{
    public EnemyTurnState(BattleManager m) : base(m) { }
    public override void Enter()
    {
        manager.StartCoroutine(manager.EnemyTurnCoroutine());
    }
}

public class VictoryState : BattleState
{
    public VictoryState(BattleManager m) : base(m) { }
    public override void Enter()
    {
        Debug.Log("Victory!");
    }
}

public class DefeatState : BattleState
{
    public DefeatState(BattleManager m) : base(m) { }
    public override void Enter()
    {
        Debug.Log("Defeat...");
    }
}

public class BattleStateMachine
{
    private BattleState current;
    public BattleState Current => current;
    public void ChangeState(BattleState next)
    {
        current?.Exit();
        current = next;
        current?.Enter();
    }
    public void Update()
    {
        current?.Update();
    }
}