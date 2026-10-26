using UnityEngine;

public class TridentProjectile : BaseProjectile
{
    [Header("Charge Scale Bounds")]
    public float minSpeed = 30f;
    public float maxSpeed = 70f;
    public int minDamage = 30;
    public int maxDamage = 75;
    public float knockbackImpulse = 12f;


    public void SetCharge(float ratio)
    {
        speed = Mathf.Lerp(minSpeed, maxSpeed, ratio);
        damage = Mathf.RoundToInt(Mathf.Lerp(minDamage, maxDamage, ratio));
        gravityScale = Mathf.Lerp(0.8f, 0.1f, ratio);

        
        if (rb == null) rb = GetComponent<Rigidbody>();
        
        rb.useGravity = gravityScale > 0;
        rb.linearVelocity = transform.forward * speed; 
    }

    protected override void InitializeMovement() 
    { 
    }

    protected override void OnImpact(Collider other)
    {
        base.OnImpact(other);

        Transform enemyRoot = other.transform.root;
        var myTeam = attackerRoot != null ? attackerRoot.GetComponent<TeamMember>() : null;
        if (myTeam != null && !myTeam.IsEnemy(enemyRoot.gameObject)) return;

        IMovementController targetController = enemyRoot.GetComponent<IMovementController>();
        if (targetController != null)
        {
            Vector3 pushDir = transform.forward;
            pushDir.y = 0.1f;
            targetController.ApplyImpulseForce(pushDir.normalized * knockbackImpulse);
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