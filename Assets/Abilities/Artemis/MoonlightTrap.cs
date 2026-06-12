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
        // Check if we hit the ground/environment
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            ArmTrap();
        }
    }

    private void ArmTrap()
    {
        _isArmed = true;
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true; // Stop moving
        //Debug.Log("Moonlight Trap Armed and Waiting...");
    }

    protected override void Update()
    {
        if (!_isArmed)
        {
            base.Update(); // Use standard range-check while flying
            return;
        }

        // Manual check for enemies walking over the trap
        searchMask = LayerMask.GetMask("Character"); // Ensure this is set to the correct layers in the Inspector
        Collider[] victims = Physics.OverlapSphere(transform.position, triggerRadius, searchMask);
        if (victims.Length > 0)
        {
            TriggerTrap(victims[0]);
        }
    }

    private void TriggerTrap(Collider victim)
    {
        var myTeam = attackerRoot.GetComponent<TeamMember>();
                
        if (myTeam != null && !myTeam.IsEnemy(victim.gameObject))
        {
            return;
        }

        ApplyDamage(victim);
        
        // Custom logic: Stop the player from moving
        EnemyAI ea = victim.transform.root.GetComponent<EnemyAI>();
        PlayerController pc = victim.transform.root.GetComponent<PlayerController>();
        if (ea != null) ea.Stun(trappedDuration);
        if (pc != null) pc.Stun(trappedDuration);

        SpawnImpactVisuals(victim);
        Destroy(gameObject);
    }
}