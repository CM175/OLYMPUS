using UnityEngine;

public class DamageDisplay : MonoBehaviour
{
    EnemyHealth _health;
    int _lastHealth;

    void Start()
    {
        _health = GetComponent<EnemyHealth>();
        _health.OnDamageDealt += ShowDamage;
    }

    void OnDestroy() => _health.OnDamageDealt -= ShowDamage;

    void ShowDamage(int damage, GameObject attacker)
    {
        //Debug.Log($"{gameObject.name} took {damage} damage. Health: {_health.currentHealth}");
    }
}