using UnityEngine;
public class BaseProjectile : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 50f;
    public float gravityScale = 0f; // 0 for straight shots (Pharah), >0 for arcs (Junkrat)
    public float maxRange = 100f;

    [Header("Impact Settings")]
    public int damage = 20;
    public bool piercesTargets = false;
    public GameObject impactEffect;

    protected Vector3 spawnPosition;

    void Start()
    {
        spawnPosition = transform.position;
        InitializeMovement();
    }

    protected virtual void InitializeMovement()
    {
        // Set initial velocity based on Speed
    }

    void Update()
    {
        // Check if we've traveled past maxRange to self-destruct
        if (Vector3.Distance(spawnPosition, transform.position) >= maxRange)
        {
            Destroy(gameObject);
        }
    }

    // This is the "Magic" function you override for different heroes
    protected virtual void OnImpact(Collider other)
    {
        // 1. Play Sound/VFX
        SpawnImpactVisuals(other);

        // 2. Deal damage
        Applydamage(other);

        // 3. Logic for piercing vs disappearing
        if (!piercesTargets)
        {
            Destroy(gameObject);
        }
    }

    private void Applydamage(Collider other)
    {
        // Interface-based damage system is best
        var hit = other.GetComponent<IDamageable>();
        hit?.TakeDamage(damage);
    }

    protected virtual void SpawnImpactVisuals(Collider other)
    {

    }
}