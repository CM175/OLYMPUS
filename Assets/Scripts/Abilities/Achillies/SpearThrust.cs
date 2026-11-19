using UnityEngine;

public class SpearThrust : Attack
{
    [Header("Spear Thrust Settings")]
    public float thrustRadius = 1.5f;

    protected override void Activate()
    {
        Transform root = transform.root;
        IMovementController movement = root.GetComponent<IMovementController>();

        Vector3 origin = (playerCamera != null) ? playerCamera.transform.position : root.position + Vector3.up * 1.5f;
        Vector3 dir = (playerCamera != null) ? playerCamera.transform.forward : (movement != null ? movement.GetAimDirection() : root.forward);

        // Define the two sphere centers of the capsule along the thrust vector
        Vector3 point1 = origin + dir * thrustRadius;
        Vector3 point2 = origin + dir * Mathf.Max(thrustRadius, range - thrustRadius);

        Collider[] hits = Physics.OverlapCapsule(point1, point2, thrustRadius, AbilityPhysics.HittableMask);
        var myTeam = root.GetComponent<TeamMember>();

        Collider closestHit = null;
        float closestDistSqr = float.MaxValue;

        foreach (Collider col in hits)
        {
            Transform targetRoot = col.transform.root;
            if (targetRoot == root) continue;
            if (myTeam != null && !myTeam.IsEnemy(targetRoot.gameObject)) continue;

            float distSqr = (col.ClosestPoint(origin) - origin).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closestHit = col;
            }
        }

        if (closestHit != null)
        {
            Transform targetRoot = closestHit.transform.root;
            IDamageable target = targetRoot.GetComponent<IDamageable>() ?? closestHit.GetComponent<IDamageable>();
            if (target != null)
            {
                Deal(target, damage);
            }
        }

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, origin + (dir * 1.5f), Quaternion.LookRotation(dir));
        }
        else
        {
            AbilityVfx.Cone(root.position + Vector3.up, dir, AbilityVfx.Gold, range, 20f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform root = transform.root;
        Gizmos.color = Color.yellow;

        Vector3 origin = root.position + Vector3.up * 1.5f;
        Vector3 dir = root.forward;
        Vector3 point1 = origin + dir * thrustRadius;
        Vector3 point2 = origin + dir * Mathf.Max(thrustRadius, range - thrustRadius);

        Gizmos.DrawWireSphere(point1, thrustRadius);
        Gizmos.DrawWireSphere(point2, thrustRadius);
        Gizmos.DrawLine(point1 + root.up * thrustRadius, point2 + root.up * thrustRadius);
        Gizmos.DrawLine(point1 - root.up * thrustRadius, point2 - root.up * thrustRadius);
        Gizmos.DrawLine(point1 + root.right * thrustRadius, point2 + root.right * thrustRadius);
        Gizmos.DrawLine(point1 - root.right * thrustRadius, point2 - root.right * thrustRadius);
    }
}
