using UnityEngine;

public class MoonlightTrapAbility : Ability
{
    public GameObject trapPrefab;
    public GameObject spawnedTrap;

    protected override void Activate()
    {
        if (playerCamera == null) return;
        if (spawnedTrap != null)
        {
            Destroy(spawnedTrap.gameObject);
        }
        // Spawn the trap slightly in front of the camera
        spawnedTrap = Instantiate(trapPrefab, 
            playerCamera.transform.position + playerCamera.transform.forward,
            transform.rotation);
        MoonlightTrap trap = spawnedTrap.GetComponent<MoonlightTrap>();
        trap.SetAttacker(transform.root.gameObject);

        StartCooldown();
    }
}