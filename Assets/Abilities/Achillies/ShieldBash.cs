using UnityEngine;

public class ShieldBash : Ability
{
    [Header("Stats")]
    public float bashRange = 4f;
    public int damage = 20;
    public float stunDuration = 1.5f;

    [Header("Cone")]
    [Tooltip("Dot-product threshold. 0.5 ≈ 60° half-angle cone in front of the player.")]
    public float minDotProduct = 0.5f;

    protected override void Activate()
    {
        PlayerStats stats = transform.root.GetComponent<PlayerStats>();
        Transform root = transform.root;

        Collider[] colliders = Physics.OverlapSphere(root.position, bashRange);

        foreach (var col in colliders)
        {
            // Only hit things inside the frontal cone
            Vector3 toTarget = (col.transform.position - root.position).normalized;
            if (Vector3.Dot(root.forward, toTarget) < minDotProduct)
                continue;

            EnemyHealth enemy = col.GetComponent<EnemyHealth>();
            if (enemy == null) continue;

            int finalDamage = stats != null
                ? Mathf.RoundToInt(damage * stats.DamageMultiplier)
                : damage;

            enemy.TakeDamage(finalDamage, gameObject);

            // Stun the AI so it stops chasing during the stagger window
            EnemyAI ai = col.GetComponentInParent<EnemyAI>();
            if (ai != null)
                ai.Stun(stunDuration);
        }

        StartCooldown();
    }

    // Editor gizmo so you can see the bash range and cone in scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.root.position, bashRange);
    }
}
