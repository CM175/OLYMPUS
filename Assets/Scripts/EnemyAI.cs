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
    
    private Transform      _currentTarget;
    private enum State { Idle, Patrol, Chase, Stunned }
    private State _state = State.Idle;

    private float _patrolWaitTimer;
    private bool  _waitingAtPoint;
    private float _targetScanTimer;
    private const float SCAN_INTERVAL = 0.5f; // Scan for targets twice a second

    // State tracking for charging abilities
    [SerializeField] private float _aiMaxChargeHoldTime = 1.5f;
    private Ability _currentlyChargingAbility = null;
    private float _aiAttackStartTime = 0f;

    private void Awake()
    {
        _agent          = GetComponent<NavMeshAgent>();
        _abilityManager = GetComponent<AbilityManager>();
        _teamMember     = GetComponent<TeamMember>();
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

        // If chasing, verify target isn't too far away
        if (_currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, _currentTarget.position);
            if (dist > loseRange || !_currentTarget.gameObject.activeInHierarchy)
            {
                _currentTarget = null;
            }
            return;
        }

        // Search for nearest opposing team member
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRange, searchMask);
        float closestDist = float.MaxValue;
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
                    bestTarget = col.transform;
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
        // Soft-coded combat ranges based on loaded primary attack settings
        float combatRange = _abilityManager.primaryAttack.range;
        _agent.stoppingDistance = combatRange * 0.8f;
        _agent.SetDestination(_currentTarget.position);

        // Always face the target smoothly while fighting
        Vector3 lookDir = (_currentTarget.position - transform.position).normalized;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
        }

        // Combat logic engagement 
        ExecuteCombatAbilities(Vector3.Distance(transform.position, _currentTarget.position));
    }

    // ── AI Ability Usage Execution ───────────────────────────────────────────

    private void ExecuteCombatAbilities(float distanceToTarget)
    {
        // 1. Primary Weapon Evaluation
        bool canFirePrimary = _abilityManager.primaryAttack != null && distanceToTarget <= _abilityManager.primaryAttack.range;
        HandleAbilityExecution(_abilityManager.primaryAttack, canFirePrimary);

        // 2. Tactical / Crowd Control Ability Evaluation
        bool canUsePrimaryAbility = _abilityManager.primaryAbility != null && distanceToTarget <= 8f;
        HandleAbilityExecution(_abilityManager.primaryAbility, canUsePrimaryAbility);

        bool canUseSecondaryAbility = _abilityManager.secondaryAbility != null && distanceToTarget <= 10f;
        HandleAbilityExecution(_abilityManager.secondaryAbility, canUseSecondaryAbility);

        // 3. Mobility Evaluation (e.g., target pulling away)
        bool canUseMobility = _abilityManager.mobilityAbility != null && distanceToTarget >= 18f;
        HandleAbilityExecution(_abilityManager.mobilityAbility, canUseMobility);

        // 4. Ultimate Evaluation
        bool canUseUltimate = _abilityManager.ultimateAbility != null && distanceToTarget <= 12f;
        HandleAbilityExecution(_abilityManager.ultimateAbility, canUseUltimate);
    }

    private void HandleAbilityExecution(Ability ability, bool shouldTrigger)
    {
        if (ability == null) return;

        // Handle Chargeable Abilities (e.g., Crescent Bow, Heavy Spells)
        if (ability.isChargeable)
        {
            if (shouldTrigger)
            {
                // If we haven't started charging this specific ability yet, press it down
                if (_currentlyChargingAbility != ability)
                {
                    // Safety: If we were somehow charging something else, release it first
                    if (_currentlyChargingAbility != null) HandleAbilityRelease(_currentlyChargingAbility);

                    _currentlyChargingAbility = ability;
                    _aiAttackStartTime = Time.time;
                    
                    // If it's a weapon, use weapon hooks; otherwise use standard ability hooks
                    if (ability is Attack weapon) weapon.OnPress();
                    else ability.Use(); // Or your base charge initiation method if separate
                }
                else
                {
                    // It is already charging. Check if it's time to release!
                    if (Time.time - _aiAttackStartTime >= _aiMaxChargeHoldTime)
                    {
                        HandleAbilityRelease(ability);
                    }
                }
            }
            else
            {
                // Target moved away or died while we were charging
                if (_currentlyChargingAbility == ability)
                {
                    HandleAbilityRelease(ability);
                }
            }
        }
        // Handle Standard Instant/Automatic Abilities
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
        // If your non-weapon abilities need an explicit Release() hook in the future, add it here:
        // else ability.OnRelease(); 

        if (_currentlyChargingAbility == ability)
        {
            _currentlyChargingAbility = null;
        }
    }

    // ── Status Effects ───────────────────────────────────────────────────────

    public void Stun(float duration)
    {
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        if (!_agent.enabled) yield break;
        State previousState = _state;
        _state           = State.Stunned;
        _agent.isStopped = true;

        yield return new WaitForSeconds(duration);

        if (_agent != null && gameObject.activeInHierarchy)
        {
            _agent.isStopped = false;
            _state           = previousState == State.Stunned ? State.Patrol : previousState;
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