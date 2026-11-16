using UnityEngine;

public class DaggerProjectile : BaseProjectile
{
    [Header("Damage Fall-Off Settings")]
    [SerializeField] private float minDamageRange = 10f;  // Full damage up to this distance
    [SerializeField] private float maxDamageRange = 25.0f; // Minimum damage past this distance
    [Range(0.1f, 1.0f)]
    [SerializeField] private float minimumDamageMultiplier = 0.4f; // Floor multiplier (e.g., 40% damage at max range)

    private Vector3 _spawnPosition;
    private bool _hasSpawnPositionSet = false;

    public void SetSpawnPosition(Vector3 spawnPos)
    {
        _spawnPosition = spawnPos;
        _hasSpawnPositionSet = true;
    }

    protected override void Start()
    {
        base.Start();
        if (!_hasSpawnPositionSet)
        {
            _spawnPosition = transform.position;
        }
    }

    protected override void OnImpact(Collider other)
    {
        // Ignore self-hits
        if (attackerRoot != null && other.transform.root.gameObject == attackerRoot) return;

        Transform targetRoot = other.transform.root;
        var myTeam = attackerRoot != null ? attackerRoot.GetComponent<TeamMember>() : null;
        if (myTeam != null && !myTeam.IsEnemy(targetRoot.gameObject)) return;

        // Calculate distance traveled for fall-off
        float distanceTraveled = Vector3.Distance(_spawnPosition, transform.position);
        
        int finalCalculatedDamage = CalculateFallOffDamage(distanceTraveled);

        // Apply calculated damage to target
        IDamageable target = targetRoot.GetComponent<IDamageable>() ?? other.GetComponent<IDamageable>();
        if (target != null)
        {
            // If you are using your project's DamageDealer class:
            DamageDealer.Deal(attackerRoot, target, finalCalculatedDamage);
        }

        SpawnImpactVisuals(other);
        
        if (!piercesTargets)
        {
            Destroy(gameObject);
        }
    }

    private int CalculateFallOffDamage(float distance)
    {
        if (distance <= minDamageRange)
        {
            return damage; // Full damage at close range
        }
        else if (distance >= maxDamageRange)
        {
            return Mathf.RoundToInt(damage * minimumDamageMultiplier); // Floor damage at long range
        }
        else
        {
            // Linear interpolation curve between min and max range
            float t = (distance - minDamageRange) / (maxDamageRange - minDamageRange);
            float multiplier = Mathf.Lerp(1.0f, minimumDamageMultiplier, t);
            return Mathf.RoundToInt(damage * multiplier);
        }
    }
}