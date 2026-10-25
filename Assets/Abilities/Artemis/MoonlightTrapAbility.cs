using UnityEngine;

public class MoonlightTrapAbility : Ability
{
    public GameObject trapPrefab;
    public GameObject spawnedTrap;

    protected override void Activate()
    {
        Transform root = transform.root;
        Vector3 spawnPos;
        Quaternion spawnRot;

        if (playerCamera != null)
        {
            spawnPos = playerCamera.transform.position + playerCamera.transform.forward;
            spawnRot = transform.rotation;
        }
        else
        {
            Vector3 aim = movementController != null ? movementController.GetAimDirection() : root.forward;
            spawnPos = root.position + Vector3.up * 0.5f + aim;
            spawnRot = Quaternion.LookRotation(aim);
        }

        if (spawnedTrap != null)
        {
            Destroy(spawnedTrap.gameObject);
        }

        spawnedTrap = Instantiate(trapPrefab, spawnPos, spawnRot);
        MoonlightTrap trap = spawnedTrap.GetComponent<MoonlightTrap>();
        if (trap != null)
        {
            trap.SetAttacker(root.gameObject);
        }
        StartCooldown();
    }

    private void OnDestroy()
    {
        if (spawnedTrap != null)
        {
            Destroy(spawnedTrap.gameObject);
        }
    }
}