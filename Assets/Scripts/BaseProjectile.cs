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

    protected virtual void Start()
    {
        spawnPosition = transform.position;
        rb = GetComponent<Rigidbody>(); 
        // We removed InitializeMovement() from here so it doesn't fire prematurely
    }

    protected virtual void InitializeMovement()
    {
        // Default straight-line movement
        if (rb != null)
        {
            rb.useGravity = gravityScale > 0;
            rb.linearVelocity = transform.forward * speed;
        }
    }

    protected virtual void Update()
    {
        // Self-destruct if out of range
        if (Vector3.Distance(spawnPosition, transform.position) >= maxRange)
        {
            Destroy(gameObject);
        }
    }

    // This is called by Unity Physics (Ensure Collider is a Trigger)
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
        var hit = other.GetComponent<IDamageable>();
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