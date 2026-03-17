using UnityEngine;

public abstract class Ultimate : Ability
{
    [Header("Ultimate Settings")]
    public int cost = 100;
    
    public PlayerStats playerStats;

    protected virtual void Start()
    {
        
        playerStats = GetComponentInParent<PlayerStats>();

        // If it's still null, it might not be parented yet. 
        // We can try to find the local one or wait.
        if (playerStats == null) return; 

        playerStats.maxUltimateCharge = cost;
        
            
        
    }

    // Overriding the use condition to check for charge
    public bool IsReady(float currentCharge)
    {
        // Must be off cooldown AND have enough charge
        return !IsOnCooldown() && currentCharge >= cost;
    }

    public override void Use()
    {
        if (!IsReady(playerStats.currentUltimateCharge)) return;
        isExecuting = true;
        Activate();
        playerStats.ConsumeCharge(cost);
    }

}