using System.Collections;
using UnityEngine;

public class ShadowStep : Ability, IInitializable
{
    [Header("Shadow Realm Settings")]
    [SerializeField] private float realmDuration = 1.5f;   // Duration spent in shadows
    [SerializeField] private float shadowSpeedMultiplier = 1.6f; // Speed boost multiplier
    
    [Header("Emerge Impact")]
    [SerializeField] private float emergeRadius = 4.5f;
    [SerializeField] private int emergeDamage = 40;
    [SerializeField] private float upwardLaunchForce = 8f;

    [Header("Visuals")]
    [SerializeField] private GameObject sinkVFXPrefab;     
    [SerializeField] private GameObject emergeVFXPrefab;   

    private Transform _rootTransform;
    private IMovementController _movementController;
    private IDamageable _healthComponent;
    
    private int _originalLayer;
    private int _shadowRealmLayer;
    private bool _isInShadowRealm = false;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
        _movementController = playerRoot.GetComponent<IMovementController>();
        _healthComponent = playerRoot.GetComponent<IDamageable>();
        
        _originalLayer = _rootTransform.gameObject.layer;
        _shadowRealmLayer = LayerMask.NameToLayer("ShadowRealm");
    }

    protected override void Activate()
    {
        if (_movementController == null)
        {
            isExecuting = false;
            return;
        }
        StartCoroutine(ShadowRealmRoutine());
    }

    private IEnumerator ShadowRealmRoutine()
    {
        _isInShadowRealm = true;
        
        // 1. Grant invulnerability
        if (_healthComponent != null)
        {
            _healthComponent.IsInvincible = true;
        }

        // 2. Play sink visual
        if (sinkVFXPrefab != null)
        {
            Instantiate(sinkVFXPrefab, _rootTransform.position, Quaternion.identity);
        }
        else
        {
            AbilityVfx.Burst(_rootTransform.position + Vector3.up * 0.5f, AbilityVfx.Violet, 1.4f, 30);
            AbilityVfx.Aura(_rootTransform, AbilityVfx.Violet, realmDuration, 0.5f);
        }

        // 3. Shift to ShadowRealm layer so bullets pass through
        if (_shadowRealmLayer != -1)
        {
            SetLayerRecursively(_rootTransform.gameObject, _shadowRealmLayer);
        }

        // 4. Hide mesh
        SetHadesMeshVisibility(false);

        // 5. Apply movement speed multiplier
        float originalSpeedMultiplier = _movementController.SpeedMultiplier;
        _movementController.SpeedMultiplier = originalSpeedMultiplier * shadowSpeedMultiplier;

        // Clean and simple timer using WaitForSeconds
        yield return new WaitForSeconds(realmDuration);

        // Restore speed before emerging
        if (_movementController != null)
        {
            _movementController.SpeedMultiplier = originalSpeedMultiplier;
        }

        ExecuteEmerge();
        StartCooldown();
    }

    private void ExecuteEmerge()
    {
        if (!_isInShadowRealm) return;
        _isInShadowRealm = false;

        // 1. Restore original layer and visibility
        if (_shadowRealmLayer != -1)
        {
            SetLayerRecursively(_rootTransform.gameObject, _originalLayer);
        }
        SetHadesMeshVisibility(true);

        // 2. Remove invulnerability
        if (_healthComponent != null)
        {
            _healthComponent.IsInvincible = false;
        }

        Vector3 emergePos = _rootTransform.position;

        // 3. Play emerge visual
        if (emergeVFXPrefab != null)
        {
            Instantiate(emergeVFXPrefab, emergePos, Quaternion.identity);
        }
        else
        {
            AbilityVfx.Ring(emergePos + Vector3.up * 0.1f, AbilityVfx.Violet, emergeRadius, 0.45f);
            AbilityVfx.Burst(emergePos + Vector3.up, AbilityVfx.Violet, 1.8f, 36);
        }

        // 4. Erupt out with damage and crowd control
        Collider[] victims = AbilityPhysics.OverlapSphere(emergePos, emergeRadius);
        var myTeam = _rootTransform.GetComponent<TeamMember>();

        foreach (var col in victims)
        {
            Transform victimRoot = col.transform.root;
            if (victimRoot == _rootTransform) continue;
            if (myTeam != null && !myTeam.IsEnemy(victimRoot.gameObject)) continue;

            IDamageable dmg = victimRoot.GetComponent<IDamageable>() ?? victimRoot.GetComponentInChildren<IDamageable>();
            IMovementController targetMove = victimRoot.GetComponent<IMovementController>();

            if (dmg != null)
            {
                Deal(dmg, emergeDamage);
            }

            if (targetMove != null)
            {
                targetMove.HaltVelocity();
                targetMove.ApplyImpulseForce(Vector3.up * upwardLaunchForce);
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    private void SetHadesMeshVisibility(bool isVisible)
    {
        MeshRenderer[] renderers = _rootTransform.GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers)
        {
            if (r.gameObject != _rootTransform.gameObject)
            {
                r.enabled = isVisible;
            }
        }
    }
}