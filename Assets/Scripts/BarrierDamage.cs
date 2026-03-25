using UnityEngine;

public class BarrierDamage : MonoBehaviour
{
    public int damage = 10;
    [HideInInspector] public PlayerStats attackerStats;

    private void OnTriggerEnter(Collider other)
    {
<<<<<<< HEAD
        // Fix: tag check BEFORE logging — was firing for every collider
        if (!other.CompareTag("Enemy")) return;

        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null) return;

        target.TakeDamage(damage);
        Debug.Log($"[BarrierDamage] Zapped {other.name} for {damage} damage.");
=======
        Debug.Log($"Barrier zapped {other.name} for {damage} damage");
        // Check if the object entering the bubble is an enemy
        if (other.CompareTag("Enemy"))
        {
            
            
            EnemyHealth enemy = other.GetComponent<EnemyHealth>();
            if (enemy != null) 
            {
                // Apply damage and award ultimate charge to the player
                enemy.TakeDamage(damage, gameObject);
                Debug.Log($"Barrier zapped {other.name} for {damage} damage");
            }
        }
>>>>>>> 43798827e79650ac34ec472772b02a2c78cecc8c
    }
}