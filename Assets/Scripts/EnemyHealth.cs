using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour
{
    public int health = 100;
    
    // Pass the GameObject of the killer so listeners can check if it was them
    public UnityEvent<GameObject> OnDeathWithKiller; 

    public void TakeDamage(int damage, GameObject attacker = null)
    {
        health -= damage;
        
        // If the attacker has PlayerStats, they get charge automatically
        if (attacker != null)
        {
            PlayerStats stats = attacker.GetComponent<PlayerStats>();
            if (stats != null) stats.AddCharge(damage);
        }

        if (health <= 0) Die(attacker);
    }

    void Die(GameObject attacker)
    {
        OnDeathWithKiller?.Invoke(attacker);
        Destroy(transform.root.gameObject);
    }
}