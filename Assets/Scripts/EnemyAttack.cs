using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    public int damage = 10;
    public float attackRange = 2f;
    public float attackCooldown = 2f;

    float _lastAttack;
    Transform _player;
    IDamageable _playerDamageable; // cached — no repeated GetComponent

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogError("[EnemyAttack] No Player found in scene.");
            return;
        }
        _player           = playerObj.transform;
        _playerDamageable = playerObj.GetComponent<IDamageable>();

        if (_playerDamageable == null)
            Debug.LogError("[EnemyAttack] Player has no IDamageable component.");
    }

    void Update()
    {
        if (_player == null || _playerDamageable == null) return;

        float distance = Vector3.Distance(transform.position, _player.position);
        if (distance < attackRange && Time.time > _lastAttack + attackCooldown)
            Attack();
    }

    void Attack()
    {
        _playerDamageable.TakeDamage(damage);
        _lastAttack = Time.time;
    }
}