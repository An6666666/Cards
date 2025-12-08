using UnityEngine;

public class PlayerBuffController : MonoBehaviour
{
    public float damageTakenRatio = 1.0f;
    public int nextAttackPlus = 0;
    public int nextDamageTakenUp = 0;
    public int nextAttackCostModify = 0;
    public int movementCostModify = 0;
    public int nextTurnDrawChange = 0;
    public int needRandomDiscardAtEnd = 0;
    public int meleeDamageReduce = 0;
    public int weak = 0;
    public int bleed = 0;
    public int imprison = 0;
    public int nextTurnAllAttackPlus = 0;
    public bool drawBlockedThisTurn = false;

    [SerializeField, HideInInspector] private int weakFromEnemies = 0;
    [SerializeField, HideInInspector] private int bleedFromEnemies = 0;
    [SerializeField, HideInInspector] private int imprisonFromEnemies = 0;

    public void OnTurnStartReset()
    {
        movementCostModify = 0;
        nextAttackCostModify = 0;
        drawBlockedThisTurn = false;
    }

    public void OnTurnEndReset(Player owner)
    {
        damageTakenRatio = 1.0f;

        if (owner != null && bleed > 0)
        {
            owner.TakeStatusDamage(3);
        }

        if (weak > 0)
        {
            weak--;
            if (weakFromEnemies > 0)
            {
                weakFromEnemies--;
                if (weakFromEnemies > weak)
                {
                    weakFromEnemies = weak;
                }
            }
        }

        if (imprison > 0)
        {
            imprison--;
            if (imprisonFromEnemies > 0)
            {
                imprisonFromEnemies--;
                if (imprisonFromEnemies > imprison)
                {
                    imprisonFromEnemies = imprison;
                }
            }
        }

        if (bleed > 0)
        {
            bleed--;
            if (bleedFromEnemies > 0)
            {
                bleedFromEnemies--;
                if (bleedFromEnemies > bleed)
                {
                    bleedFromEnemies = bleed;
                }
            }
        }
        drawBlockedThisTurn = false;
    }

    public bool CanMove()
    {
        return imprison <= 0;
    }

    public void RemoveAllNegativeEffects(Player owner)
    {
        damageTakenRatio = Mathf.Min(damageTakenRatio, 1.0f);
        nextDamageTakenUp = 0;
        weak = 0;
        weakFromEnemies = 0;
        bleed = 0;
        bleedFromEnemies = 0;
        imprison = 0;
        imprisonFromEnemies = 0;
        needRandomDiscardAtEnd = 0;
        drawBlockedThisTurn = false;
    }

    public void RemoveEnemyNegativeEffects(Player owner)
    {
        if (weakFromEnemies > 0)
        {
            weak = Mathf.Max(0, weak - weakFromEnemies);
            weakFromEnemies = 0;
        }

        if (bleedFromEnemies > 0)
        {
            bleed = Mathf.Max(0, bleed - bleedFromEnemies);
            bleedFromEnemies = 0;
        }

        if (imprisonFromEnemies > 0)
        {
            imprison = Mathf.Max(0, imprison - imprisonFromEnemies);
            imprisonFromEnemies = 0;
        }
    }

    public void ApplyWeakFromEnemy(int duration)
    {
        duration = Mathf.Max(0, duration);
        weak = Mathf.Max(weak, duration);
        weakFromEnemies = Mathf.Max(weakFromEnemies, Mathf.Min(duration, weak));
    }

    public void IncreaseWeakFromPlayer(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        weak = Mathf.Max(0, weak + amount);
    }

    public void ApplyBleedFromEnemy(int duration)
    {
        duration = Mathf.Max(0, duration);
        bleed = Mathf.Max(bleed, duration);
        bleedFromEnemies = Mathf.Max(bleedFromEnemies, Mathf.Min(duration, bleed));
    }

    public void ApplyImprisonFromEnemy(int duration)
    {
        duration = Mathf.Max(0, duration);
        imprison = Mathf.Max(imprison, duration);
        imprisonFromEnemies = Mathf.Max(imprisonFromEnemies, Mathf.Min(duration, imprison));
    }

    public void ResetAll()
    {
        damageTakenRatio = 1.0f;
        nextAttackPlus = 0;
        nextDamageTakenUp = 0;
        nextAttackCostModify = 0;
        movementCostModify = 0;
        nextTurnDrawChange = 0;
        needRandomDiscardAtEnd = 0;
        meleeDamageReduce = 0;
        weak = 0;
        bleed = 0;
        imprison = 0;
        nextTurnAllAttackPlus = 0;
        drawBlockedThisTurn = false;
        weakFromEnemies = 0;
        bleedFromEnemies = 0;
        imprisonFromEnemies = 0;
    }
}
