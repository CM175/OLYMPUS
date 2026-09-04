using UnityEngine;

public class TridentStrike : Attack, IInitializable
{
    [Header("Melee Settings")]
    [SerializeField] private float thrustRadius = 1.2f;

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

    protected override void Activate()
    {
        // Handled via OnPress/OnRelease charge timings
    }

    private void PerformMeleeThrust()
    {
        IMovementController movement = _rootTransform.GetComponent<IMovementController>();
        Vector3 origin = (playerCamera != null) ? playerCamera.transform.position : _rootTransform.position + Vector3.up * 1.5f;
        Vector3 dir = (movement != null) ? movement.GetAimDirection() : _rootTransform.forward;

        if (Physics.SphereCast(origin, thrustRadius, dir, out RaycastHit hit, range))
        {
            Transform targetRoot = hit.collider.transform.root;
            var myTeam = _rootTransform.GetComponent<TeamMember>();
            if (myTeam != null && !myTeam.IsEnemy(targetRoot.gameObject)) return;

            IDamageable target = targetRoot.GetComponent<IDamageable>() ?? hit.collider.GetComponent<IDamageable>();
            if (target != null)
            {
                PlayerStats stats = _rootTransform.GetComponent<PlayerStats>();
                int finalDamage = (stats != null) ? Mathf.RoundToInt(damage * stats.DamageMultiplier) : damage;
                target.TakeDamage(finalDamage);
            }
        }

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, origin + dir * 1.5f, Quaternion.LookRotation(dir), _rootTransform);
        }
    }

    private void ThrowTrident(float chargeRatio)
    {
        if (tridentProjectilePrefab == null) return;

        Vector3 spawnPos = (playerCamera != null) ? playerCamera.transform.position + playerCamera.transform.forward : _rootTransform.position + Vector3.up * 1.5f;
        Quaternion spawnRot = (playerCamera != null) ? playerCamera.transform.rotation : _rootTransform.rotation;

        GameObject proj = Instantiate(tridentProjectilePrefab, spawnPos, spawnRot);
        TridentProjectile script = proj.GetComponent<TridentProjectile>();
        if (script != null)
        {
            script.SetAttacker(_rootTransform.gameObject);
            script.SetCharge(chargeRatio);
        }
    }
}