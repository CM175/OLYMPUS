using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UntertowSurge : Ability, IInitializable
{
    [Header("Surge Movement Settings")]
    [SerializeField] private float surgeSpeed = 25f;
    [SerializeField] private float surgeDuration = 2f;
    [SerializeField] private float sweepRadius = 2.5f;
    [SerializeField] private float turnRate = 180f; // Turning speed in degrees per second (allows steering like Charge)

    [Header("Displacement & Damage")]
    [SerializeField] private int surgeDamage = 30;
    [SerializeField] private float pushForce = 20f;

    [Header("Visuals")]
    [SerializeField] private GameObject waveVFXPrefab;
    [SerializeField] private GameObject wallSplashVFXPrefab;

    private Transform _rootTransform;
    private IMovementController _movementController;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
        _movementController = playerRoot.GetComponent<IMovementController>();
    }

    protected override void Activate()
    {
        if (_movementController == null)
        {
            isExecuting = false;
            return;
        }
        StartCoroutine(SurgeRoutine());
    }

    private IEnumerator SurgeRoutine()
    {
        _movementController.IsDashing = true;

        // Ignore collisions between characters so you slide through groups smoothly
        int charLayer = LayerMask.NameToLayer("Character");
        if (charLayer != -1) Physics.IgnoreLayerCollision(charLayer, charLayer, true);

        // Initialize starting direction
        Vector3 surgeDirection = _movementController.GetAimDirection();
        surgeDirection.y = 0f;
        surgeDirection = surgeDirection.sqrMagnitude > 0.001f ? surgeDirection.normalized : _rootTransform.forward;

        if (waveVFXPrefab != null)
        {
            Instantiate(waveVFXPrefab, _rootTransform.position, Quaternion.LookRotation(surgeDirection));
        }

        float elapsed = 0f;
        HashSet<Transform> sweptTargets = new HashSet<Transform>();
        var myTeam = _rootTransform.GetComponent<TeamMember>();

        while (elapsed < surgeDuration)
        {
            // 1. Dynamic Steering Logic (derived from Charge.cs pattern)
            Vector3 targetDirection = _movementController.GetAimDirection();
            targetDirection.y = 0f;
            if (targetDirection.sqrMagnitude > 0.001f)
            {
                targetDirection.Normalize();
                float step = turnRate * Mathf.Deg2Rad * Time.deltaTime;
                surgeDirection = Vector3.RotateTowards(surgeDirection, targetDirection, step, 0.0f);
            }

            // Align rotation with current travel vector
            _rootTransform.rotation = Quaternion.LookRotation(surgeDirection);

            // 3. Move forward along the steered direction
            Vector3 moveStep = surgeDirection * surgeSpeed * Time.deltaTime;
            _movementController.Move(moveStep);

            // 4. Sweep and displace nearby enemies without getting stuck
            Collider[] hits = AbilityPhysics.OverlapSphere(_rootTransform.position + Vector3.up * 0.5f, sweepRadius);
            foreach (var hit in hits)
            {
                Transform enemyRoot = hit.transform.root;
                if (enemyRoot == _rootTransform || sweptTargets.Contains(enemyRoot)) continue;
                if (myTeam != null && !myTeam.IsEnemy(enemyRoot.gameObject)) continue;

                sweptTargets.Add(enemyRoot);

                IDamageable targetDmg = enemyRoot.GetComponent<IDamageable>() ?? enemyRoot.GetComponentInChildren<IDamageable>();
                IMovementController targetMove = enemyRoot.GetComponent<IMovementController>();

                if (targetDmg != null)
                {
                    Deal(targetDmg, surgeDamage);
                }

                if (targetMove != null)
                {
                    targetMove.HaltVelocity();
                    ApplyDisplacement(targetMove, enemyRoot);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Cleanup state
        if (charLayer != -1) Physics.IgnoreLayerCollision(charLayer, charLayer, false);

        _movementController.HaltVelocity();
        _movementController.IsDashing = false;

        StartCooldown();
    }

    

    public override void Cancel()
    {
        base.Cancel();
        StopAllCoroutines();

        // Restore collision layers and movement state immediately when cancelled
        int charLayer = LayerMask.NameToLayer("Character");
        if (charLayer != -1) Physics.IgnoreLayerCollision(charLayer, charLayer, false);

        if (_movementController != null)
        {
            _movementController.HaltVelocity();
            _movementController.IsDashing = false;
        }

        // Put the ability on cooldown when manually cancelled
        StartCooldown();
    }

    private void ApplyDisplacement(IMovementController targetMovement, Transform targetTransform)
    {
        Vector3 forward = _rootTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 toEnemy = targetTransform.position - _rootTransform.position;
        toEnemy.y = 0f;

        Vector3 sideDir = Vector3.Cross(forward, Vector3.up).normalized;

        if (toEnemy.sqrMagnitude > 0.001f && Vector3.Dot(toEnemy.normalized, sideDir) < 0)
        {
            sideDir = -sideDir;
        }

        // Single impulse: 14 horizontal, 5 vertical launch
        Vector3 impulse = sideDir * pushForce;
        targetMovement.HaltVelocity();
        targetMovement.ApplyImpulseForce(impulse);
    }
}
