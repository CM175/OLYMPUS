using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    public int damage = 10;
    public float attackRange = 2f;
    public float attackCooldown = 2f;

    float _lastAttack;
    Transform _player;
    IDamageable _playerDamageable; // cached — no repeated GetComponent
    bool targetAssigned;

    void Start()
    {
        targetAssigned = false;
        RLEnemyCombatAgent targetAgent = FindFirstObjectByType<RLEnemyCombatAgent>();
        _player = targetAgent != null ? targetAgent.transform : null;
        if (_player == null)
        {
            Debug.LogError($"[{GetType().Name}] No RLEnemyCombatAgent found.");
            return;
        }

        _playerDamageable = _player.GetComponent<IDamageable>();

        if (_playerDamageable == null)
            Debug.LogError("[EnemyAttack] Player has no IDamageable component.");
    }

    void Update()
    {
        if (!targetAssigned)
        {
            RLEnemyCombatAgent targetAgent = FindFirstObjectByType<RLEnemyCombatAgent>();
            _player = targetAgent != null ? targetAgent.transform : null;
            if (_player == null)
            {
                Debug.LogError($"[{GetType().Name}] No RLEnemyCombatAgent found.");
                return;
            }

            _playerDamageable = _player.GetComponent<IDamageable>();

            if (_playerDamageable == null)
                Debug.LogError("[EnemyAttack] Player has no IDamageable component.");
        } 

        if (_player == null || _playerDamageable == null) return;
        float distance = Vector3.Distance(transform.position, _player.position);
        if (distance < attackRange && Time.time > _lastAttack + attackCooldown)
            Attack();
    }

    void Attack()
    {
        DamageDealer.Deal(gameObject, _playerDamageable, damage);
        _lastAttack = Time.time;
    }

    void Chase()
    {

    }
}