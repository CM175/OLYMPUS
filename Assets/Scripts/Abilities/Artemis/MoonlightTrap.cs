using UnityEngine;

public class MoonlightTrap : BaseProjectile
{
    [Header("Trap Settings")]
    public float triggerRadius = 2f;
    public float trappedDuration = 1.5f;

    public LayerMask searchMask; 
    private bool _isArmed = false;

    protected override void Start()
    {
        base.Start();
        InitializeMovement();
    }

    protected override void OnImpact(Collider other)
    {
        // Check if we hit the ground/environment to set up the trap trap layout
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            ArmTrap();
        }
    }

    private void ArmTrap()
    {
        _isArmed = true;
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true; // Freeze physics translation safely
    }

    protected override void Update()
    {
        if (!_isArmed)
        {
            base.Update(); // Fallback to standard range checks while flying airborne
            return;
        }

        // Search for target elements walking across the trigger volume
        searchMask = LayerMask.GetMask("Character"); 
        Collider[] victims = Physics.OverlapSphere(transform.position, triggerRadius, searchMask);
        if (victims.Length > 0)
        {
            TriggerTrap(victims[0]);
        }
    }

    private void TriggerTrap(Collider victim)
    {
        Transform victimRoot = victim.transform.root;
        var myTeam = attackerRoot.GetComponent<TeamMember>();
                
        if (myTeam != null && !myTeam.IsEnemy(victimRoot.gameObject))
        {
            return;
        }

        ApplyDamage(victim);
        
        // OPTIMIZED: Replaced concrete EnemyAI and PlayerController lookups with our unified interface contract hook
        IMovementController movement = victimRoot.GetComponent<IMovementController>();
        if (movement != null)
        {
            movement.Stun(trappedDuration);
        }

        SpawnImpactVisuals(victim);
        Destroy(gameObject);
    }
}