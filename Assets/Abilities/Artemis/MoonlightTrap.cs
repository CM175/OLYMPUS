using UnityEngine;

public class MoonlightTrap : BaseProjectile
{
    [Header("Trap Settings")]
    public float triggerRadius = 2f;
    public float trappedDuration = 1.5f;
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
        Debug.Log("Moonlight Trap Armed and Waiting...");
    }

    protected override void Update()
    {
        if (!_isArmed)
        {
            base.Update(); // Use standard range-check while flying
            return;
        }

        // Manual check for enemies walking over the trap
        Collider[] victims = Physics.OverlapSphere(transform.position, triggerRadius);
        if (victims.Length > 0)
        {
            TriggerTrap(victims[0]);
        }
    }

    private void TriggerTrap(Collider victim)
    {
        var myTeam = transform.root.GetComponent<TeamMember>();
                
        if (myTeam != null && !myTeam.IsEnemy(victim.gameObject))
        {
            return;
        }

        ApplyDamage(victim);
        
        // Custom logic: Stop the player from moving
        EnemyAI ea = victim.GetComponent<EnemyAI>();
        if (ea != null) ea.Stun(trappedDuration);

        SpawnImpactVisuals(victim);
        Destroy(gameObject);
    }
}