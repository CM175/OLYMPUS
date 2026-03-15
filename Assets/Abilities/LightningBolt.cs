using UnityEngine;

public class LightningBolt : Ability
{
    public float range = 100f;
    public int damage = 25;
    public float chainRange = 5f;

    protected override void Activate()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            // Apply damage
            hit.collider.GetComponent<PlayerHealth>()?.TakeDamage(damage);
            hit.collider.GetComponent<EnemyHealth>()?.TakeDamage(damage);

            // Chain to nearest nearby enemy
            Collider[] nearby = Physics.OverlapSphere(hit.point, chainRange);
            foreach (var col in nearby)
            {
                if (col.gameObject == hit.collider.gameObject) continue;
                col.GetComponent<EnemyHealth>()?.TakeDamage((int)(damage * 0.5f));
                Debug.Log("Chain ");
                break; // only chain once
            }

            Debug.Log($"Thunderbolt hit {hit.collider.name}");
        }
    }

}