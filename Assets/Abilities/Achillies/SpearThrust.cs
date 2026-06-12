using UnityEngine;

public class SpearThrust : Attack
{
    [Header("Spear Thrust Settings")]
    public float thrustRadius = 1.5f; 

    protected override void Activate()
    {
        Transform root = transform.root;
        PlayerStats stats = root.GetComponent<PlayerStats>();
        IMovementController movement = root.GetComponent<IMovementController>();

        // OPTIMIZED: Safe origin/direction calculation fallback context for AI scripts
        Vector3 origin = (playerCamera != null) ? playerCamera.transform.position : root.position + Vector3.up;
        Vector3 direction = (movement != null) ? movement.GetAimDirection() : root.forward;

        if (Physics.SphereCast(origin, thrustRadius, direction, out RaycastHit hit, range))
        {
            Transform targetRoot = hit.collider.transform.root;
            var myTeam = root.GetComponent<TeamMember>();
            
            if (myTeam != null && !myTeam.IsEnemy(targetRoot.gameObject))
            {
                return;
            }
            
            IDamageable target = hit.collider.GetComponent<IDamageable>() ?? targetRoot.GetComponent<IDamageable>();
            
            if (target != null)
            {
                int finalDamage = stats != null 
                    ? Mathf.RoundToInt(damage * stats.DamageMultiplier) 
                    : damage;

                target.TakeDamage(finalDamage);
                Debug.Log($"[SpearThrust] Hit {hit.collider.name} for {finalDamage} damage.");
            }
        }

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, origin + (direction * 1.5f), Quaternion.LookRotation(direction));
        }
    }
}