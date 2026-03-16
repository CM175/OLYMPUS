
using System.ComponentModel;
using UnityEngine;

public class LightningBolt : Ability
{
    public float range = 100f;
    public int damage = 25;
    public float chainRange = 15f;

    protected override void Activate()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            
            EnemyHealth hitEnemy = hit.collider.GetComponent<EnemyHealth>();
            if (hitEnemy != null)
            {
                // Apply damage
                hitEnemy.TakeDamage(damage);
                
                // Chain to nearest nearby enemy
                Collider[] nearby = Physics.OverlapSphere(hit.point, chainRange);
                foreach (var col in nearby)
                {
                    
                    if (col.gameObject == hit.collider.gameObject) continue;

                    // Skip anything without EnemyHealth
                    EnemyHealth chainTarget = col.GetComponent<EnemyHealth>();
                    if (chainTarget == null) continue;

                    // Valid chain target found
                    chainTarget.TakeDamage((int)(damage * 0.5f));
                    //Debug.Log($"Thunderbolt: Chained to {col.name} for {damage * 0.5f} damage");
                    break;
                }
            }

            

            //Debug.Log($"Thunderbolt hit {hit.collider.name}");
        }
    }

}