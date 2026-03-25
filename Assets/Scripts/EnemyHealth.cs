using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public int health = 100;

    public UnityEvent<GameObject> OnDeathWithKiller;

    public event System.Action<int> OnDamageDealt;
    public static event System.Action<int> OnAnyEnemyDamaged;

    // Satisfies IDamageable — attacker unknown
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(int damage, GameObject attacker)
    {
        health -= damage;
        OnDamageDealt?.Invoke(damage);
        OnAnyEnemyDamaged?.Invoke(damage);
        if (health <= 0) Die(attacker);
    }

    private void Die(GameObject attacker)
    {
        OnDeathWithKiller?.Invoke(attacker);
        Destroy(gameObject);
    }
}