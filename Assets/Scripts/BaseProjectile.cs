using UnityEngine;

public class BaseProjectile : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 50f;
    public float gravityScale = 0f; 
    public float maxRange = 100f;

    [Header("Impact Settings")]
    public int damage = 20;
    public bool piercesTargets = false;
    public GameObject impactEffect;

    protected Vector3 spawnPosition;
    protected Rigidbody rb;
    protected GameObject attackerRoot;
    public virtual void SetAttacker(GameObject attacker)
    {
        attackerRoot = attacker.transform.root.gameObject;
    }

    protected virtual void Start()
    {
        spawnPosition = transform.position;
        rb = GetComponent<Rigidbody>(); 
    }

    protected virtual void InitializeMovement()
    {
        if (rb != null)
        {
            rb.useGravity = gravityScale > 0;
            rb.linearVelocity = transform.forward * speed;
        }
    }

    protected virtual void Update()
    {
        if (Vector3.Distance(spawnPosition, transform.position) >= maxRange)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        OnImpact(other);
    }

    protected virtual void OnImpact(Collider other)
    {
        
        SpawnImpactVisuals(other);
        ApplyDamage(other);

        if (!piercesTargets)
        {
            Destroy(gameObject);
        }
    }

    protected void ApplyDamage(Collider other)
    {
        if (attackerRoot != null)
        {
            var myTeam = attackerRoot.GetComponent<TeamMember>();
            if (myTeam != null && !myTeam.IsEnemy(other.transform.root.gameObject))
            {
                return; 
            }
        }
        // OPTIMIZED: Adaptive interface resolution across both primary objects and layout children proxies
        IDamageable hit = other.GetComponent<IDamageable>();
        hit?.TakeDamage(damage);
    }

    protected void SpawnImpactVisuals(Collider other)
    {
        if (impactEffect != null)
        {
            Instantiate(impactEffect, transform.position, Quaternion.identity);
        }
    }
}