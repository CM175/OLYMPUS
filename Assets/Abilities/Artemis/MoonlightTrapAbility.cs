using UnityEngine;

public class MoonlightTrapAbility : Ability
{
    public GameObject trapPrefab;

    protected override void Activate()
    {
        if (playerCamera == null) return;

        // Spawn the trap slightly in front of the camera
        Instantiate(trapPrefab, 
            playerCamera.transform.position + playerCamera.transform.forward,
            transform.rotation);

        StartCooldown();
    }
}