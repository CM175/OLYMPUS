using UnityEngine;

public class StormArrow : BaseProjectile
{
    public float minSpeed = 20f;
    
    public float maxSpeed = 100f;
    
    public int minDamage= 25;
    
    public int maxDamage = 125;
    
    public void SetCharge(float chargeRatio)
    {
        // Scale stats based on bow charge
        damage = Mathf.RoundToInt(Mathf.Lerp(minDamage, maxDamage, chargeRatio));
        speed = Mathf.Lerp(minSpeed, maxSpeed, chargeRatio);
        gravityScale = Mathf.Lerp(1.5f, 0.2f, chargeRatio);

            if (rb == null) rb = GetComponent<Rigidbody>();
        
        rb.useGravity = gravityScale > 0;
        rb.linearVelocity = transform.forward * speed; 
    }
    

    protected override void InitializeMovement()
    {
        // Do nothing here; wait for SetCharge to be called by the Bow
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