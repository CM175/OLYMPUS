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
        // Check if we hit a damageable target
        IDamageable target = other.transform.root.GetComponent<IDamageable>();
        IMovementController targetController = other.transform.root.GetComponent<IMovementController>();
        
        target?.TakeDamage(damage);
        targetController?.Stun(0.5f);
        targetController?.ApplyImpulseForce(transform.forward*5f);
            


        // Logic for piercing or destroying is handled by base.OnImpact
        base.OnImpact(other);
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