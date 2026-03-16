using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int health = 100;

    public void TakeDamage(int damage, PlayerStats attacker = null) // Added parameter
    {
        health -= damage;
        if (attacker != null) attacker.AddCharge(damage); // Award charge on hit
        if (health <= 0) Die();
    }

    void Die()
    {
        Destroy(transform.root.gameObject);
    }
}