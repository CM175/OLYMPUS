using UnityEngine;

public abstract class Ultimate : Ability
{
    [Header("Ultimate Settings")]
    public int chargeCost = 100;
    
    public PlayerStats playerStats;

    protected virtual void Start()
    {
        
        playerStats = transform.root.GetComponent<PlayerStats>();
    
        if (playerStats == null)
        {
            Debug.LogError($"Ultimate: PlayerStats not found on root of {gameObject.name}. " +
                "Ensure PlayerStats script is on the main Player object.");
        }
        
    }


    // Overriding the use condition to check for charge
    public bool IsReady(int currentCharge)
    {
        // Must be off cooldown AND have enough charge
        return !IsOnCooldown() && currentCharge >= chargeCost;
    }

    public override void Use()
    {
        if (!IsReady(playerStats.currentUltimateCharge))
            return;
        Activate();
        playerStats.ConsumeCharge(chargeCost);
    }

}