using UnityEngine;

public class TridentStrike : Attack, IInitializable
{
    [Header("Melee Settings")]
    [SerializeField] private float thrustRadius = 0.6f;

    [Header("Ranged Throw Settings")]
    [SerializeField] private GameObject tridentProjectilePrefab;
    [SerializeField] private float maxChargeTime = 1.2f;

    private float _pressStartTime;
    private bool _isCharging;
    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
    }

    public override void OnPress()
    {
        base.OnPress();
        if (!CanUse()) return;
        _isCharging = true;
        _pressStartTime = Time.time;
    }

    public override void OnRelease()
    {
        base.OnRelease();
        if (!_isCharging) return;

        float heldDuration = Time.time - _pressStartTime;
        if (heldDuration >= 0.35f)
        {
            float chargeRatio = Mathf.Clamp01((heldDuration - 0.35f) / (maxChargeTime - 0.35f));
            ThrowTrident(chargeRatio);
        }
        else
        {
            PerformMeleeThrust();
        }

        _isCharging = false;
        StartCooldown();
    }

    private void PerformMeleeThrust()
    {
        IMovementController movement = _rootTransform.GetComponent<IMovementController>();
        Vector3 origin = (playerCamera != null) ? playerCamera.transform.position : _rootTransform.position + Vector3.up * 1.5f;
        Vector3 dir = (playerCamera != null) ? playerCamera.transform.forward : (movement != null ? movement.GetAimDirection() : _rootTransform.forward);

        // Define the two sphere centers of the capsule along the thrust vector
        Vector3 point1 = origin + dir * thrustRadius;
        Vector3 point2 = origin + dir * Mathf.Max(thrustRadius, range - thrustRadius);

        Collider[] hits = Physics.OverlapCapsule(point1, point2, thrustRadius, AbilityPhysics.HittableMask);
        var myTeam = _rootTransform.GetComponent<TeamMember>();

        Collider closestHit = null;
        float closestDistSqr = float.MaxValue;

        foreach (Collider col in hits)
        {
            Transform targetRoot = col.transform.root;
            if (targetRoot == _rootTransform) continue;
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
            Instantiate(vfxPrefab, origin + dir * 1.5f, Quaternion.LookRotation(dir), _rootTransform);
        }
        else
        {
            AbilityVfx.Cone(_rootTransform.position + Vector3.up, dir, AbilityVfx.Teal, range, 25f);
        }
    }

    private void ThrowTrident(float chargeRatio)
    {
        if (tridentProjectilePrefab == null) return;

        Vector3 spawnPos;
        Quaternion spawnRot;

        if (playerCamera != null)
        {
            spawnPos = playerCamera.transform.position + playerCamera.transform.forward;
            spawnRot = playerCamera.transform.rotation;
        }
        else
        {
            IMovementController movement = _rootTransform.GetComponent<IMovementController>();
            Vector3 aim = movement != null ? movement.GetAimDirection() : _rootTransform.forward;
            spawnPos = _rootTransform.position + Vector3.up * 1.5f + aim;
            spawnRot = Quaternion.LookRotation(aim);
        }

        GameObject proj = Instantiate(tridentProjectilePrefab, spawnPos, spawnRot);
        TridentProjectile script = proj.GetComponent<TridentProjectile>();
        if (script != null)
        {
            script.SetAttacker(_rootTransform.gameObject);
            script.SetCharge(chargeRatio);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform root = _rootTransform != null ? _rootTransform : transform.root;
        Gizmos.color = Color.cyan;

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