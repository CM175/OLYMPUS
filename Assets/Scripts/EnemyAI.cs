using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AbilityManager))]
[RequireComponent(typeof(TeamMember))]

public class EnemyAI : MonoBehaviour
{
    [Header("Detection")]
    public float detectionRange = 25f;
    public float loseRange      = 35f;
    public LayerMask searchMask; // Set this to your Player/Hitbox layers

    [Header("Patrol")]
    public float patrolWaitTime = 2f;
    public float patrolRadius   = 15f;

    private NavMeshAgent   _agent;
    private AbilityManager _abilityManager;
    private TeamMember     _teamMember;

    public Transform      _currentTarget;
    private enum State { Idle, Patrol, Chase, Stunned }
    private State _state = State.Idle;

    private float _patrolWaitTimer;
    private bool  _waitingAtPoint;
    private float _targetScanTimer;
    private const float SCAN_INTERVAL = 0.5f;

    // State tracking for charging abilities
    [SerializeField] private float _aiMaxChargeHoldTime = 1.5f;
    private Ability _currentlyChargingAbility = null;
    private float   _aiAttackStartTime = 0f;

    // Exposes stun state to AIMovementController so it can delegate IsStunned
    // without running its own duplicate stun coroutine
    public bool IsStunned => _state == State.Stunned;
    
    private AIMovementController movement;

    private void Awake()
    {
        _agent          = GetComponent<NavMeshAgent>();
        _abilityManager = GetComponent<AbilityManager>();
        _teamMember     = GetComponent<TeamMember>();
        movement = GetComponent<AIMovementController>();
        if (_agent != null)
        {
            _agent.updateRotation = false; // Let your script control rotation
        }
    }

    private void Start()
    {
        _state = State.Patrol;
        SetNewPatrolPoint();
    }

    private void Update()
    {
        if (!_agent.enabled) return;
        if (_state == State.Stunned) return;
        if (movement.IsUnderExternalForce)
            return;

        if (movement.IsUnderExternalForce)
        {
            _agent.ResetPath();
            return;
        }

        HandleTargetScanning();

        switch (_state)
        {
            case State.Idle:
            case State.Patrol:
                HandlePatrol();
                if (_currentTarget != null)
                {
                    _state = State.Chase;
                }
                break;

            case State.Chase:
                if (_currentTarget == null)
                {
                    _state = State.Patrol;
                    SetNewPatrolPoint();
                    break;
                }
                HandleChase();
                break;
        }
    }

    // ── Target Sensing ───────────────────────────────────────────────────────

    private void HandleTargetScanning()
    {
        _targetScanTimer += Time.deltaTime;
        if (_targetScanTimer < SCAN_INTERVAL) return;
        _targetScanTimer = 0f;

        if (_currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, _currentTarget.position);
            if (dist > loseRange || !_currentTarget.gameObject.activeInHierarchy)
            {
                _currentTarget = null;
            }
            return;
        }

        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRange, searchMask);
        float closestDist    = float.MaxValue;
        Transform bestTarget = null;

        foreach (var col in colliders)
        {
            if (col.gameObject == this.gameObject) continue;

            if (_teamMember.IsEnemy(col.gameObject))
            {
                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget  = col.transform;
                }
            }
        }

        _currentTarget = bestTarget;
    }

    // ── Patrol & Chase Movement ──────────────────────────────────────────────

    private void HandlePatrol()
    {
        if (_agent.remainingDistance <= _agent.stoppingDistance)
        {
            if (!_waitingAtPoint)
            {
                _waitingAtPoint  = true;
                _patrolWaitTimer = patrolWaitTime;
            }

            _patrolWaitTimer -= Time.deltaTime;

            if (_patrolWaitTimer <= 0f)
            {
                _waitingAtPoint = false;
                SetNewPatrolPoint();
            }
        }
    }

    private void SetNewPatrolPoint()
    {
        Vector3 randomDir = Random.insideUnitSphere * patrolRadius + transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDir, out hit, patrolRadius, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);
    }

    private void HandleChase()
    {
        float combatRange        = _abilityManager.primaryAttack.range;
        _agent.stoppingDistance  = combatRange * 0.8f;
        _agent.SetDestination(_currentTarget.position);

        Vector3 horizontalLookDir = _currentTarget.position - transform.position;
        horizontalLookDir.y = 0f;

        if (horizontalLookDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(horizontalLookDir),
                Time.deltaTime * 10f
            );
        }
        ExecuteCombatAbilities(Vector3.Distance(transform.position, _currentTarget.position));
    }

    // ── AI Ability Usage Execution ───────────────────────────────────────────

    private void ExecuteCombatAbilities(float distanceToTarget)
    {
        bool canFirePrimary = _abilityManager.primaryAttack != null
            && distanceToTarget <= _abilityManager.primaryAttack.range;
        HandleAbilityExecution(_abilityManager.primaryAttack, canFirePrimary);

        bool canUsePrimaryAbility = _abilityManager.primaryAbility != null
            && distanceToTarget <= 8f;
        HandleAbilityExecution(_abilityManager.primaryAbility, canUsePrimaryAbility);

        bool canUseSecondaryAbility = _abilityManager.secondaryAbility != null
            && distanceToTarget <= 10f;
        HandleAbilityExecution(_abilityManager.secondaryAbility, canUseSecondaryAbility);

        bool canUseMobility = _abilityManager.mobilityAbility != null
            && distanceToTarget >= 18f;
        HandleAbilityExecution(_abilityManager.mobilityAbility, canUseMobility);

        bool canUseUltimate = _abilityManager.ultimateAbility != null
            && distanceToTarget <= 12f;
        HandleAbilityExecution(_abilityManager.ultimateAbility, canUseUltimate);
    }

    private void HandleAbilityExecution(Ability ability, bool shouldTrigger)
    {
        if (ability == null) return;

        if (ability.isChargeable)
        {
            if (shouldTrigger)
            {
                if (_currentlyChargingAbility != ability)
                {
                    if (_currentlyChargingAbility != null)
                        HandleAbilityRelease(_currentlyChargingAbility);

                    _currentlyChargingAbility = ability;
                    _aiAttackStartTime        = Time.time;

                    if (ability is Attack weapon) weapon.OnPress();
                    else ability.Use();
                }
                else
                {
                    if (Time.time - _aiAttackStartTime >= _aiMaxChargeHoldTime)
                        HandleAbilityRelease(ability);
                }
            }
            else
            {
                if (_currentlyChargingAbility == ability)
                    HandleAbilityRelease(ability);
            }
        }
        else
        {
            if (shouldTrigger)
            {
                if (ability is Attack weapon) weapon.OnPress();
                else ability.Use();
            }
        }
    }

    private void HandleAbilityRelease(Ability ability)
    {
        if (ability is Attack weapon) weapon.OnRelease();

        if (_currentlyChargingAbility == ability)
            _currentlyChargingAbility = null;
    }

    // ── Status Effects ───────────────────────────────────────────────────────

    // Single authoritative stun routine. AIMovementController.Stun() delegates
    // here rather than running its own parallel coroutine.
    public void SetStunned(bool stunned)
    {
        if (stunned)
        {
            _state = State.Stunned;
            if (_agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
        }
        else
        {
            _state = State.Patrol;
            if (_agent.enabled && _agent.isOnNavMesh) _agent.isStopped = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseRange);
    }
}