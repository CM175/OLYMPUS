using UnityEngine;

public class DamageDisplay : MonoBehaviour
{
    EnemyHealth _health;

    void Start()
    {
        _health = GetComponent<EnemyHealth>();
        EnemyHealth.OnDamageDealt += ShowDamage;
    }

    void OnDestroy() => EnemyHealth.OnDamageDealt -= ShowDamage;

    void ShowDamage(int damage, GameObject attacker)
    {
        //Debug.Log($"{gameObject.name} took {damage} damage. Health: {_health.currentHealth}");
    }
}