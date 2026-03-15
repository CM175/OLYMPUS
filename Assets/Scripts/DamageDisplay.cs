using UnityEngine;

public class DamageDisplay : MonoBehaviour
{
    EnemyHealth _health;
    int _lastHealth;

    void Start()
    {
        _health = GetComponent<EnemyHealth>();
        _lastHealth = _health.health;
    }

    void Update()
    {
        if (_health.health != _lastHealth)
        {
            int damage = _lastHealth - _health.health;
            Debug.Log($"{gameObject.name} took {damage} damage. Health: {_health.health}/{_lastHealth+damage}");
            _lastHealth = _health.health;
            
        }
    }
}