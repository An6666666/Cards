using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerBuffController))]
public class PlayerStats : MonoBehaviour
{
    [Header("基本屬性")]
    public int maxHP = 50;
    public int currentHP;
    public int maxEnergy = 4;
    public int energy;
    public int block = 0;
    public int gold = 0;

    private PlayerBuffController buffController;
    private Player owner;

    private void RaiseEnergyChanged()
    {
        UIEventBus.RaiseEnergyState(new EnergySnapshot(energy, maxEnergy));
    }

    public void Initialize(Player player, PlayerBuffController buffs)
    {
        owner = player;
        buffController = buffs;
        currentHP = maxHP;
        energy = maxEnergy;

        RaiseEnergyChanged();
    }

    public void StartTurn()
    {
        energy = maxEnergy;

        RaiseEnergyChanged();
    }

    public void UseEnergy(int cost)
    {
        Debug.Log($"UseEnergy: deducting {cost} energy. Energy before={energy}");
        energy -= cost;
        if (energy < 0) energy = 0;

        RaiseEnergyChanged();
    }

    public int CalculateAttackDamage(int baseDamage)
    {
        int dmg = baseDamage + buffController.nextAttackPlus + buffController.nextTurnAllAttackPlus;
        if (dmg < 0) dmg = 0;
        buffController.nextAttackPlus = 0;
        return dmg;
    }

    public void AddBlock(int amount, List<CardBase> relics)
    {
        block += amount;
        foreach (CardBase r in relics)
        {
            if (r is Relic_ZiDianJiao z)
            {
                z.OnAddBlock(owner, amount);
            }
        }
    }

    public void TakeDamage(int dmg)
    {
        int incoming = dmg;
        if (buffController.weak > 0) incoming += 2;

        int reduced = incoming - buffController.meleeDamageReduce;
        if (reduced < 0) reduced = 0;

        float realDmgF = reduced * buffController.damageTakenRatio;
        int realDmg = Mathf.CeilToInt(realDmgF);

        int remain = realDmg - block;
        if (remain > 0)
        {
            block = 0;
            currentHP -= remain;

            HandleFatalDamage();

            //只要有扣到 HP（remain>0）就播受傷
            owner?.PlayWoundedAnim();
        }
        else
        {
            block -= realDmg;
        }
    }


    public void TakeDamageDirect(int dmg)
    {
        int incoming = dmg;
        if (buffController.weak > 0)
        {
            incoming += 2;
        }

        currentHP -= incoming;
        HandleFatalDamage();
    }

    public void TakeStatusDamage(int dmg)
    {
        if (dmg <= 0)
        {
            return;
        }

        int remaining = dmg;

        if (block > 0)
        {
            if (block >= remaining)
            {
                block -= remaining;
                remaining = 0;
            }
            else
            {
                remaining -= block;
                block = 0;
            }
        }

        if (remaining > 0)
        {
            currentHP -= remaining;
            HandleFatalDamage();
        }
    }

    public void AddGold(int amount)
    {
        gold += amount;
    }
    private void HandleFatalDamage()
    {
        if (currentHP > 0)
        {
            return;
        }

        if (buffController != null && buffController.TryConsumeGuardianSpiritCharge(owner))
        {
            return;
        }

        if (currentHP < 0)
        {
            currentHP = 0;
        }
    }
}