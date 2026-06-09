using UnityEngine;

public class WingedDaggers : Attack
{
    [Header("Hermes Velocity Damage Settings")]
    [Tooltip("The baseline speed above which damage starts scaling up (e.g., Hermes' default walk speed).")]
    public float baseSpeedThreshold = 6f;
    
    [Tooltip("How aggressively the damage scales with extra velocity. Higher = more damage per unit of speed.")]
    public float velocityScaleFactor = 0.15f;

    protected override void Activate()
    {
        Transform root = transform.root;
        
        Vector3 attackPoint = root.position + root.forward * (range / 2f);
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint, range / 2f);

        foreach (var enemy in hitEnemies)
        {
            if (enemy.transform.root == root) continue;

            IDamageable target = enemy.GetComponent<IDamageable>();
            if (target != null)
            {
                ApplyDamage(target, enemy.transform);
            }
        }

        Debug.Log("[WingedDaggers] Slashed!");
    }

    private void ApplyDamage(IDamageable target, Transform enemyTransform)
    {
        Transform root = transform.root;
        PlayerStats stats = root.GetComponent<PlayerStats>();
        CharacterController cc = root.GetComponent<CharacterController>();
        
        int finalDamage = damage;

        // 1. Apply baseline system architecture modifier
        if (stats != null)
        {
            finalDamage = Mathf.RoundToInt(finalDamage * stats.DamageMultiplier);
        }

        // 2. HERMES ACTUAL VELOCITY MECHANIC
        if (cc != null)
        {
            // cc.velocity returns actual current units/second (3D vector)
            float actualSpeed = cc.velocity.magnitude;

            if (actualSpeed > baseSpeedThreshold)
            {
                float excessSpeed = actualSpeed - baseSpeedThreshold;
                
                // Linear scaling formula: Damage * (1 + (excessSpeed * scaleFactor))
                float damageMultiplier = 1f + (excessSpeed * velocityScaleFactor);
                
                finalDamage = Mathf.RoundToInt(finalDamage * damageMultiplier);
                
                Debug.Log($"[Hermes] Actual Speed: {actualSpeed:F2} m/s | velocity scale factor {velocityScaleFactor}  | Damage Scaled up to: {finalDamage}");
            }
            else
            {
                Debug.Log($"[Hermes] Base Speed: {actualSpeed:F2} m/s | Standard Damage: {finalDamage}");
            }
        }

        target.TakeDamage(finalDamage);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 attackPoint = transform.root.position + transform.root.forward * (range / 2f);
        Gizmos.DrawWireSphere(attackPoint, range / 2f);
    }
}