using System.Collections;
using UnityEngine;

public class ChariotOfTheSun : Ultimate, IInitializable
{
    public float duration = 5f;
    public float beamRadius = 6f;
    public int damagePerTick = 10;
    public int healPerTick = 15;
    public float tickRate = 0.5f;

    private PlayerController _playerController;

    public void Initialize(GameObject playerRoot)
    {
        _playerController = playerRoot.GetComponent<PlayerController>();
    }

    protected override void Activate()
    {
        base.Activate(); // Safely processes and consumes Ultimate Charge resources
        StartCoroutine(AscensionChariotRoutine());
    }

    private IEnumerator AscensionChariotRoutine()
    {
        Debug.Log("[Ultimate] Apollo takes to the sky in his Chariot!");

        // Brief tactical logic adjustment: apply temporary invincibility or speed while channeling from overhead views
        PlayerHealth health = transform.root.GetComponent<PlayerHealth>();
        if (health != null) health.IsInvincible = true;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Cast down from player crosshair perspective to establish localized tracking coordinate systems
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, range))
            {
                // Spawn a tracing visual frame at the hit position
                if (vfxPrefab != null)
                {
                    GameObject fx = Instantiate(vfxPrefab, hit.point, Quaternion.identity);
                    Destroy(fx, tickRate);
                }

                // Process Area of Effect logic
                Collider[] entities = Physics.OverlapSphere(hit.point, beamRadius);
                foreach (var col in entities)
                {
                    if (col.CompareTag("Enemy"))
                    {
                        col.GetComponent<IDamageable>()?.TakeDamage(damagePerTick);
                    }
                    else if (col.CompareTag("Player"))
                    {
                        var pH = col.GetComponent<PlayerHealth>();
                        if (pH != null)
                        {
                            pH.currentHealth = Mathf.Min(pH.maxHealth, pH.currentHealth + healPerTick);
                        }
                    }
                }
            }

            yield return new WaitForSeconds(tickRate);
            elapsed += tickRate;
        }

        if (health != null) health.IsInvincible = false;
        
        Debug.Log("[Ultimate] Apollo's Chariot returned safely.");
        StartCooldown();
    }
}