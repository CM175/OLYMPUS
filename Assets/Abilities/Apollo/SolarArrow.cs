using UnityEngine;

public class SolarArrow : BaseProjectile
{
    [Header("Dynamic Scaling Bounds")]
    public float minSpeed = 25f;
    public float maxSpeed = 80f;
    public int minDamageOrHealing = 15;
    public int maxDamageOrHealing = 60;

    [Header("Utility Settings")]
    public bool isHealingArrow = false;

    public void InitializeChargedProjectile(float chargeRatio)
    {
        speed = Mathf.Lerp(minSpeed, maxSpeed, chargeRatio);
        damage = Mathf.RoundToInt(Mathf.Lerp(minDamageOrHealing, maxDamageOrHealing, chargeRatio));
        gravityScale = Mathf.Lerp(1.0f, 0.1f, chargeRatio);

        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.useGravity = gravityScale > 0;
        rb.linearVelocity = transform.forward * speed;
    }

    protected override void InitializeMovement()
    {
        // Wait gracefully until InitializeChargedProjectile is triggered by the Bow weapon context
    }

    protected override void OnImpact(Collider other)
    {
        SpawnImpactVisuals(other);

        if (isHealingArrow)
        {
            // Custom Support Interface Logic
            var health = other.GetComponent<PlayerHealth>();
            if (health != null)
            {
                // Inverting the damage framework natively into a healing addition safely
                health.currentHealth = Mathf.Min(health.maxHealth, health.currentHealth + damage);
                Debug.Log($"[SolarArrow] Healed teammate {other.name} for {damage} points.");
            }
        }
        else
        {
            ApplyDamage(other);
        }

        if (!piercesTargets)
        {
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
        }
    }
}