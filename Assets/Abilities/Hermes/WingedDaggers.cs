using UnityEngine;

public class WingedDaggers : Ability
{
    [Header("Dagger Settings")]
    public int damage = 15;
    public float attackRange = 2.5f;
    public float attackRate = 0.5f; // Time between swings

    protected override void Activate()
    {
        // Reference the player root for position and orientation
        Transform root = transform.root;
        
        // Find potential targets in a small sphere at the dagger's reach
        Vector3 attackPoint = root.position + root.forward * (attackRange / 2f);
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint, attackRange / 2f);

        foreach (var enemy in hitEnemies)
        {
            // Don't hit yourself
            if (enemy.transform.root == root) continue;

            IDamageable target = enemy.GetComponent<IDamageable>();
            if (target != null)
            {
                ApplyDamage(target, enemy.transform);
            }
        }

        // Logic to trigger a "Swing" animation would go here
        Debug.Log("[WingedDaggers] Slashed!");

        // Start the short cooldown between attacks
        Invoke(nameof(StartCooldown), attackRate);
    }

    private void ApplyDamage(IDamageable target, Transform enemyTransform)
    {
        PlayerStats stats = transform.root.GetComponent<PlayerStats>();
        int finalDamage = damage;

        // Apply global damage multipliers from buffs like Ultimate or Passive
        if (stats != null)
        {
            finalDamage = Mathf.RoundToInt(damage * stats.DamageMultiplier);
        }

        target.TakeDamage(finalDamage);
    }

    // Visualization for the editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 attackPoint = transform.root.position + transform.root.forward * (attackRange / 2f);
        Gizmos.DrawWireSphere(attackPoint, attackRange / 2f);
    }
}