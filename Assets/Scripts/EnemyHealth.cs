using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public int health = 100;

    /// <summary>
    /// Instance event — raised on this specific enemy taking damage.
    /// Useful for per-enemy effects (hit flash, damage numbers, etc.)
    /// </summary>
    public event System.Action<int> OnDamageDealt;

    /// <summary>
    /// Static event — raised whenever ANY enemy takes damage.
    /// PlayerStats subscribes once and awards charge without needing
    /// a reference to individual enemies or knowing they exist.
    /// </summary>
    public static event System.Action<int> OnAnyEnemyDamaged;

    public void TakeDamage(int damage)
    {
        health -= damage;
        OnDamageDealt?.Invoke(damage);
        OnAnyEnemyDamaged?.Invoke(damage);
        if (health <= 0) Die();
    }

    void Die()
    {
        Destroy(gameObject);
    }
}