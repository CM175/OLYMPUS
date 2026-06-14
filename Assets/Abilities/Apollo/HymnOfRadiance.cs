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
            Transform targetRoot = target.transform.root;
            if (targetRoot == transform.root) continue;
            
            var myTeam = transform.root.GetComponent<TeamMember>();
            
            // Handle opposing targets
            if (myTeam != null && myTeam.IsEnemy(target.gameObject))
            {
                var dmg = targetRoot.GetComponent<IDamageable>();
                dmg?.TakeDamage(damageAmount);
                
                // OPTIMIZED: Use interface impulses instead of tight NavMeshAgent coupling
                IMovementController movement = targetRoot.GetComponent<IMovementController>();
                if (movement != null)
                {
                    Vector3 pushDir = (targetRoot.position - origin);//.normalized;
                    pushDir.y = 0;
                    movement.ApplyImpulseForce(pushDir * 150f); // Scale force up contextually for continuous calculations
                }
            }
            // Handle friendly targets
            else if (myTeam != null && !myTeam.IsEnemy(target.gameObject))
            {
                var pHealth = targetRoot.GetComponent<IDamageable>();
                pHealth?.ApplyHeal(healAmount);
            }
        }

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, origin, Quaternion.identity);
        }

        StartCooldown();
    }
}