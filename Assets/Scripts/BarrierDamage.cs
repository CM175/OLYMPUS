using UnityEngine;

public class BarrierDamage : MonoBehaviour 
{
    public int damage = 10;
    [HideInInspector] public PlayerStats attackerStats;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the bubble is an enemy
        if (other.CompareTag("Enemy"))
        {
            EnemyHealth enemy = other.GetComponent<EnemyHealth>();
            if (enemy != null) 
            {
                // Apply damage and award ultimate charge to the player
                enemy.TakeDamage(damage, attackerStats);
                Debug.Log($"Barrier zapped {other.name} for {damage} damage");
            }
        }
    }
}