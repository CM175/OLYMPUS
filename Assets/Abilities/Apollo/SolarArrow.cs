using UnityEngine;

public class SolarArrow : BaseProjectile
{
    [Header("Dynamic Scaling Bounds")]
    public float minSpeed = 25f;
    public float maxSpeed = 80f;
    public int minDamageOrHealing = 15;
    public int maxDamageOrHealing = 60;
    public float damageOverTimeDuration = 0.5f;

    public void SetCharge(float chargeRatio)
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
        if (other.transform.root.gameObject == attackerRoot) return; 

        _hasImpacted = true;
        SpawnImpactVisuals(other);
        
        var myTeam = attackerRoot.GetComponent<TeamMember>();
        // Custom Support Interface Logic
        
        if (myTeam != null)
        {
            
            IDamageable health = other.GetComponent<IDamageable>();
            if (!myTeam.IsEnemy(other.transform.root.gameObject))
            {
            
                health?.ApplyHeal(damage);
            }
            else
            {
                
                DamageDealer.DealOverTime(attackerRoot, health, damage, damageOverTimeDuration); 
            }
        }

        if (!piercesTargets)
        {
            Destroy(gameObject);
        }
        else
        {
            _hasImpacted = false;
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