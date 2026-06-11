using UnityEngine;

public class SolarBlind : Ability
{
    [Header("Blind Settings")]
    public float flashRadius = 5f;
    public int minorBurnDamage = 15;
    public float utilityDuration = 2f;

    public float range = 15f;

    protected override void Activate()
    {
        if (playerCamera == null)
        {
            isExecuting = false;
            return;
        }

        // Fire a raycast or check origin directly in front of camera sightline
        Vector3 strikePoint = playerCamera.transform.position + playerCamera.transform.forward * range;
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, range))
        {
            strikePoint = hit.point;
        }

        // Spawn visual explosion if set
        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, strikePoint, Quaternion.identity);
        }

        // Detect targets caught in the flash area
        Collider[] victims = Physics.OverlapSphere(strikePoint, flashRadius);
        foreach (var victim in victims)
        {
            if (victim.CompareTag("Enemy"))
            {
                // Apply damage via decoupling interface
                victim.GetComponent<IDamageable>()?.TakeDamage(minorBurnDamage);

                // Safe hook into NavMesh AI to simulate a disorientation stun/slow
                var enemyAI = victim.GetComponentInParent<EnemyAI>();
                if (enemyAI != null)
                {
                    enemyAI.Stun(utilityDuration);
                    Debug.Log($"[SolarBlind] Disoriented AI unit: {victim.name}");
                }
            }
        }

        StartCooldown();
    }
}