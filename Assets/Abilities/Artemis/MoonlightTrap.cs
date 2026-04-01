using UnityEngine;

public class MoonlightTrap : Ability
{
    public GameObject trapPrefab;
    public float throwForce = 5f;

    protected override void Activate()
    {
        Vector3 spawnPos = transform.root.position + transform.root.forward;
        GameObject trap = Instantiate(trapPrefab, spawnPos, Quaternion.identity);
        
        Rigidbody rb = trap.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(transform.root.forward * throwForce, ForceMode.Impulse);
        }

        Debug.Log("[MoonlightTrap] Trap deployed.");
        StartCooldown();
    }
}