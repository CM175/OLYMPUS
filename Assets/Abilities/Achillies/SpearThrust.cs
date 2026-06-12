using UnityEngine;

public class SpearThrust : Attack
{
    [Header("Spear Thrust Settings")]
    public float thrustRadius = 1.5f; // How wide the hit detection is
    protected override void Activate()
    {
        // 1. Get the player's root and stats for damage calculation
        Transform root = transform.root;
        PlayerStats stats = root.GetComponent<PlayerStats>();

        // 2. Define the attack origin (usually the camera's position/direction)
        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = playerCamera.transform.forward;

        // 3. Use SphereCast to detect enemies in a line (gives the spear some "thickness")
        if (Physics.SphereCast(origin, thrustRadius, direction, out RaycastHit hit, range))
        {
            var myTeam = transform.root.GetComponent<TeamMember>();
            if (myTeam != null && !myTeam.IsEnemy(hit.collider.gameObject))
            {
                return;
            }
            
            IDamageable target = hit.collider.GetComponent<IDamageable>();
            
            if (target != null)
            {
                // Apply damage multiplier if it exists in PlayerStats
                int finalDamage = stats != null 
                    ? Mathf.RoundToInt(damage * stats.DamageMultiplier) 
                    : damage;

                target.TakeDamage(finalDamage);
                
                Debug.Log($"[SpearThrust] Hit {hit.collider.name} for {finalDamage} damage.");
            }
        }

        // 4. Optional: Instantiate a thrust VFX if one is assigned in the base class
        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, origin + (direction * 1.5f), Quaternion.LookRotation(direction));
        }
    }

    // Visualizes the thrust range in the Unity Editor
    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;
        Gizmos.color = Color.cyan;
        Vector3 endPoint = playerCamera.transform.position + (playerCamera.transform.forward * range);
        Gizmos.DrawLine(playerCamera.transform.position, endPoint);
        Gizmos.DrawWireSphere(endPoint, thrustRadius);
    }
}