using UnityEngine;

public class SpearThrow : Ability
{
    [Header("Projectile Settings")]
    public GameObject spearPrefab;
    public int damage = 30;

    protected override void Activate()
    {
        Transform root = transform.root;
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

        GameObject spearObj = Instantiate(spearPrefab, spawnPos, spawnRot);
        SpearProjectile projectile = spearObj.GetComponent<SpearProjectile>();
        if (projectile != null)
        {
            projectile.SetAttacker(root.gameObject);
            projectile.damage = damage; // multiplier applied on impact by BaseProjectile
        }
        StartCooldown();
    }
}