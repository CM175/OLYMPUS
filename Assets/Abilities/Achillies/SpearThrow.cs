using UnityEngine;

public class SpearThrow : Ability
{
    [Header("Projectile Settings")]
    public GameObject spearPrefab;
    public int damage = 30;

    protected override void Activate()
    {
        // 1. Determine spawn position and rotation
        Vector3 spawnPos = playerCamera.transform.position + (playerCamera.transform.forward * 1.5f);
        Quaternion spawnRot = playerCamera.transform.rotation;

        // 2. Instantiate the physical spear
        GameObject spearObj = Instantiate(spearPrefab, spawnPos, spawnRot);
        
        // 3. Configure the projectile's stats dynamically based on player buffs
        SpearProjectile projectile = spearObj.GetComponent<SpearProjectile>();
        if (projectile != null)
        {
            projectile.SetAttacker(transform.root.gameObject); 
            PlayerStats stats = transform.root.GetComponent<PlayerStats>();
            
            // Apply Damage Multiplier from PlayerStats
            int finalDamage = stats != null
                ? Mathf.RoundToInt(damage * stats.DamageMultiplier)
                : damage;

            projectile.damage = finalDamage;
        }

        StartCooldown(); 
    }
}