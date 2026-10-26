using System.Collections.Generic;
using UnityEngine;

public class TidalSurgeProjectile : BaseProjectile
{
    [Header("Wave Displacement")]
    public float pushImpulse = 16f;
    public float liftImpulse = 4f;
    public float slowMultiplier = 0.5f;
    public float slowDuration = 1.5f;

    private readonly HashSet<Transform> _hitTargets = new();

    protected override void Start()
    {
        base.Start();
        piercesTargets = true;
        InitializeMovement();
    }

    protected override void OnImpact(Collider other)
    {
        Transform targetRoot = other.transform.root;
        if (_hitTargets.Contains(targetRoot)) return;

        TeamMember myTeam = attackerRoot != null ? attackerRoot.GetComponent<TeamMember>() : null;
        if (myTeam != null && !myTeam.IsEnemy(targetRoot.gameObject)) return;

        _hitTargets.Add(targetRoot);
        ApplyDamage(other);

        IMovementController move = targetRoot.GetComponent<IMovementController>();
        if (move != null)
        {
            move.HaltVelocity();
            Vector3 force = (transform.forward * pushImpulse) + (Vector3.up * liftImpulse);
            move.ApplyImpulseForce(force);
            move.ApplySlow(slowMultiplier, slowDuration, decaying: true);
        }

        SpawnImpactVisuals(other);
    }

}