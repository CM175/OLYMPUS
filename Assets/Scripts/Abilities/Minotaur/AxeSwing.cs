using System.Collections.Generic;
using UnityEngine;

public class AxeSwing : Attack, IInitializable
{
    [Header("Melee Combo Settings")]
    [Tooltip("Capsule radius for the axe cleave hitbox.")]
    [SerializeField] private float swingRadius = 1.4f;
    [Tooltip("The cone angle width for the axe slash (e.g., 90 degrees total front arc).")]
    [SerializeField] private float swingAngle = 90f;

    [Header("Combo Alternation Visuals")]
    [SerializeField] private GameObject vfxSwingLeftPrefab;
    [SerializeField] private GameObject vfxSwingRightPrefab;

    private bool _isLeftSwing = true;
    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
    }

    protected override void Activate()
    {
        if (_rootTransform == null) _rootTransform = transform.root;

        Vector3 origin = (playerCamera != null) ? playerCamera.transform.position : _rootTransform.position + Vector3.up * 1.5f;
        Vector3 dir = (playerCamera != null)
            ? playerCamera.transform.forward
            : (movementController != null ? movementController.GetAimDirection() : _rootTransform.forward);

        Vector3 flatLookDir = dir;
        flatLookDir.y = 0f;
        if (flatLookDir.sqrMagnitude > 0.001f) flatLookDir.Normalize();
        else flatLookDir = _rootTransform.forward;

        Vector3 spawnPos = (playerCamera != null)
            ? playerCamera.transform.position + (playerCamera.transform.forward * 1.5f)
            : _rootTransform.position + Vector3.up * 1.5f + (flatLookDir * 1.5f);

        GameObject chosenVFX = _isLeftSwing ? vfxSwingLeftPrefab : vfxSwingRightPrefab;
        if (chosenVFX == null) chosenVFX = vfxPrefab;
        if (chosenVFX != null)
        {
            Instantiate(chosenVFX, spawnPos, Quaternion.LookRotation(flatLookDir), _rootTransform);
        }

        _isLeftSwing = !_isLeftSwing;

        // Define the two sphere centers of the capsule along the swing vector
        Vector3 point1 = origin + dir * swingRadius;
        Vector3 point2 = origin + dir * Mathf.Max(swingRadius, range - swingRadius);

        Collider[] hits = Physics.OverlapCapsule(point1, point2, swingRadius, AbilityPhysics.HittableMask);
        float cosHalfAngle = Mathf.Cos(swingAngle * 0.5f * Mathf.Deg2Rad);
        var myTeam = _rootTransform.GetComponent<TeamMember>();
        HashSet<Transform> hitTargets = new HashSet<Transform>();

        foreach (Collider col in hits)
        {
            Transform victimRoot = col.transform.root;
            if (victimRoot == _rootTransform || hitTargets.Contains(victimRoot)) continue;
            if (myTeam != null && !myTeam.IsEnemy(victimRoot.gameObject)) continue;

            Vector3 toTargetFlat = col.ClosestPoint(origin) - origin;
            toTargetFlat.y = 0f;
            if (toTargetFlat.sqrMagnitude > 0.01f)
            {
                toTargetFlat.Normalize();
                if (Vector3.Dot(flatLookDir, toTargetFlat) < cosHalfAngle) continue;
            }

            IDamageable targetDamage = victimRoot.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>();
            if (targetDamage != null)
            {
                hitTargets.Add(victimRoot);
                Deal(targetDamage, damage);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform root = _rootTransform != null ? _rootTransform : transform.root;
        Gizmos.color = Color.red;

        Vector3 origin = root.position + Vector3.up * 1.5f;
        Vector3 dir = root.forward;
        Vector3 point1 = origin + dir * swingRadius;
        Vector3 point2 = origin + dir * Mathf.Max(swingRadius, range - swingRadius);

        Gizmos.DrawWireSphere(point1, swingRadius);
        Gizmos.DrawWireSphere(point2, swingRadius);
        Gizmos.DrawLine(point1 + root.up * swingRadius, point2 + root.up * swingRadius);
        Gizmos.DrawLine(point1 - root.up * swingRadius, point2 - root.up * swingRadius);
        Gizmos.DrawLine(point1 + root.right * swingRadius, point2 + root.right * swingRadius);
        Gizmos.DrawLine(point1 - root.right * swingRadius, point2 - root.right * swingRadius);
    }
}
