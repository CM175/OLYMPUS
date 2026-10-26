using UnityEngine;

public class AscensionLeap : Ability
{
    [Header("Mobility")]
    public float upwardForce = 14f;
    public float forwardForce = 6f;
    [Header("Healing")]
    public int healAmount = 35; 
    public float healDuration = 2f; // Duration of the healing effect

    protected override void Activate()
    {
        IMovementController movement = transform.root.GetComponent<IMovementController>();
        IDamageable health = transform.root.GetComponent<IDamageable>();
        if (movement != null && playerCamera != null)
        {
            Vector3 jumpVector = Vector3.up * upwardForce + playerCamera.transform.forward * forwardForce;
            movement.ApplyImpulseForce(jumpVector);
            health?.HealOverTime(healAmount, healDuration); // Apply brief invincibility during leap
        }

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, transform.root.position, Quaternion.identity);
        }

        StartCooldown();
    }
}