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
            
            Vector3 toTarget = (col.transform.position - root.position).normalized;
            if (Vector3.Dot(root.forward, toTarget) < minDotProduct) continue;

            var myTeam = transform.root.GetComponent<TeamMember>();
            if (myTeam != null && !myTeam.IsEnemy(col.gameObject))
            {
                continue;
            }
            IDamageable target = col.transform.root.GetComponent<IDamageable>();
            if (target == null) continue;

            // DamageMultiplier still applied here — it's about outgoing damage scaling
            // Charge awarding happens automatically via EnemyHealth.OnAnyEnemyDamaged
            int finalDamage = stats != null
                ? Mathf.RoundToInt(damage * stats.DamageMultiplier)
                : damage;

            target.TakeDamage(finalDamage);

            IMovementController targetController = col.transform.root.GetComponent<IMovementController>();
            if (targetController != null) targetController.Stun(stunDuration);
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
