using UnityEngine;

public class BidentPrimary : Attack, IInitializable
{
    [Header("Bident Projectile Settings")]
    [SerializeField] private GameObject bidentBoltPrefab;
    [SerializeField] private float fireRate = 0.3f;
    [SerializeField] private float projectileSpeed = 60f;

    private float _nextFireTime;
    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
    }

    protected override bool CanUse()
    {
        return Time.time >= _nextFireTime && base.CanUse();
    }

    protected override void Activate()
    {
        _nextFireTime = Time.time + fireRate;

        Transform root = _rootTransform != null ? _rootTransform : transform.root;
        Vector3 spawnPos;
        Quaternion spawnRot;

        if (playerCamera != null)
        {
            spawnPos = playerCamera.transform.position + (playerCamera.transform.forward * 1.5f);
            spawnRot = playerCamera.transform.rotation;
        }
        else
        {
            Vector3 aim = movementController != null ? movementController.GetAimDirection() : root.forward;
            spawnPos = root.position + Vector3.up * 1.5f + (aim * 1.5f);
            spawnRot = Quaternion.LookRotation(aim);
        }

        if (bidentBoltPrefab != null)
        {
            GameObject boltObj = Instantiate(bidentBoltPrefab, spawnPos, spawnRot);
            BaseProjectile projectile = boltObj.GetComponent<BaseProjectile>();
            if (projectile != null)
            {
                projectile.SetAttacker(root.gameObject);
                projectile.damage = damage;
                projectile.speed = projectileSpeed;
            }
        }
    }

    // Hook to reward bonus Soul Essence when hitting marked targets with abilities
    public void OnMarkedTargetHit(GameObject target)
    {
        // Add your Hades resource/soul manager logic here (e.g., HadesStats.AddSoulEssence(bonusAmount))
        //Debug.Log($"[Bident of the Dead] Bonus Soul Essence generated from hitting marked target: {target.name}");
    }
}