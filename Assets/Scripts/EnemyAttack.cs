using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    public int damage = 10;
    public float attackRange = 2f;
    public float attackCooldown = 2f;

    float lastAttack;

    Transform player;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void Update()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance < attackRange && Time.time > lastAttack + attackCooldown)
        {
            Attack();
        }
    }

    void Attack()
    {
        player.GetComponent<PlayerHealth>().TakeDamage(damage);
        lastAttack = Time.time;
    }
}