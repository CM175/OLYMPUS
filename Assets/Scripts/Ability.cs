using UnityEngine;

public abstract class Ability : MonoBehaviour
{
    public float cooldown;
    public Camera playerCamera;
    
    [System.NonSerialized] public float lastUsedTime = -999f;
    [System.NonSerialized] public bool isExecuting = false;

    public GameObject vfxPrefab;
    public Sprite sprite;

    // --- OPTIMIZATION: EXCLUSIVE STATE LOCKING ---
    [Header("State Control")]
    [Tooltip("If true, no other abilities can be activated while this ability is running.")]
    public bool exclusiveState = false;

    [System.NonSerialized] protected IMovementController movementController;
    // Keep track of the manager to query other active abilities on the character
    [System.NonSerialized] protected AbilityManager abilityManager;

    public virtual void OnPress() {}
    public virtual void OnRelease() {}
    
    public void Use()
    {
        if (!CanUse()) return;
        isExecuting = true;
        Activate();
    }

    protected virtual bool CanUse()
    {
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
}