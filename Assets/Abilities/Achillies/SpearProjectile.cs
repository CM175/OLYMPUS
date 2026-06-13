using UnityEngine;

public class SpearProjectile : BaseProjectile
{
    // Override start to ensure we don't fire movement until instructed or ready
    protected override void Start()
    {
        base.Start();
        InitializeMovement();
    }

    protected override void InitializeMovement()
    {
        if (rb != null)
        {
            rb.useGravity = gravityScale > 0;
            // Set velocity toward the forward direction of the projectile
            rb.linearVelocity = transform.forward * speed;
        }
    }

    protected override void OnImpact(Collider other)
    {
        if (attackerRoot != null)
        {
            var myTeam = attackerRoot.GetComponent<TeamMember>();
            if (myTeam != null && !myTeam.IsEnemy(other.gameObject))
            {
                return; 
            }
        }
        base.OnImpact(other);
        // Check if we hit a damageable target
        IMovementController targetController = other.transform.root.GetComponent<IMovementController>();
        
        targetController?.Stun(0.5f);
        targetController?.ApplyImpulseForce(transform.forward*50f);        
    }

    private void FixedUpdate()
    {
        // Make the arrow "swivel" to face its travel direction
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
        }
    }
}