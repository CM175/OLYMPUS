using UnityEngine;

public class SkyCharge : Ability
{
    public float dashDistance = 8f;
    public GameObject trailEffectPrefab;
    public int damage = 30;

    protected override void Activate()
    {
        CharacterController controller = GetComponent<CharacterController>();
        if (controller == null) return;

        Vector3 dashTarget = transform.position + transform.forward * dashDistance;

        // Check for obstacles
        if (Physics.Raycast(transform.position, transform.forward,
            out RaycastHit hit, dashDistance))
        {
            dashTarget = hit.point - transform.forward * 0.5f;
        }

        controller.enabled = false;
        transform.position = dashTarget;
        controller.enabled = true;

        // Damage anything in the trail
        Collider[] hit2 = Physics.OverlapCapsule(
            transform.position, dashTarget, 1f);
        foreach (var col in hit2)
        {
            if (col.gameObject == gameObject) continue;
            col.GetComponent<EnemyHealth>()?.TakeDamage(damage);
        }

        Debug.Log("Sky Charge activated");
    }
}