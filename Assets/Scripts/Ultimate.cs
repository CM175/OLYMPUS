using UnityEngine;

public abstract class Ultimate : Ability
{
    [Header("Ultimate Settings")]
    public int chargeCost = 100;


    // Overriding the use condition to check for charge
    public bool IsReady(int currentCharge)
    {
        // Must be off cooldown AND have enough charge
        return !IsOnCooldown() && currentCharge >= chargeCost;
    }

    }