using UnityEngine;

public class HymnOfRadiance : Ability
{
    public float burstRadius = 8f;
    public int healAmount = 50;
    public int damageAmount = 20;
    public float healOrDamageDuration =1.5f; // Duration of the healing effect
    public float pushForce = 10f;

    protected override void Activate()
    {
        //Debug.Log($"Activating hymn of radiance");
        Vector3 origin = transform.root.position;
        Collider[] targets = Physics.OverlapSphere(origin, burstRadius);

        foreach (var target in targets)
        {
            Transform targetRoot = target.transform.root;
            
            var myTeam = transform.root.GetComponent<TeamMember>();
            
            // Handle opposing targets
            if (myTeam != null && myTeam.IsEnemy(target.gameObject))
            {
                var dmg = targetRoot.GetComponent<IDamageable>();
                DealOverTime(dmg, damageAmount, healOrDamageDuration);
                
                // OPTIMIZED: Use interface impulses instead of tight NavMeshAgent coupling
                IMovementController movement = targetRoot.GetComponent<IMovementController>();
                if (movement != null)
                {
                    Vector3 pushDir = (targetRoot.position - origin);//.normalized;
                    pushDir.y = 0;
                    movement.ApplyImpulseForce(pushDir * pushForce); 
                }
            }
            // Handle friendly targets
            else if (myTeam != null && !myTeam.IsEnemy(target.gameObject))
            {
                var pHealth = targetRoot.GetComponent<IDamageable>();
                pHealth?.HealOverTime(healAmount, healOrDamageDuration);
            }
        }

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, origin, Quaternion.identity);
        }

        StartCooldown();
    }
}