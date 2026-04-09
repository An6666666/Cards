using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerBuffController))]
public class PlayerStats : MonoBehaviour
{
    [Header("Player Stats")]
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
        block = 0;

        RaiseEnergyChanged();
        NotifyHpChanged();
        NotifyBlockChanged();
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
        if (energy < 0)
        {
            energy = 0;
        }

        RaiseEnergyChanged();
    }

    public void GainEnergy(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0)
        {
            return;
        }

        energy += amount;
        RaiseEnergyChanged();
    }

    public int CalculateAttackDamage(int baseDamage)
    {
        int dmg = baseDamage + buffController.nextAttackPlus + buffController.nextTurnAllAttackPlus;
        if (dmg < 0)
        {
            dmg = 0;
        }

        buffController.nextAttackPlus = 0;
        return dmg;
    }

    public void AddBlock(int amount, List<RelicBase> relics)
    {
        block = Mathf.Max(0, block + amount);
        foreach (RelicBase relic in relics)
        {
            relic?.OnAddBlock(owner, amount);
        }

        NotifyBlockChanged();
    }

    public void TakeDamage(int dmg)
    {
        int previousBlock = block;
        int previousHp = currentHP;
        int incoming = dmg;
        if (buffController.weak > 0)
        {
            incoming += 2;
        }

        int reduced = incoming - buffController.meleeDamageReduce;
        if (reduced < 0)
        {
            reduced = 0;
        }

        float realDmgF = reduced * buffController.damageTakenRatio;
        int realDmg = Mathf.CeilToInt(realDmgF);
        int blockedDamage = Mathf.Min(previousBlock, realDmg);

        int remain = realDmg - block;
        if (remain > 0)
        {
            block = 0;
            currentHP -= remain;

            HandleFatalDamage();
        }
        else
        {
            block -= realDmg;
        }

        int actualHpDamage = GetActualHpDamage(previousHp);
        NotifyActualHpDamage(actualHpDamage);
        NotifyShieldDamageFeedback(previousBlock, blockedDamage, actualHpDamage);
        NotifyBlockChanged();
    }

    public void TakeDamageDirect(int dmg)
    {
        int previousHp = currentHP;
        int incoming = dmg;
        if (buffController.weak > 0)
        {
            incoming += 2;
        }

        currentHP -= incoming;
        HandleFatalDamage();
        NotifyActualHpDamage(GetActualHpDamage(previousHp));
    }

    public void TakeStatusDamage(int dmg)
    {
        if (dmg <= 0)
        {
            return;
        }

        int previousBlock = block;
        int previousHp = currentHP;
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

        int actualHpDamage = GetActualHpDamage(previousHp);
        NotifyActualHpDamage(actualHpDamage);
        int blockedDamage = Mathf.Min(previousBlock, dmg);
        NotifyShieldDamageFeedback(previousBlock, blockedDamage, actualHpDamage);
        NotifyBlockChanged();
    }

    public void AddGold(int amount)
    {
        gold += amount;
    }

    public void SetBlock(int value)
    {
        int previousBlock = block;
        block = Mathf.Max(0, value);

        if (previousBlock > 0 && block <= 0)
        {
            owner?.PlayShieldBreakFX();
        }

        NotifyBlockChanged();
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

    private int GetActualHpDamage(int previousHp)
    {
        return Mathf.Max(0, previousHp - currentHP);
    }

    private void NotifyActualHpDamage(int actualHpDamage)
    {
        if (actualHpDamage <= 0)
        {
            return;
        }

        owner?.PlayWoundedAnim();
        owner?.PlayHitFeedback();
        owner?.PlayHurtSFX();
        NotifyHpChanged();
    }

    private void NotifyHpChanged()
    {
        owner?.RefreshLowHpFeedback();
    }

    private void NotifyBlockChanged()
    {
        owner?.SetShieldFXActive(block > 0);
    }

    private void NotifyShieldDamageFeedback(int previousBlock, int blockedDamage, int actualHpDamage)
    {
        if (previousBlock <= 0 || blockedDamage <= 0)
        {
            return;
        }

        if (actualHpDamage <= 0)
        {
            owner?.PlayShieldBlockNoDamageSFX();
        }

        if (block <= 0)
        {
            owner?.PlayShieldBreakFX();
            return;
        }

        owner?.PlayShieldHitFX();
    }
}
