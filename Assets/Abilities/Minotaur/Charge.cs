using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

public class Charge : Ability, IInitializable
{
    [Header("Charge Speeds")]
    [SerializeField] private float chargeSpeed = 5f;
    [SerializeField] private float chargeDuration = 2.0f;
    [SerializeField] private float hitSweepRadius = 2.0f;

    [Header("Damage Profile")]
    [SerializeField] private int impactDamage = 30; 
    [SerializeField] private int pinWallCrushDamage = 75; 

    // REFACTORED: Use decoupled abstraction interface here instead of PlayerController
    private IMovementController _movementController;
    private CharacterController _characterController; // May be null for AI
    private Transform _rootTransform;
    
    private NavMeshAgent _pinnedAgent = null;
    private EnemyAI _pinnedAI = null;

    private HashSet<Transform> _hitSecondaryTargets = new HashSet<Transform>();

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
        _characterController = playerRoot.GetComponent<CharacterController>();
        _movementController = playerRoot.GetComponent<IMovementController>();
    }

    protected override void Activate()
    {
        if (_movementController == null)
        {
            isExecuting = false;
            return;
        }

        StartCoroutine(ChargeRoutine());
    }

    // Position of the charging entity. Falls back to the root transform
    // for AI agents that don't have a CharacterController.
    private Vector3 GetCurrentPosition()
    {
        return _characterController != null
            ? _characterController.transform.position
            : _rootTransform.position;
    }

    // Collider center offset, used for sweep positioning. Falls back to
    // a generic "chest height" offset for AI agents.
    private Vector3 GetCenterOffset()
    {
        return _characterController != null
            ? _characterController.center
            : Vector3.up;
    }

    private IEnumerator ChargeRoutine()
    {
        _pinnedAgent = null;
        _pinnedAI = null;
        _hitSecondaryTargets.Clear(); 

        int characterLayer = LayerMask.NameToLayer("Character");
        if (characterLayer != -1)
        {
            Physics.IgnoreLayerCollision(characterLayer, characterLayer, true);
        }

        _movementController.IsDashing = true;
        float speed = chargeSpeed;

        float elapsedTime = 0f;
        Vector3 chargeDirection = _rootTransform.forward;
        chargeDirection.y = 0f;
        chargeDirection.Normalize();

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, GetCurrentPosition(), Quaternion.LookRotation(chargeDirection));
        }

        while (elapsedTime < chargeDuration)
        {
            Vector3 movementThisFrame = chargeDirection * speed * Time.deltaTime;
            movementThisFrame.y = -4f * Time.deltaTime; 

            // REFACTORED: Displace frame positions natively via the interface controller
            _movementController.TeleportOrMove(movementThisFrame);

            int wallMask = LayerMask.GetMask("Ground");

            if (Physics.Raycast(
                GetCurrentPosition(),
                chargeDirection,
                out RaycastHit ray,
                1.2f,
                wallMask))
            {
                if (_pinnedAgent != null) ExecuteWallCrush();
                yield return new WaitForSeconds(0.5f);
                break;
            }

            Vector3 sweepCenter = GetCurrentPosition() + GetCenterOffset() + (chargeDirection * 0.5f);
            Collider[] hits = Physics.OverlapSphere(sweepCenter, hitSweepRadius);

            foreach (var hit in hits)
            {
                var myTeam = _rootTransform.GetComponent<TeamMember>();
                
                if (myTeam != null && !myTeam.IsEnemy(hit.transform.root.gameObject))
                {
                    continue;
                }

                if (_hitSecondaryTargets.Contains(hit.transform.root)) continue;

                IDamageable target = hit.transform.root.GetComponent<IDamageable>();
                if (target != null)
                {
                    NavMeshAgent enemyAgent = hit.transform.root.GetComponent<NavMeshAgent>();
                    EnemyAI enemyAI = hit.transform.root.GetComponent<EnemyAI>();

                    if (enemyAgent != null && enemyAgent.enabled)
                    {
                        if (_pinnedAgent == null)
                        {
                            _pinnedAgent = enemyAgent;
                            _pinnedAI = enemyAI;
                            
                            _pinnedAgent.isStopped = true;
                            if (_pinnedAI != null) _pinnedAI.Stun(chargeDuration + 1f);

                            speed *= 0.85f; 
                        }
                        else if (enemyAgent != _pinnedAgent)
                        {
                            _hitSecondaryTargets.Add(hit.transform.root);

                            target.TakeDamage((int)(impactDamage * 0.5f));
                            if (enemyAI != null) enemyAI.Stun(1f);

                            Vector3 forward = _rootTransform.forward;
                            forward.y = 0f;
                            forward.Normalize();

                            Vector3 toEnemy = enemyAgent.transform.position - GetCurrentPosition();
                            toEnemy.y = 0f;
                            toEnemy.Normalize();

                            Vector3 sideDir = Vector3.Cross(forward, Vector3.up).normalized;

                            if (Vector3.Dot(toEnemy, sideDir) < 0)
                                sideDir = -sideDir;
                            StartCoroutine(AnimateChargeBounce(enemyAgent, sideDir));
                        }
                    }
                }
            }

            if (_pinnedAgent != null && _pinnedAgent.gameObject != null)
            {
                Vector3 desiredPinnedPosition = GetCurrentPosition() + (chargeDirection * 1.5f);
                desiredPinnedPosition.y = _pinnedAgent.transform.position.y;

                Vector3 frameDisplacement = desiredPinnedPosition - _pinnedAgent.transform.position;
                _pinnedAgent.Move(frameDisplacement * (Time.deltaTime * 25f));
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        if (characterLayer != -1)
        {
            Physics.IgnoreLayerCollision(characterLayer, characterLayer, false);
        }

        if (_pinnedAgent != null && _pinnedAgent.gameObject != null)
        {
            IDamageable targetDamage = _pinnedAgent.GetComponentInChildren<IDamageable>();
            targetDamage?.TakeDamage(impactDamage);
            _pinnedAgent.isStopped = false;
        }

        _movementController.HaltVelocity();
        _movementController.IsDashing = false;

        StartCooldown();
    }

    private void ExecuteWallCrush()
    {
        IDamageable target = _pinnedAgent.GetComponentInChildren<IDamageable>();
        target?.TakeDamage(pinWallCrushDamage);

        if (_pinnedAI != null)
        {
            _pinnedAI.Stun(1f);
        }
        
        _pinnedAgent = null;
        _pinnedAI = null;
    }

    private IEnumerator AnimateChargeBounce(NavMeshAgent agent, Vector3 direction)
    {
        agent.isStopped = true;

        float duration = 0.35f;
        float elapsed = 0f;

        direction += Random.insideUnitSphere * 0.1f;
        direction.y = 0;
        direction.Normalize();

        Vector3 horizontalStep = direction * 3f;
        float arcHeight = 0.5f;

        EnemyAI ai = agent.GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.Stun(duration + 0.2f);
        }

        Transform t = agent.transform;
        Vector3 startPos = t.position;

        while (elapsed < duration)
        {
            float tNorm = elapsed / duration;
            float heightOffset = Mathf.Sin(tNorm * Mathf.PI) * arcHeight;

            Vector3 nextPos = startPos + (horizontalStep * tNorm);
            nextPos.y += heightOffset;

            if (NavMesh.SamplePosition(nextPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                t.position = hit.position + Vector3.up * heightOffset;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (NavMesh.SamplePosition(t.position, out NavMeshHit finalHit, 4f, NavMesh.AllAreas))
        {
            t.position = finalHit.position;
        }

        agent.isStopped = false;
    }
}