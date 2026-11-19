using System.Collections.Generic;
using UnityEngine;

public class ShieldBash : Ability
{
    [Header("Stats")]
    public float bashRange = 4f;
    public float bashRadius = 1.2f;
    public int damage = 20;
    public float stunDuration = 1.5f;

    [Header("Cone")]
    [Tooltip("Dot-product threshold. 0.5 ≈ 60° half-angle cone in front of the player.")]
    public float minDotProduct = 0.5f;

    protected override void Activate()
    {
        Transform root = transform.root;
        IMovementController movement = root.GetComponent<IMovementController>();

        Vector3 origin = (playerCamera != null) ? playerCamera.transform.position : root.position + Vector3.up * 1.5f;
        Vector3 dir = (playerCamera != null) ? playerCamera.transform.forward : (movement != null ? movement.GetAimDirection() : root.forward);

        AbilityVfx.Cone(root.position + Vector3.up, dir, AbilityVfx.Gold, bashRange,
                        Mathf.Acos(Mathf.Clamp(minDotProduct, -1f, 1f)) * Mathf.Rad2Deg);

        Vector3 point1 = origin + dir * bashRadius;
        Vector3 point2 = origin + dir * Mathf.Max(bashRadius, bashRange - bashRadius);

        Collider[] hits = Physics.OverlapCapsule(point1, point2, bashRadius, AbilityPhysics.HittableMask);
        var myTeam = root.GetComponent<TeamMember>();
        HashSet<Transform> hitTargets = new HashSet<Transform>();

        foreach (Collider col in hits)
        {
            Transform targetRoot = col.transform.root;
            if (targetRoot == root || hitTargets.Contains(targetRoot)) continue;
            if (myTeam != null && !myTeam.IsEnemy(targetRoot.gameObject)) continue;

            Vector3 toTarget = (col.ClosestPoint(origin) - origin);
            if (toTarget.sqrMagnitude > 0.001f && Vector3.Dot(dir, toTarget.normalized) < minDotProduct) continue;

            IDamageable target = targetRoot.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>();
            if (target == null) continue;

            hitTargets.Add(targetRoot);
            Deal(target, damage);

            IMovementController targetController = targetRoot.GetComponent<IMovementController>();
            if (targetController != null) targetController.Stun(stunDuration);
        }

        StartCooldown();
    }

    private void OnDrawGizmosSelected()
    {
        Transform root = transform.root;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);

        Vector3 origin = root.position + Vector3.up * 1.5f;
        Vector3 dir = root.forward;
        Vector3 point1 = origin + dir * bashRadius;
        Vector3 point2 = origin + dir * Mathf.Max(bashRadius, bashRange - bashRadius);

        Gizmos.DrawWireSphere(point1, bashRadius);
        Gizmos.DrawWireSphere(point2, bashRadius);
        Gizmos.DrawLine(point1 + root.up * bashRadius, point2 + root.up * bashRadius);
        Gizmos.DrawLine(point1 - root.up * bashRadius, point2 - root.up * bashRadius);
        Gizmos.DrawLine(point1 + root.right * bashRadius, point2 + root.right * bashRadius);
        Gizmos.DrawLine(point1 - root.right * bashRadius, point2 - root.right * bashRadius);
    }
}
