using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SonicBoom : Ultimate, IInitializable
{
    [Header("Dash Mechanics")]
    public float maxDistance = 25f;
    public float windupTime = 0.3f;
    public float sweepRadius = 2.5f;
    public LayerMask obstacleMask = ~0;

    [Header("Damage & Silence Profile")]
    public int baseDamage = 75;
    public float silenceDuration = 1.5f;
    public float pushbackImpulse = 10f;

    [Header("Visuals & FX")]
    public GameObject sonicTrailVfxPrefab;
    public GameObject impactBurstVfxPrefab;

    private Transform _rootTransform;
    private IMovementController _movementController;
    private AbilityManager _abilityManager;
    private GameObject _activeVfxInstance;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
        _movementController = playerRoot.GetComponent<IMovementController>();
        _abilityManager = playerRoot.GetComponent<AbilityManager>();
    }

    protected override void Activate()
    {
        base.Activate();
        if (_movementController == null)
        {
            isExecuting = false;
            return;
        }

        StartCoroutine(SonicBoomRoutine());
    }

    private IEnumerator SonicBoomRoutine()
    {
        exclusiveState = true;
        _movementController.HaltVelocity();
        _movementController.IsDashing = true;

        // 1. Telegraph / Windup window
        Vector3 aimDirection = _movementController.GetAimDirection();
        aimDirection.y = 0f;
        aimDirection = aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : _rootTransform.forward;
        _rootTransform.rotation = Quaternion.LookRotation(aimDirection);

        yield return new WaitForSeconds(windupTime);

        // 2. Trajectory Obstacle Collision Check (Stop short of walls)
        Vector3 startPos = _rootTransform.position;
        float finalDistance = maxDistance;

        int maskWithoutCharacters = obstacleMask & ~(1 << LayerMask.NameToLayer("Character"));
        if (Physics.SphereCast(startPos + Vector3.up * 0.5f, 0.4f, aimDirection, out RaycastHit wallHit, maxDistance, maskWithoutCharacters, QueryTriggerInteraction.Ignore))
        {
            finalDistance = Mathf.Max(0.5f, wallHit.distance - 0.5f);
        }

        Vector3 endPos = startPos + (aimDirection * finalDistance);

        // 3. Trail Visuals
        if (sonicTrailVfxPrefab != null)
        {
            _activeVfxInstance = Instantiate(sonicTrailVfxPrefab, startPos, Quaternion.LookRotation(aimDirection));
            Destroy(_activeVfxInstance, 2.0f);
        }

       // 4. Ultra-Fast Dash Execution (~0.1s duration)
        int charLayer = LayerMask.NameToLayer("Character");
        if (charLayer != -1) Physics.IgnoreLayerCollision(charLayer, charLayer, true);

        float dashDuration = 0.1f;
        float dashTimer = 0f;
        Vector3 dashVelocity = aimDirection * (finalDistance / dashDuration);

        while (dashTimer < dashDuration)
        {
            _movementController.Move(dashVelocity * Time.deltaTime);
            dashTimer += Time.deltaTime;
            yield return null;
        }

        if (charLayer != -1) Physics.IgnoreLayerCollision(charLayer, charLayer, false);

        _movementController.HaltVelocity();
        _movementController.IsDashing = false;

        ProcessSonicHits(startPos, endPos, aimDirection, finalDistance);

        // 5. Impact End Visual
        if (impactBurstVfxPrefab != null)
        {
            GameObject burst = Instantiate(impactBurstVfxPrefab, endPos, Quaternion.identity);
            Destroy(burst, 2.0f);
        }

        exclusiveState = false;
        StartCooldown();
    }

    private void ProcessSonicHits(Vector3 start, Vector3 end, Vector3 dir, float dist)
    {
        RaycastHit[] hits = Physics.SphereCastAll(start + Vector3.up * 0.5f, sweepRadius, dir, dist);
        TeamMember myTeam = _rootTransform.GetComponent<TeamMember>();

        int finalDmg = playerStats != null
            ? Mathf.RoundToInt(baseDamage * playerStats.DamageMultiplier)
            : baseDamage;

        HashSet<Transform> hitVictims = new HashSet<Transform>();

        foreach (var hit in hits)
        {
            Transform victimRoot = hit.collider.transform.root;
            if (victimRoot == _rootTransform || hitVictims.Contains(victimRoot)) continue;
            if (myTeam != null && !myTeam.IsEnemy(victimRoot.gameObject)) continue;

            hitVictims.Add(victimRoot);

            // Damage
            IDamageable dmg = victimRoot.GetComponent<IDamageable>() ?? victimRoot.GetComponentInChildren<IDamageable>();
            dmg?.TakeDamage(finalDmg);

            // Impulse & Interrupt
            IMovementController targetMovement = victimRoot.GetComponent<IMovementController>();
            if (targetMovement != null)
            {
                targetMovement.HaltVelocity();
                Vector3 knockbackDir = dir + (Vector3.up * 0.25f);
                targetMovement.ApplyImpulseForce(knockbackDir.normalized * pushbackImpulse);
            }

            // Silence (Interrupts ongoing casts and locks abilities)
            if (victimRoot.TryGetComponent<AbilityManager>(out var enemyAbilities))
            {
                StartCoroutine(ApplySilence(enemyAbilities, silenceDuration));
            }
        }
    }

    private IEnumerator ApplySilence(AbilityManager targetAbilities, float duration)
    {
        targetAbilities.InterruptAllAbilities();
        targetAbilities.enabled = false;
        yield return new WaitForSeconds(duration);
        if (targetAbilities != null)
        {
            targetAbilities.enabled = true;
        }
    }

    public override void Cancel()
    {
        base.Cancel();
        exclusiveState = false;
        CleanupVfx();
    }

    private void OnDisable()
    {
        exclusiveState = false;
        CleanupVfx();
    }

    private void CleanupVfx()
    {
        if (_activeVfxInstance != null)
        {
            Destroy(_activeVfxInstance);
            _activeVfxInstance = null;
        }
    }
}