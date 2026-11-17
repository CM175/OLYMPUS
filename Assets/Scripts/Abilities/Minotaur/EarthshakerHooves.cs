using UnityEngine;
using System.Collections.Generic;

public class EarthshakerHooves : Ability, IInitializable
{
    [Header("Stomp Settings")]
    [SerializeField] private float stompRadius = 6.0f;
    [SerializeField] private int baseDamage = 30;

    [Header("Crowd Control")]
    [SerializeField] private float slowMultiplier = 0.4f; // Heavy slow
    [SerializeField] private float slowDuration = 1.5f;
    [SerializeField] private float knockbackForce = 30.0f;
    [SerializeField] private float upwardPop = 10.0f;

    [Header("Visuals")]
    [SerializeField] private GameObject stompVFXPrefab;

    private Transform _rootTransform;
    private IMovementController _movementController;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
        _movementController = playerRoot.GetComponent<IMovementController>();
    }

    protected override void Activate()
    {
        if (_rootTransform == null) _rootTransform = transform.root;
        ExecuteStomp();
    }

    private void ExecuteStomp()
    {
        // Brief stationary lock during the stomp
        if (_movementController != null)
        {
            _movementController.HaltVelocity();
        }

        Vector3 centerPos = _rootTransform.position;

        if (stompVFXPrefab != null)
        {
            Instantiate(stompVFXPrefab, centerPos, Quaternion.identity);
        }
        else
        {
            AbilityVfx.Ring(centerPos + Vector3.up * 0.1f, AbilityVfx.Crimson, stompRadius, 0.5f);
            AbilityVfx.Burst(centerPos + Vector3.up * 0.3f, AbilityVfx.Dust, 1.8f, 35, 0.6f);
        }

        Collider[] hitEnemies = AbilityPhysics.OverlapSphere(centerPos, stompRadius);
        var myTeam = _rootTransform.GetComponent<TeamMember>();
        HashSet<Transform> processedVictims = new HashSet<Transform>();

        foreach (var col in hitEnemies)
        {
            Transform victimRoot = col.transform.root;
            if (victimRoot == _rootTransform || processedVictims.Contains(victimRoot)) continue;
            if (myTeam != null && !myTeam.IsEnemy(victimRoot.gameObject)) continue;
            
            processedVictims.Add(victimRoot);

            IDamageable damageable = victimRoot.GetComponent<IDamageable>() ?? victimRoot.GetComponentInChildren<IDamageable>();
            IMovementController targetMovement = victimRoot.GetComponent<IMovementController>();

            if (damageable != null)
            {

                Deal(damageable, baseDamage);

                if (targetMovement != null)
                {
                    // Apply slow and knockback disruption together
                    targetMovement.ApplySlow(slowMultiplier, slowDuration, decaying: true);

                    Vector3 pushDir = victimRoot.position - centerPos;
                    pushDir.y = 0f;
                    if (pushDir.sqrMagnitude < 0.001f) pushDir = _rootTransform.forward;
                    pushDir.Normalize();

                    Vector3 impulse = (pushDir * knockbackForce) + (Vector3.up * upwardPop);
                    targetMovement.HaltVelocity();
                    targetMovement.ApplyImpulseForce(impulse);
                }
            }
        }

        StartCooldown();
    }
}
