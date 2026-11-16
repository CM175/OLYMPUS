using System.Collections;
using UnityEngine;

public class WingedDaggers : Attack
{
    [Header("Burst Settings")]
    [SerializeField] private GameObject daggerPrefab;
    [SerializeField] private int daggersPerBurst = 2;
    [SerializeField] private float burstInterval = 0.08f; // Time between each dagger in the burst
    [SerializeField] private float throwForce = 50f;
    [SerializeField] private float fireCooldown = 1f;   // Cooldown after a full burst

    private float _nextCanUseTime;
    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
    }

    protected override bool CanUse()
    {
        return Time.time >= _nextCanUseTime && base.CanUse();
    }

    protected override void Activate()
    {
        _nextCanUseTime = Time.time + fireCooldown;
        StartCoroutine(FireBurstRoutine());
    }

    private IEnumerator FireBurstRoutine()
    {
        Transform root = _rootTransform != null ? _rootTransform : transform.root;

        for (int i = 0; i < daggersPerBurst; i++)
        {
            // Re-evaluate aim direction slightly during the burst for smooth tracking
            Vector3 aimDir = playerCamera != null 
                ? playerCamera.transform.forward 
                : (movementController != null ? movementController.GetAimDirection() : root.forward);
            aimDir.Normalize();

            Vector3 spawnPos = playerCamera != null 
                ? playerCamera.transform.position + playerCamera.transform.forward * 1.0f + (playerCamera.transform.right * (i * 0.1f - 0.1f)) // Slight horizontal spread per dagger
                : root.position + Vector3.up * 1.5f + aimDir * 1.0f;

            if (daggerPrefab != null)
            {
                GameObject dagger = Instantiate(daggerPrefab, spawnPos, Quaternion.LookRotation(aimDir));
                DaggerProjectile proj = dagger.GetComponent<DaggerProjectile>();
                if (proj != null)
                {
                    proj.SetAttacker(root.gameObject);
                    proj.damage = damage;
                    proj.SetSpawnPosition(spawnPos);
                }
            }

            // Wait for the next dagger in the burst (skip delay on the final shot)
            if (i < daggersPerBurst - 1)
            {
                yield return new WaitForSeconds(burstInterval);
            }
        }
    }
}