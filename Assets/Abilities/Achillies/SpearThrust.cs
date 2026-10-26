using UnityEngine;

public class SpearThrust : Attack
{
    [Header("Spear Thrust Settings")]
    public float thrustRadius = 1.5f; 

    protected override void Activate()
    {
        
        Debug.Log("thrusted");
        Transform root = transform.root;
        IMovementController movement = root.GetComponent<IMovementController>();

        // OPTIMIZED: Safe origin/direction calculation fallback context for AI scripts
        Vector3 origin = (playerCamera != null) ? playerCamera.transform.position : root.position + Vector3.up;
        Vector3 direction = (movement != null) ? movement.GetAimDirection() : root.forward;

        if (Physics.SphereCast(origin, thrustRadius, direction, out RaycastHit hit, range))
        {
            
            Transform targetRoot = hit.collider.transform.root;
            Debug.Log($"[SpearThrust] Hit {hit.collider.name} at {hit.point} with normal {hit.normal}. Target root: {targetRoot.name}");
            var myTeam = root.GetComponent<TeamMember>();
            
            if (myTeam != null && !myTeam.IsEnemy(targetRoot.gameObject))
            {
                return;
            }
            
            IDamageable target = hit.collider.GetComponent<IDamageable>() ?? targetRoot.GetComponent<IDamageable>();
            
            if (target != null)
            {
                Deal(target, damage);
                //Debug.Log($"[SpearThrust] Hit {hit.collider.name} for {finalDamage} damage.");
            }
        }

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, origin + (direction * 1.5f), Quaternion.LookRotation(direction));
        }
    }
}