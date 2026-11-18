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

    [Header("Generated VFX")]
    [Tooltip("Adds a code-generated trail and impact burst when the prefab has no particle effects of its own.")]
    public bool useGeneratedVfx = true;
    [Tooltip("Leave alpha at 0 to use the projectile type's default colour.")]
    public Color vfxColor = new Color(0f, 0f, 0f, 0f);
    protected virtual Color DefaultVfxColor => new Color(1f, 0.9f, 0.6f);
    protected Color VfxColor => vfxColor.a > 0f ? vfxColor : DefaultVfxColor;

    protected Vector3 spawnPosition;
    protected Rigidbody rb;
    protected GameObject attackerRoot;
    public bool _hasImpacted = false;
    public int projectileLayer;

    public virtual void SetAttacker(GameObject attacker)
    {
        attackerRoot = attacker.transform.root.gameObject;
    }

    protected virtual void Start()
    {
        projectileLayer = LayerMask.NameToLayer("Projectile");
        spawnPosition = transform.position;
        rb = GetComponent<Rigidbody>();
        SetLayerRecursively(gameObject);
        AttachGeneratedVfx();
        InitializeMovement();
    }

    private void AttachGeneratedVfx()
    {
        if (!useGeneratedVfx) return;
        if (GetComponentInChildren<ParticleSystem>() != null || GetComponentInChildren<TrailRenderer>() != null) return;
        AbilityVfx.Trail(gameObject, VfxColor);
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
        if (other.CompareTag("Objective")) return;
        if (_hasImpacted) return;
        if (attackerRoot != null && other.transform.root.gameObject == attackerRoot) return;
        OnImpact(other);
    }

    protected virtual void OnImpact(Collider other)
    {
        _hasImpacted = true;
        SpawnImpactVisuals(other);
        ApplyDamage(other);
        LayerMask layerMask = other.gameObject.layer;
        //Debug.Log($"Projectile hit {other.gameObject.name} on layer {layerMask}. Pierces: {piercesTargets}");
        if (!piercesTargets || layerMask != LayerMask.NameToLayer("Character"))
        {
            Destroy(gameObject);
        }
        else
        {
            _hasImpacted = false; 
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

        IDamageable hit = other.transform.root.GetComponent<IDamageable>();
        if (hit == null)
        {
            hit = other.GetComponent<IDamageable>();
        }

        DamageDealer.Deal(attackerRoot, hit, damage);
    }

    protected void SpawnImpactVisuals(Collider other)
    {
        if (impactEffect != null)
        {
            Instantiate(impactEffect, transform.position, Quaternion.identity);
        }
        else if (useGeneratedVfx)
        {
            AbilityVfx.Impact(transform.position, VfxColor);
        }
    }

    private void SetLayerRecursively(GameObject obj)
    {
        obj.layer = projectileLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject);
        }
    }
}