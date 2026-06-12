using UnityEngine;

public class HymnOfRadiance : Ability
{
    public float burstRadius = 8f;
    public int healAmount = 50;
    public int damageAmount = 20;

    protected override void Activate()
    {
        Vector3 origin = transform.root.position;
        Collider[] targets = Physics.OverlapSphere(origin, burstRadius);

        foreach (var target in targets)
        {
            if (target.transform.root == transform.root) continue;
            var myTeam = transform.root.GetComponent<TeamMember>();
            // Treat AI / Enemies
            if (myTeam != null && myTeam.IsEnemy(target.gameObject))
            {
                var dmg = target.GetComponent<IDamageable>();
                dmg?.TakeDamage(damageAmount);
                
                // Add minor knockback impulse using the AI routing definitions if present
                var navAI = target.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
                if (navAI != null && navAI.enabled)
                {
                    Vector3 pushDir = (target.transform.position - origin).normalized;
                    pushDir.y = 0;
                    navAI.Move(pushDir * 2.5f);
                }
            }
            // Treat Players / Friendly Targets
            else if (myTeam != null && !myTeam.IsEnemy(target.gameObject))
            {
                var pHealth = target.GetComponent<IDamageable>();
                if (pHealth != null)
                {
                    pHealth.ApplyHeal(healAmount);
                }
            }
        }

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, origin, Quaternion.identity);
        }

        Debug.Log("[HymnOfRadiance] Concordant melody resonated.");
        StartCooldown();
    }
}