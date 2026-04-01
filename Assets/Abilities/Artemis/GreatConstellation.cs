using UnityEngine;

public class GreatConstellation : Ultimate
{
    public float damage = 150f;
    public float beamRadius = 2f;

    protected override void Activate()
    {
        base.Activate(); // Consumes Ultimate Charge

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.SphereCast(ray, beamRadius, out RaycastHit hit, 100f))
        {
            IDamageable target = hit.collider.GetComponent<IDamageable>();
            target?.TakeDamage(Mathf.RoundToInt(damage));
        }

        Debug.Log("[GreatConstellation] The stars have aligned.");
        StartCooldown();
    }
}