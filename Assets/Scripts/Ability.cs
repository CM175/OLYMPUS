using UnityEngine;

// How the RL agent judges an ability. Existing prefabs default to Damage, so behaviour is unchanged until you set a role.
public enum AbilityRole { Damage, Mobility, Defensive, Buff, Utility }

public abstract class Ability : MonoBehaviour
{
    [Header("RL")]
    [Tooltip("Damage = paid for damage dealt, punished if it hits nothing. Defensive = paid for damage it mitigates. Buff/Utility = never punished for dealing no damage.")]
    public AbilityRole role = AbilityRole.Damage;

    public float cooldown;
    public Camera playerCamera;
    public bool CanBeCancelled = false;
    
    [System.NonSerialized] public float lastUsedTime = -999f;
    [System.NonSerialized] public bool isExecuting = false;
    public bool isChargeable = false; // Added here

    public GameObject vfxPrefab;
    public Sprite sprite;

    // --- OPTIMIZATION: EXCLUSIVE STATE LOCKING ---
    [Header("State Control")]
    [Tooltip("If true, no other abilities can be activated while this ability is running.")]
    public bool exclusiveState = false;

    [System.NonSerialized] protected IMovementController movementController;
    // Keep track of the manager to query other active abilities on the character
    [System.NonSerialized] protected AbilityManager abilityManager;

    protected GameObject Owner => transform.root.gameObject;

    // Outgoing damage always goes through these so the attacker is explicit
    // and PlayerStats.DamageMultiplier is applied exactly once.
    // The ability currently dealing damage. Damage events fire synchronously inside Deal(), so listeners
    // (e.g. the RL agent) can read this to know which ability caused a hit. Null = unknown / not via an ability.
    public static Ability CurrentDealer { get; private set; }

    protected void Deal(IDamageable target, int baseDamage)
    {
        Ability previous = CurrentDealer;
        CurrentDealer = this;
        try { DamageDealer.Deal(Owner, target, baseDamage); }
        finally { CurrentDealer = previous; }
    }

    protected void DealOverTime(IDamageable target, int baseTotalDamage, float duration)
    {
        Ability previous = CurrentDealer;
        CurrentDealer = this;
        try { DamageDealer.DealOverTime(Owner, target, baseTotalDamage, duration); }
        finally { CurrentDealer = previous; }
    }

    public virtual void OnPress() {}
    public virtual void OnRelease() {}
    
    public void Use()
    {
        if (!CanUse()) return;
        isExecuting = true;

        // Automatically set the ambient context to this character root
        using (new DamageContext(transform.root.gameObject))
        {
            Activate();
        }
    }

    protected virtual bool CanUse()
    {
        
        if (isExecuting && CanBeCancelled)
        {
            Cancel();
            return false;
        }
        // 1. Gate execution if the character is stunned
        if (movementController != null && movementController.IsStunned) 
            return false;

        // 2. Gate execution if ANOTHER ability has claimed an exclusive action lock
        if (abilityManager != null && abilityManager.HasActiveExclusiveAbility())
        {
            return false;
        }


        return !IsOnCooldown() && !isExecuting;
    }

    protected abstract void Activate();
    
    public bool IsOnCooldown()
    {
        return Time.time < lastUsedTime + cooldown;
    }

    public void StartCooldown()
    {
        lastUsedTime = Time.time;
        isExecuting = false;
    }

    public void SetMovementController(IMovementController controller)
    {
        movementController = controller;
    }

    // Assign the manager context during initialization setup
    public void SetAbilityManager(AbilityManager manager)
    {
        abilityManager = manager;
    }

    public virtual void Cancel()
    {
        if (isExecuting)
        {
            isExecuting = false;
            StopAllCoroutines();
            StartCooldown();
        }

        if (movementController != null)
        {
            movementController.IsDashing = false;
            movementController.HaltVelocity();
        }
    }
}