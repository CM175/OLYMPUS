using UnityEngine;

public class StormArrow : BaseProjectile
{
    [Header("Bow Specifics")]
    public float maxSpeed = 100f;
    public float minSpeed = 20f;
    public int maxDamage = 125;
    public int minDamage = 25;
    
    private Rigidbody rb;

    // Call this from your Bow script right after instantiating the arrow
    public void SetCharge(float chargeRatio)
    {
        // Linearly interpolate speed and damage based on how long the string was held
        speed = Mathf.Lerp(minSpeed, maxSpeed, chargeRatio);
        damage = Mathf.RoundToInt(Mathf.Lerp(minDamage, maxDamage, chargeRatio));
        
        // Hanzo's arrows typically have less gravity the harder they are fired
        gravityScale = Mathf.Lerp(2.0f, 0.2f, chargeRatio); 
    }

    protected override void InitializeMovement()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = gravityScale > 0;
        rb.linearVelocity = transform.forward * speed;
    }

    void FixedUpdate()
    {
        // "Arrow Swiveling": Makes the arrow point in the direction it's actually falling
        if (rb != null && rb.linearVelocity != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
        }
    }

    protected override void OnTriggerEnter(Collider other)
    {

        // Hanzo-specific: Logic for Headshots
        bool isHeadshot = CheckForHeadshot(other);
        if (isHeadshot)
        {
            damage *= 2; 
            Debug.Log("Critical Hit!");
        }

        base.OnTriggerEnter(other);
    }

    private bool CheckForHeadshot(Collider other)
    {
        // Simple check: does the collider have a specific tag or layer?
        return other.CompareTag("HeadshotBox");
    }
}